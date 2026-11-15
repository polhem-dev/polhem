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
            var value = CreateDbAccess().ExecuteScalar($"SELECT MAX(format_version) FROM {RecordTableName}");
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
                Status = Enum.TryParse<TableUpgradeStatus>(ValueUtilities.CStr(row["status"]), out var status)
                    ? status
                    : TableUpgradeStatus.Upgrading,
                Error = ValueUtilities.CStr(row["error"]),
                SchemaXml = ValueUtilities.CStr(row["schema_xml"]),
                SchemaHash = ValueUtilities.CStr(row["schema_hash"]),
                UpdateTime = ValueUtilities.CDateTime(row["sys_update_time"], DateTime.MinValue),
            };
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
            return TryWrite(
                $"UPDATE {RecordTableName} SET status = {{1}}, error = {{2}}, schema_xml = {{3}}, schema_hash = {{4}}, " +
                "sys_update_time = {5} WHERE table_name = {0}",
                Key(schema.TableName), nameof(TableUpgradeStatus.Succeeded), string.Empty, xml, ComputeHash(xml),
                DateTime.UtcNow);
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
            => TryWrite(
                $"UPDATE {RecordTableName} SET status = {{1}}, error = {{2}}, sys_update_time = {{3}} " +
                "WHERE table_name = {0}",
                Key(tableName), nameof(TableUpgradeStatus.Failed), error, DateTime.UtcNow);

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
                $"SELECT DISTINCT framework_version FROM {RecordTableName} WHERE format_version = {{0}}", formatVersion);
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
    }
}
