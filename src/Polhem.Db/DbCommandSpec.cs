using Polhem.Core;
using Polhem.Core.Collections;
using Polhem.Core.Data;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Polhem.Definition.Database;

namespace Polhem.Db
{
    /// <summary>
    /// Describes a database command as an intermediary for <see cref="DbCommand"/>.
    /// </summary>
    public sealed class DbCommandSpec : CollectionItem
    {
        private const int DefaultTimeout = 30;  // Default timeout in seconds
        private const string BindByNamePropertyName = "BindByName";
        // Null is a cached answer too: a provider whose command type has no BindByName is looked
        // up once, not once per command.
        private static readonly ConcurrentDictionary<Type, PropertyInfo?> s_bindByNameCache = new();
        // Pre-compiled placeholder regex: {key}; supports {{key}} as an escape (outputs {key})
        private static readonly Regex s_placeholderRegex =
            new Regex(@"\{(?<key>[^\}]+)\}|\{\{(?<escaped>[^\}]+)\}\}", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        /// <summary>
        /// Initializes a new empty instance of <see cref="DbCommandSpec"/>.
        /// </summary>
        public DbCommandSpec()
        { }

        /// <summary>
        /// Initializes a new instance of <see cref="DbCommandSpec"/> using positional parameters.
        /// </summary>
        /// <param name="kind">The execution kind of the database command.</param>
        /// <param name="commandText">The SQL statement to execute; use {0}, {1} positional placeholders.</param>
        /// <param name="values">Positional parameter values corresponding to {0}, {1}, ...</param>
        public DbCommandSpec(DbCommandKind kind, string commandText, params object[] values)
        {
            if (string.IsNullOrWhiteSpace(commandText))
                throw new ArgumentNullException(nameof(commandText), "Command text cannot be null or empty.");

            Kind = kind;
            CommandText = commandText;

            if (values != null && values.Length > 0)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    Parameters.Add("p" + i, values[i]);
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of <see cref="DbCommandSpec"/> using named parameters.
        /// </summary>
        /// <param name="kind">The execution kind of the database command.</param>
        /// <param name="commandText">The SQL statement to execute; use {Name} named placeholders.</param>
        /// <param name="parameters">A dictionary of named parameter values.</param>
        public DbCommandSpec(DbCommandKind kind, string commandText, IDictionary<string, object>? parameters)
        {
            if (string.IsNullOrWhiteSpace(commandText))
                throw new ArgumentNullException(nameof(commandText), "Command text cannot be null or empty.");

            Kind = kind;
            CommandText = commandText;

            if (parameters != null)
            {
                foreach (var kv in parameters)
                {
                    Parameters.Add(kv.Key, kv.Value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the execution kind of the database command.
        /// </summary>
        public DbCommandKind Kind { get; set; } = DbCommandKind.NonQuery;

        /// <summary>
        /// Gets or sets the SQL statement to execute.
        /// </summary>
        public string CommandText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the command type; defaults to <see cref="CommandType.Text"/>.
        /// </summary>
        public CommandType CommandType { get; set; } = CommandType.Text;

        /// <summary>
        /// Gets or sets the command execution timeout in seconds. Stored as-is;
        /// the effective value applied to <see cref="DbCommand.CommandTimeout"/> is
        /// resolved by <see cref="DbAccess"/> at execution time (non-positive →
        /// default 30 sec; values exceeding the per-app cap are clamped).
        /// </summary>
        public int CommandTimeout { get; set; } = DefaultTimeout;

        /// <summary>
        /// Gets the parameter specifications for this command.
        /// </summary>
        public DbParameterSpecCollection Parameters { get; } = [];

        /// <summary>
        /// Gets or sets the columns of the result set that hold a calendar day rather than an instant.
        /// </summary>
        /// <remarks>
        /// ADO.NET reports a `date` column as `System.DateTime`, which loses the distinction the
        /// definition layer makes between <see cref="FieldDbType.Date"/> and
        /// <see cref="FieldDbType.DateTime"/>. Queries the framework generates from a schema are marked
        /// from that schema; a hand-written query has no schema to consult, so declare its calendar-day
        /// columns here and <see cref="DbAccess"/> marks them on the returned table.
        /// <para>
        /// Applies only to <see cref="DbCommandKind.DataTable"/>. Setting it on any other kind is
        /// rejected at execution time rather than ignored, because a declaration that silently does
        /// nothing is the failure this option exists to prevent. Column names are matched
        /// case-insensitively, and a name that matches no column raises an error.
        /// </para>
        /// </remarks>
        public List<string> DateColumns { get; } = [];

        /// <summary>
        /// Gets the CLR types to build result columns with, keyed by column name, in place of the types
        /// the provider reports.
        /// </summary>
        /// <remarks>
        /// SQLite has no column types, only per-value storage classes, and Microsoft.Data.Sqlite types
        /// each result column after the first row. A `NUMERIC` column whose first row holds a whole
        /// number becomes an `Int64` column, and every later `100.5` is then read as `100` without an
        /// error. Converting after the read cannot recover it. A column created with its declared type
        /// before the rows are read converts each value into that type as it loads, so no row is narrowed
        /// to the type of the first. A value keeps the precision of the type the provider read it as (a
        /// SQLite <c>REAL</c> arrives as a <see cref="double"/>), and a value that does not fit the declared
        /// type throws.
        /// <para>
        /// Applies only to <see cref="DbCommandKind.DataTable"/>, and setting it on any other kind is
        /// rejected at execution time, as with <see cref="DateColumns"/>. Names are matched
        /// case-insensitively. Unlike <see cref="DateColumns"/>, a name absent from the result set is
        /// ignored: a caller declaring from a schema declares every field, and a query may select only
        /// some of them. Declared columns keep their position in the result set.
        /// </para>
        /// <para>
        /// When it is set, the table is built from the result set alone: a column name that appears twice
        /// throws instead of being renamed, and no key, nullability or length information is read into
        /// the columns, which <c>DataTable.Load</c> on the asynchronous path would otherwise add.
        /// </para>
        /// </remarks>
        public Dictionary<string, Type> ColumnTypes { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates a <see cref="DbCommand"/> instance configured with the current <see cref="DbCommandSpec"/> settings.
        /// </summary>
        /// <param name="databaseType">The database type.</param>
        /// <param name="connection">The database connection used to create and bind the command.</param>
        public DbCommand CreateCommand(DatabaseType databaseType, DbConnection connection)
            => CreateCommand(databaseType, connection, boundCompanyId: null);

        /// <summary>
        /// The name of the parameter <see cref="CommandTextVariable.CompanyId"/> becomes. It starts with a
        /// letter because Oracle does not accept a bind name that starts with an underscore.
        /// </summary>
        internal const string CompanyParameterName = "polhem_company_id";

        /// <summary>
        /// Creates the command for a <see cref="DbAccess"/> that may be bound to a company.
        /// </summary>
        /// <param name="databaseType">The database type.</param>
        /// <param name="connection">The database connection used to create and bind the command.</param>
        /// <param name="boundCompanyId">The company the <see cref="DbAccess"/> is bound to, or <c>null</c> when it is not.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a bound command does not reference <see cref="CommandTextVariable.CompanyId"/>, or an
        /// unbound one does.
        /// </exception>
        internal DbCommand CreateCommand(DatabaseType databaseType, DbConnection connection, string? boundCompanyId)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection), "Connection cannot be null.");
            if (string.IsNullOrWhiteSpace(CommandText))
                throw new InvalidOperationException("CommandText cannot be null or empty.");
            // Checked here rather than where the marks are applied, because a kind that returns no
            // table never reaches that code — the declaration would then be silently inert, which is
            // the failure `DateColumns` exists to prevent. Every execution path builds its command
            // through this method, so this is the one gate all of them pass.
            if (DateColumns.Count > 0 && Kind != DbCommandKind.DataTable)
            {
                throw new InvalidOperationException(
                    $"{nameof(DateColumns)} applies only to {nameof(DbCommandKind.DataTable)} commands; " +
                    $"this command is {Kind}.");
            }
            if (ColumnTypes.Count > 0 && Kind != DbCommandKind.DataTable)
            {
                throw new InvalidOperationException(
                    $"{nameof(ColumnTypes)} applies only to {nameof(DbCommandKind.DataTable)} commands; " +
                    $"this command is {Kind}.");
            }

            bool referencesCompany = RequireCompanyBinding(boundCompanyId);

            string parameterPrefix = databaseType.GetParameterPrefix();
            var cmd = connection.CreateCommand();
            // Pass through for StoredProcedure; skip parameter resolution
            cmd.CommandText = (CommandType == CommandType.StoredProcedure)
                ? CommandText
                : ResolveParameters(parameterPrefix);
            cmd.CommandType = CommandType;
            cmd.CommandTimeout = CommandTimeout;

            foreach (var spec in Parameters)
            {
                cmd.Parameters.Add(CreateParameter(cmd, databaseType, parameterPrefix, spec));
            }
            if (referencesCompany)
            {
                cmd.Parameters.Add(CreateParameter(cmd, databaseType, parameterPrefix,
                    new DbParameterSpec(CompanyParameterName, boundCompanyId!) { DbType = DbType.String }));
            }

            ApplyOracleBindByName(databaseType, cmd);

            return cmd;
        }

        // Oracle.ManagedDataAccess binds parameters by their position in the collection unless
        // `OracleCommand.BindByName` is set, so `:p1` resolves to the first parameter added rather
        // than to the parameter of that name. Every other provider this framework supports binds
        // by name, which is what the `{0}` / `{Name}` placeholder API promises: a caller may write
        // the placeholders in any order and may use one twice. Under positional binding those two
        // shapes silently bind the wrong value to the wrong column — the failure that broke every
        // FormSchema-driven request on Oracle, because sign-in's `UPDATE st_session` sets two
        // columns before matching on the access token and so sent a DateTime where a RAW(16) was
        // expected (ORA-00932).
        //
        // Set here rather than worked around per statement: the alternative is every author of
        // every SQL string remembering an Oracle-only ordering rule that nothing checks. It is
        // applied to text commands only — for CommandType.StoredProcedure, BindByName would match
        // parameter names against the procedure's declared argument names, a different contract
        // than the positional one callers of that path have today.
        //
        // Reflection because Polhem.Db takes no reference to any ADO.NET driver: the provider factory
        // is registered by the host, and adding Oracle.ManagedDataAccess here would put an Oracle
        // dependency in every consumer's package graph. The property lookup is cached per command
        // type, so the cost after the first command of a run is a dictionary read.
        private static void ApplyOracleBindByName(DatabaseType databaseType, DbCommand cmd)
        {
            if (databaseType != DatabaseType.Oracle || cmd.CommandType != CommandType.Text) return;

            var property = s_bindByNameCache.GetOrAdd(cmd.GetType(), static type =>
                type.GetProperty(BindByNamePropertyName, BindingFlags.Public | BindingFlags.Instance));

            if (property is { CanWrite: true } && property.PropertyType == typeof(bool))
                property.SetValue(cmd, true);
        }

        /// <summary>
        /// Builds a single provider parameter from its specification.
        /// </summary>
        /// <param name="cmd">The command the parameter is created from.</param>
        /// <param name="databaseType">The database type, which decides the value and type normalizations.</param>
        /// <param name="parameterPrefix">The provider's parameter prefix, prepended when the name lacks it.</param>
        /// <param name="spec">The parameter specification.</param>
        private static DbParameter CreateParameter(DbCommand cmd, DatabaseType databaseType,
            string parameterPrefix, DbParameterSpec spec)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = (string.IsNullOrEmpty(parameterPrefix) || spec.Name.StartsWith(parameterPrefix, StringComparison.Ordinal))
                ? spec.Name
                : parameterPrefix + spec.Name;
            var value = NormalizeParameterValue(databaseType, spec.Value);
            p.Value = value ?? DBNull.Value;
            // An inline `{0}` parameter carries no DbType, leaving the provider to infer one from
            // the CLR value — and Npgsql infers `timestamptz` for DateTime, which rejects
            // Kind=Unspecified outright and would silently apply the server's zone to a Utc one.
            // Naming DbType.DateTime pins it to the naive column type this framework stores in
            // (ADR-032 D1), and routes SQL Server through the datetime2 upgrade below so an
            // inline value keeps the same sub-millisecond precision a schema-driven one does.
            var dbType = spec.DbType ?? (value is DateTime ? DbType.DateTime : null);
            if (dbType.HasValue) p.DbType = NormalizeDbType(databaseType, dbType.Value);
            if (spec.Size.HasValue && spec.Size.Value > 0) p.Size = spec.Size.Value;
            p.IsNullable = spec.IsNullable;
            if (!string.IsNullOrEmpty(spec.SourceColumn))
                p.SourceColumn = spec.SourceColumn;
            p.SourceVersion = spec.SourceVersion;
            return p;
        }

        /// <summary>
        /// Normalizes a parameter value for the target provider. Oracle.ManagedDataAccess does
        /// not accept a <see cref="Guid"/> instance as the value of a <c>RAW(16)</c> parameter
        /// (the framework's <see cref="Polhem.Core.Data.FieldDbType.Guid"/> mapping), and throws
        /// <c>ArgumentException : Value does not fall within the expected range</c>; convert to
        /// <c>byte[]</c> via <see cref="Guid.ToByteArray()"/> for Oracle only. A <see cref="DateOnly"/> becomes the
        /// <see cref="DateTime"/> at midnight of that day on SQLite and Oracle, and a PostgreSQL
        /// <see cref="DateTime"/> loses its <see cref="DateTimeKind"/>. Other values pass through unchanged.
        /// </summary>
        /// <remarks>
        /// A client filters a date column with a <see cref="DateOnly"/> (ADR-032 D4), while a saved date is a
        /// <see cref="DateTime"/>. SQLite stores that as text, <c>2026-02-02 00:00:00</c>, and Microsoft.Data.Sqlite
        /// binds a <see cref="DateOnly"/> as <c>2026-02-02</c>, so the two never compare equal and a range leaves out
        /// its last day. Oracle.ManagedDataAccess refuses a <see cref="DateOnly"/> value outright. SQL Server, PostgreSQL
        /// and MySQL bind it as a date and compare it with the stored day. <c>GetListFilterValueDbTests</c> runs the
        /// filter against each database.
        /// </remarks>
        internal static object? NormalizeParameterValue(DatabaseType databaseType, object? value)
        {
            if (databaseType == DatabaseType.Oracle && value is Guid guid)
                return guid.ToByteArray();
            if (databaseType is DatabaseType.SQLite or DatabaseType.Oracle && value is DateOnly day)
                return day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            // Npgsql validates DateTimeKind against the target PostgreSQL type and refuses both
            // mismatches: `timestamptz` demands Utc, `timestamp` demands anything but. Since every
            // DateTime parameter is sent as `timestamp` (see NormalizeDbType) and its value is
            // already UTC by ADR-032 D1, the Kind carries no information worth defending — D6 says
            // as much — so it is cleared here rather than making every caller remember to.
            if (databaseType == DatabaseType.PostgreSQL && value is DateTime dateTime)
                return DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
            return value;
        }

        /// <summary>
        /// Normalizes a parameter <see cref="DbType"/> for the target provider. Two provider-specific
        /// rewrites are applied:
        /// <list type="bullet">
        /// <item>Oracle.ManagedDataAccess rejects <c>DbType.Guid</c> on
        /// <see cref="System.Data.Common.DbParameter.DbType"/> assignment (Oracle has no native UUID type —
        /// the framework maps <see cref="Polhem.Core.Data.FieldDbType.Guid"/> to <c>RAW(16)</c>); rewrite to
        /// <see cref="DbType.Binary"/> for Oracle. This pairs with <see cref="NormalizeParameterValue"/>,
        /// which converts the value side from <see cref="Guid"/> to <c>byte[]</c>.</item>
        /// <item>SQL Server upgrades <c>DbType.DateTime</c> to <c>DbType.DateTime2</c> so the full .NET
        /// <see cref="DateTime"/> range and 100 ns precision survive the parameter layer (plain
        /// <c>DbType.DateTime</c> rounds to ~3.33 ms and rejects pre-1753 values before transmission).
        /// Applied SQL Server-only so PostgreSQL/Oracle DateTime handling is unaffected.</item>
        /// </list>
        /// </summary>
        internal static DbType NormalizeDbType(DatabaseType databaseType, DbType dbType)
        {
            if (databaseType == DatabaseType.Oracle && dbType == DbType.Guid)
                return DbType.Binary;
            if (databaseType == DatabaseType.SQLServer && dbType == DbType.DateTime)
                return DbType.DateTime2;
            // Npgsql maps DbType.DateTime to `timestamptz`, which refuses a Kind=Unspecified value
            // outright and, for a Kind=Utc one, lets PostgreSQL re-express it in the server's zone on
            // the way into the `timestamp` column the schema actually declares. Either way the
            // database would be deciding the zone — the one thing ADR-032 D1 keeps it out of.
            // DbType.DateTime2 is Npgsql's spelling of `timestamp without time zone`.
            if (databaseType == DatabaseType.PostgreSQL && dbType == DbType.DateTime)
                return DbType.DateTime2;
            return dbType;
        }

        /// <summary>
        /// Resolves {0} or {Name} placeholders in CommandText and converts them to the database parameter format.
        /// Use {{Name}} to output a literal {Name}.
        /// </summary>
        /// <param name="parameterPrefix">The database parameter prefix character, e.g., @ or :.</param>
        /// <returns>The resolved SQL command text.</returns>
        private string ResolveParameters(string parameterPrefix)
        {
            if (string.IsNullOrWhiteSpace(CommandText))
                throw new InvalidOperationException("Failed to execute SQL command: Command text is empty.");

            // The company placeholder becomes a parameter name directly, so the placeholder pass below,
            // which would look for a parameter called "@CompanyId", never sees it.
            string commandText = ExpandParametersVariable(CommandText)
                .Replace(CommandTextVariable.CompanyId, parameterPrefix + CompanyParameterName, StringComparison.Ordinal);

            return s_placeholderRegex.Replace(commandText, match =>
            {
                var escaped = match.Groups["escaped"];
                if (escaped.Success)
                    return "{" + escaped.Value + "}";

                var key = match.Groups["key"].Value;
                var name = int.TryParse(key, out var index)
                    ? ResolveNumericKey(index)
                    : ResolveNamedKey(key);

                return string.IsNullOrEmpty(parameterPrefix) ? name : parameterPrefix + name;
            });
        }

        /// <summary>
        /// Checks that the command and the company binding of its <see cref="DbAccess"/> agree, and
        /// returns whether the command references <see cref="CommandTextVariable.CompanyId"/>.
        /// </summary>
        private bool RequireCompanyBinding(string? boundCompanyId)
        {
            bool referencesCompany = CommandType != CommandType.StoredProcedure
                && CommandText.Contains(CommandTextVariable.CompanyId, StringComparison.Ordinal);
            if (boundCompanyId is not null && !referencesCompany)
            {
                throw new InvalidOperationException(
                    "This database access is bound to a company, but the command does not reference " +
                    $"{CommandTextVariable.CompanyId}. Confine the statement to the company with it.");
            }
            if (referencesCompany && Parameters.Any(p => IsCompanyParameterName(p.Name)))
            {
                throw new InvalidOperationException(
                    $"The parameter name '{CompanyParameterName}' is reserved for {CommandTextVariable.CompanyId}.");
            }
            if (boundCompanyId is null && referencesCompany)
            {
                throw new InvalidOperationException(
                    $"The command references {CommandTextVariable.CompanyId}, but the database access is not " +
                    "bound to a company.");
            }
            return referencesCompany;
        }

        private static bool IsCompanyParameterName(string name)
            => string.Equals(name.TrimStart('@', ':', '?'), CompanyParameterName, StringComparison.OrdinalIgnoreCase);

        private string ExpandParametersVariable(string commandText)
        {
            if (!StringUtilities.Contains(commandText, CommandTextVariable.Parameters))
                return commandText;

            var sb = new StringBuilder();
            for (int i = 0; i < Parameters.Count; i++)
            {
                if (sb.Length > 0) sb.Append(',');
                sb.Append('{').Append(i).Append('}');
            }
            return StringUtilities.Replace(commandText, CommandTextVariable.Parameters, sb.ToString());
        }

        private string ResolveNumericKey(int index)
        {
            if (index < 0 || index >= Parameters.Count)
                throw new InvalidOperationException(
                    $"Failed to resolve SQL parameter: Index {{{index}}} not found in Parameters collection.");

            var name = Parameters[index].Name;
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException(
                    $"Failed to resolve SQL parameter: Parameter at index {index} has empty name.");

            return name;
        }

        private string ResolveNamedKey(string key)
        {
            var param = Parameters.GetOrDefault(key)
                ?? throw new InvalidOperationException(
                    $"Failed to resolve SQL parameter: Name {{{key}}} not found in Parameters collection.");

            if (string.IsNullOrWhiteSpace(param.Name))
                throw new InvalidOperationException(
                    $"Failed to resolve SQL parameter: Parameter '{key}' has empty name.");

            return param.Name;
        }

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override string ToString()
        {
            return CommandText;
        }
    }
}
