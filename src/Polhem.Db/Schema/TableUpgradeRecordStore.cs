using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Polhem.Core;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Db.Manager;
using Polhem.Db.Providers;
using Polhem.Definition.Database;

namespace Polhem.Db.Schema
{
    /// <summary>
    /// Reads and writes the upgrade records of one database: one row per upgraded table in <c>st_table_upgrade</c>,
    /// with the outcome of its last upgrade, the <see cref="SchemaFormat"/> and framework version of the tool that ran
    /// it, and the table schema it applied.
    /// </summary>
    /// <remarks>
    /// The record table is created by the first upgrade that changes a table of the database, from a schema built in
    /// code; it is not declared in any definition. A database without it was never upgraded by a tool that writes
    /// records, and counts as format 0.
    /// </remarks>
    public sealed class TableUpgradeRecordStore
    {
        /// <summary>
        /// The name of the record table.
        /// </summary>
        public const string DefaultRecordTableName = "st_table_upgrade";

        private const int TableNameLength = 128;
        private const int FrameworkVersionLength = 50;
        private const int StatusLength = 20;
        private const int HashLength = 64;

        private readonly string _databaseId;
        private readonly IDbConnectionManager _connectionManager;
        private readonly IDialectFactory _dialect;
        private readonly DatabaseType _databaseType;
        private bool _tableExists;
        private Dictionary<string, RecordSummary>? _summaries;

        /// <summary>
        /// Initializes a new instance of <see cref="TableUpgradeRecordStore"/> for the specified database.
        /// </summary>
        /// <param name="databaseId">The database whose records to read and write.</param>
        /// <param name="connectionManager">The DI-resolved connection manager.</param>
        public TableUpgradeRecordStore(string databaseId, IDbConnectionManager connectionManager)
            : this(databaseId, connectionManager, DefaultRecordTableName)
        { }

        /// <summary>
        /// Initializes a new instance with a record table of another name, so a test can keep its records apart from
        /// the ones the other tests of the same database write.
        /// </summary>
        internal TableUpgradeRecordStore(string databaseId, IDbConnectionManager connectionManager, string recordTableName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(databaseId);
            ArgumentNullException.ThrowIfNull(connectionManager);
            ArgumentException.ThrowIfNullOrWhiteSpace(recordTableName);
            _databaseId = databaseId;
            _connectionManager = connectionManager;
            _databaseType = connectionManager.GetConnectionInfo(databaseId).DatabaseType;
            _dialect = DbDialectRegistry.Get(_databaseType);
            RecordTableName = recordTableName;
        }

        /// <summary>
        /// Gets the name of the record table.
        /// </summary>
        public string RecordTableName { get; }

        /// <summary>
        /// Returns the highest <see cref="SchemaFormat"/> version any upgrade of this database has recorded, or 0 when
        /// the database has no record table or no records.
        /// </summary>
        public int GetHighestFormatVersion()
        {
            if (!TableExists()) return 0;
            var value = CreateDbAccess().ExecuteScalar(
                $"SELECT MAX(format_version) FROM {RecordTableName} WHERE status <> {{0}}", nameof(TableUpgradeStatus.Verified));
            return value is null or DBNull ? 0 : ValueUtilities.CInt(value);
        }

        /// <summary>
        /// Describes why this database must not be upgraded by this build of the framework, or returns <c>null</c>
        /// when it may be.
        /// </summary>
        /// <remarks>
        /// A database that an upgrade of a higher <see cref="SchemaFormat"/> has changed must not be upgraded with a
        /// lower one: the older tool would take the newer shape for a difference and change it back.
        /// </remarks>
        public string? DescribeNewerFormat()
        {
            int highest = GetHighestFormatVersion();
            if (highest <= SchemaFormat.Current) return null;
            return $"Database '{_databaseId}' was upgraded by a tool of schema format {highest}" +
                   FrameworkVersionSuffix(highest) + $"; this framework only knows schema format {SchemaFormat.Current}. " +
                   "Upgrade it with a newer tool. Nothing in the database was changed.";
        }

        /// <summary>
        /// Returns the record of the specified table, or <c>null</c> when no upgrade of it has been recorded.
        /// </summary>
        /// <param name="tableName">The table name.</param>
        public TableUpgradeRecord? GetRecord(string tableName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
            if (!TableExists()) return null;
            var table = CreateDbAccess().ExecuteDataTable(
                "SELECT table_name, format_version, framework_version, status, error, schema_xml, schema_hash, " +
                $"sys_update_time FROM {RecordTableName} WHERE table_name = {{0}}", Key(tableName));
            if (table is null || table.Rows.Count == 0) return null;
            var row = table.Rows[0];
            return new TableUpgradeRecord
            {
                TableName = ValueUtilities.CStr(row["table_name"]),
                FormatVersion = ValueUtilities.CInt(row["format_version"]),
                FrameworkVersion = ValueUtilities.CStr(row["framework_version"]),
                Status = ParseStatus(ValueUtilities.CStr(row["status"])),
                Error = ValueUtilities.CStr(row["error"]),
                SchemaXml = ValueUtilities.CStr(row["schema_xml"]),
                SchemaHash = ValueUtilities.CStr(row["schema_hash"]),
                UpdateTime = ValueUtilities.CDateTime(row["sys_update_time"], DateTime.MinValue),
            };
        }

        /// <summary>
        /// Returns whether the table's record says its last upgrade succeeded with this <see cref="SchemaFormat"/> and
        /// with <paramref name="define"/> as it is now, so the table can be taken as unchanged without reading the
        /// database's catalog.
        /// </summary>
        /// <remarks>
        /// The first call reads the table name, status, format version and schema hash of every record in the database
        /// in one query; the recorded schemas themselves are not read. Later calls use what that query returned, kept
        /// current by the writes made through this instance.
        /// </remarks>
        /// <param name="define">The table's definition.</param>
        public bool IsRecordedAsCurrent(TableSchema define)
        {
            ArgumentNullException.ThrowIfNull(define);
            Dictionary<string, RecordSummary> summaries;
            try
            {
                summaries = LoadSummaries();
            }
            catch (Exception ex) when (ex is DbException or InvalidOperationException)
            {
                // Records that cannot be read only cost the shortcut: the caller compares with the database instead.
                return false;
            }
            return summaries.TryGetValue(Key(define.TableName), out var summary)
                && summary.Status is TableUpgradeStatus.Succeeded or TableUpgradeStatus.Verified
                && summary.FormatVersion == SchemaFormat.Current
                && string.Equals(summary.SchemaHash, ComputeHash(XmlCodec.Serialize(define)), StringComparison.Ordinal);
        }

        /// <summary>
        /// Records that a comparison with the database found the table already matching <paramref name="define"/>,
        /// unless its record says so already.
        /// </summary>
        /// <remarks>
        /// This gives a table that needed no upgrade the record a later <see cref="UpgradeCompareMode.Fast"/> compare
        /// relies on, and corrects a record left <see cref="TableUpgradeStatus.Upgrading"/> or
        /// <see cref="TableUpgradeStatus.Failed"/>. The record becomes <see cref="TableUpgradeStatus.Verified"/>
        /// unless this format wrote it, so a deployment that changed no table does not refuse a rollback to an older
        /// tool. Nothing is written in a database a tool of a higher <see cref="SchemaFormat"/> has upgraded. The table
        /// matches already, so a record that cannot be written does not fail the caller.
        /// </remarks>
        /// <returns><c>false</c> when the record could not be written.</returns>
        /// <param name="define">The definition the table was found to match.</param>
        public bool TryRecordUnchanged(TableSchema define)
        {
            ArgumentNullException.ThrowIfNull(define);
            try
            {
                if (IsRecordedAsCurrent(define)) return true;
                var summaries = LoadSummaries();
                if (summaries.Values.Any(r => r.Status != TableUpgradeStatus.Verified && r.FormatVersion > SchemaFormat.Current))
                    return true;
                EnsureTable();
                string xml = XmlCodec.Serialize(define);
                string hash = ComputeHash(xml);
                string key = Key(define.TableName);
                // A record this format already wrote keeps counting as this format's upgrade; any other becomes a
                // verification, which does not refuse an older tool for a table nobody changed.
                var recorded = summaries.TryGetValue(key, out var existing) && existing.FormatVersion == SchemaFormat.Current
                    && existing.Status != TableUpgradeStatus.Verified
                    ? TableUpgradeStatus.Succeeded
                    : TableUpgradeStatus.Verified;
                string status = recorded.ToString();
                var now = DateTime.UtcNow;
                Upsert(define.TableName,
                    () => new DbCommandSpec(DbCommandKind.NonQuery,
                        $"UPDATE {RecordTableName} SET format_version = {{1}}, framework_version = {{2}}, status = {{3}}, " +
                        "error = {4}, schema_xml = {5}, schema_hash = {6}, sys_update_time = {7} WHERE table_name = {0}",
                        key, SchemaFormat.Current, FrameworkVersion, status, string.Empty, xml, hash, now),
                    () => new DbCommandSpec(DbCommandKind.NonQuery,
                        $"INSERT INTO {RecordTableName} (table_name, format_version, framework_version, status, error, " +
                        "schema_xml, schema_hash, sys_update_time) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})",
                        key, SchemaFormat.Current, FrameworkVersion, status, string.Empty, xml, hash, now));
                Remember(key, recorded, hash);
                return true;
            }
            catch (Exception ex) when (ex is DbException or InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// Throws when this database must not be upgraded by this build of the framework.
        /// </summary>
        /// <exception cref="InvalidOperationException">An upgrade of a higher schema format has changed the database.</exception>
        internal void ThrowIfNewerFormat()
        {
            var reason = DescribeNewerFormat();
            if (reason is not null)
                throw new InvalidOperationException(reason);
        }

        /// <summary>
        /// Records that an upgrade of the table is starting, creating the record table if the database has none.
        /// </summary>
        /// <remarks>
        /// Written before any statement of the upgrade runs, so a tool of a lower format is refused even when this
        /// upgrade fails or stops half way. The schema of the last successful upgrade is kept.
        /// </remarks>
        /// <param name="tableName">The table about to be upgraded.</param>
        internal void MarkUpgrading(string tableName)
        {
            EnsureTable();
            var now = DateTime.UtcNow;
            string key = Key(tableName);
            string status = nameof(TableUpgradeStatus.Upgrading);
            var update = () => new DbCommandSpec(DbCommandKind.NonQuery,
                $"UPDATE {RecordTableName} SET format_version = {{1}}, framework_version = {{2}}, status = {{3}}, " +
                "error = {4}, sys_update_time = {5} WHERE table_name = {0}",
                key, SchemaFormat.Current, FrameworkVersion, status, string.Empty, now);
            var insert = () => new DbCommandSpec(DbCommandKind.NonQuery,
                $"INSERT INTO {RecordTableName} (table_name, format_version, framework_version, status, error, " +
                "schema_xml, schema_hash, sys_update_time) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})",
                key, SchemaFormat.Current, FrameworkVersion, status, string.Empty, string.Empty, string.Empty, now);
            Upsert(tableName, update, insert);
            Remember(key, TableUpgradeStatus.Upgrading, null);
        }

        /// <summary>
        /// Records that the upgrade of the table succeeded with the specified schema.
        /// </summary>
        /// <remarks>
        /// The table has already changed when this runs, so a record that cannot be written does not turn the upgrade
        /// into a failure. The record then stays <see cref="TableUpgradeStatus.Upgrading"/>.
        /// </remarks>
        /// <returns><c>false</c> when the record could not be written.</returns>
        /// <param name="schema">The schema the upgrade applied.</param>
        internal bool TryMarkSucceeded(TableSchema schema)
        {
            ArgumentNullException.ThrowIfNull(schema);
            string xml = XmlCodec.Serialize(schema);
            string hash = ComputeHash(xml);
            bool written = TryWrite(
                $"UPDATE {RecordTableName} SET status = {{1}}, error = {{2}}, schema_xml = {{3}}, schema_hash = {{4}}, " +
                "sys_update_time = {5} WHERE table_name = {0}",
                Key(schema.TableName), nameof(TableUpgradeStatus.Succeeded), string.Empty, xml, hash,
                DateTime.UtcNow);
            if (written) Remember(Key(schema.TableName), TableUpgradeStatus.Succeeded, hash);
            return written;
        }

        /// <summary>
        /// Records that the upgrade of the table failed, keeping the schema of the last upgrade that succeeded.
        /// </summary>
        /// <returns><c>false</c> when the record could not be written.</returns>
        /// <param name="tableName">The table whose upgrade failed.</param>
        /// <param name="error">The error to record.</param>
        /// <remarks>
        /// The upgrade's own error is the one the caller reports, so a record that cannot be written does not replace
        /// it. The record then stays <see cref="TableUpgradeStatus.Upgrading"/>.
        /// </remarks>
        internal bool TryMarkFailed(string tableName, string error)
        {
            bool written = TryWrite(
                $"UPDATE {RecordTableName} SET status = {{1}}, error = {{2}}, sys_update_time = {{3}} " +
                "WHERE table_name = {0}",
                Key(tableName), nameof(TableUpgradeStatus.Failed), error, DateTime.UtcNow);
            if (written) Remember(Key(tableName), TableUpgradeStatus.Failed, null);
            return written;
        }

        private bool TryWrite(string sql, params object[] values)
        {
            try
            {
                CreateDbAccess().ExecuteNonQuery(sql, values);
                return true;
            }
            catch (Exception ex) when (ex is DbException or InvalidOperationException)
            {
                // `InvalidOperationException` covers a connection the pool could not hand out in time.
                return false;
            }
        }

        /// <summary>
        /// Returns the SHA-256 of a schema's XML in lowercase hexadecimal.
        /// </summary>
        /// <param name="schemaXml">The schema XML.</param>
        public static string ComputeHash(string schemaXml)
        {
            ArgumentNullException.ThrowIfNull(schemaXml);
            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(schemaXml)));
        }

        /// <summary>
        /// Builds the schema of the record table.
        /// </summary>
        /// <param name="recordTableName">The name of the record table.</param>
        internal static TableSchema BuildRecordSchema(string recordTableName)
        {
            var schema = new TableSchema { TableName = recordTableName, DisplayName = "Table upgrade records" };
            schema.Fields!.Add("table_name", "Table name", FieldDbType.String, TableNameLength);
            schema.Fields!.Add("format_version", "Schema format version", FieldDbType.Integer);
            schema.Fields!.Add("framework_version", "Framework version", FieldDbType.String, FrameworkVersionLength);
            schema.Fields!.Add("status", "Status", FieldDbType.String, StatusLength);
            schema.Fields!.Add("error", "Error", FieldDbType.Text);
            schema.Fields!.Add("schema_xml", "Table schema", FieldDbType.Text);
            schema.Fields!.Add("schema_hash", "Table schema hash", FieldDbType.String, HashLength);
            schema.Fields!.Add("sys_update_time", "Update time", FieldDbType.DateTime);
            schema.Indexes!.AddPrimaryKey("table_name");
            return schema;
        }

        private string FrameworkVersionSuffix(int formatVersion)
        {
            var table = CreateDbAccess().ExecuteDataTable(
                $"SELECT DISTINCT framework_version FROM {RecordTableName} WHERE format_version = {{0}} AND status <> {{1}}",
                formatVersion, nameof(TableUpgradeStatus.Verified));
            // Compared as versions: as text, 1.10.0 would sort below 1.9.0.
            string text = (table?.Rows.Cast<DataRow>() ?? [])
                .Select(row => ValueUtilities.CStr(row[0]))
                .Where(v => v.Length > 0)
                .OrderByDescending(v => Version.TryParse(v.Split('-')[0], out var parsed) ? parsed : new Version(0, 0))
                .FirstOrDefault() ?? string.Empty;
            return text.Length == 0 ? string.Empty : $" (Polhem {text})";
        }

        private static string FrameworkVersion { get; } = ReadFrameworkVersion();

        private static string ReadFrameworkVersion()
        {
            string version = typeof(TableUpgradeRecordStore).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
            int plus = version.IndexOf('+', StringComparison.Ordinal);
            if (plus >= 0) version = version[..plus];
            return version.Length > FrameworkVersionLength ? version[..FrameworkVersionLength] : version;
        }

        // A table name is an identifier, so the records match it regardless of case on every database.
        private static string Key(string tableName) => tableName.ToLowerInvariant();

        private void Upsert(string tableName, Func<DbCommandSpec> update, Func<DbCommandSpec> insert)
        {
            var dbAccess = CreateDbAccess();
            if (dbAccess.Execute(update()).RowsAffected > 0) return;
            try
            {
                dbAccess.Execute(insert());
            }
            catch (DbException) when (Exists(dbAccess, tableName))
            {
                // Another process inserted the record between the update and the insert; update the row it wrote.
                dbAccess.Execute(update());
            }
        }

        private bool Exists(DbAccess dbAccess, string tableName)
        {
            var count = dbAccess.ExecuteScalar(
                $"SELECT COUNT(*) FROM {RecordTableName} WHERE table_name = {{0}}", Key(tableName));
            return ValueUtilities.CInt(count ?? 0) > 0;
        }

        private void EnsureTable()
        {
            if (TableExists()) return;
            string sql = _dialect.CreateCreateTableCommandBuilder().GetCommandText(BuildRecordSchema(RecordTableName));
            try
            {
                CreateDbAccess().Execute(new DbCommandSpec(DbCommandKind.NonQuery, sql));
            }
            catch (DbException) when (ReadTableExists())
            {
                // Another process created the record table at the same time, which is as good.
                _tableExists = true;
                return;
            }
            _tableExists = true;
        }

        private bool TableExists() => _tableExists || (_tableExists = ReadTableExists());

        private bool ReadTableExists()
            => _dialect.CreateTableSchemaProvider(_databaseId, _connectionManager).GetTableSchema(RecordTableName) is not null;

        private DbAccess CreateDbAccess() => new(_databaseId, _connectionManager);

        /// <summary>
        /// Reads the summary of every record once per instance: the columns a fast compare needs, without the
        /// recorded schemas, read row by row so a database with many tables does not hold a table of results.
        /// </summary>
        private Dictionary<string, RecordSummary> LoadSummaries()
        {
            if (_summaries is not null) return _summaries;
            var summaries = new Dictionary<string, RecordSummary>(StringComparer.Ordinal);
            if (TableExists())
            {
                using var connection = _connectionManager.CreateConnection(_databaseId);
                connection.Open();
                var spec = new DbCommandSpec(DbCommandKind.DataTable,
                    $"SELECT table_name, status, format_version, schema_hash FROM {RecordTableName}");
                using var command = spec.CreateCommand(_databaseType, connection);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    summaries[ValueUtilities.CStr(reader.GetValue(0))] = new RecordSummary(
                        ParseStatus(ValueUtilities.CStr(reader.GetValue(1))),
                        ValueUtilities.CInt(reader.GetValue(2)),
                        ValueUtilities.CStr(reader.GetValue(3)));
                }
            }
            return _summaries = summaries;
        }

        /// <summary>
        /// Keeps the loaded summaries current with a write made through this instance; a null hash keeps the one
        /// already known, as the write did.
        /// </summary>
        private void Remember(string key, TableUpgradeStatus status, string? hash)
        {
            if (_summaries is null) return;
            string kept = hash ?? (_summaries.TryGetValue(key, out var old) ? old.SchemaHash : string.Empty);
            _summaries[key] = new RecordSummary(status, SchemaFormat.Current, kept);
        }

        private static TableUpgradeStatus ParseStatus(string text)
            => Enum.TryParse<TableUpgradeStatus>(text, out var status) ? status : TableUpgradeStatus.Upgrading;

        private readonly record struct RecordSummary(TableUpgradeStatus Status, int FormatVersion, string SchemaHash);
    }
}
