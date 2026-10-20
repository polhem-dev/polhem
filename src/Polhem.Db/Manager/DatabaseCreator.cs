using System.Data.Common;
using System.Globalization;
using Polhem.Definition.Database;

namespace Polhem.Db.Manager
{
    /// <summary>
    /// Creates the database a connection string names when it does not exist yet.
    /// </summary>
    /// <remarks>
    /// The connection string is the one the application itself connects with. The database it names
    /// is created through a second connection to the engine's administrative database, so the login
    /// in that connection string needs permission to create databases.
    /// <para>
    /// SQL Server, PostgreSQL and MySQL are created with DDL. A SQLite database is a file, created by
    /// opening it. Oracle is never created: its "database" is a schema that only a DBA can create.
    /// </para>
    /// </remarks>
    public static class DatabaseCreator
    {
        /// <summary>
        /// The longest database name <see cref="ValidateDatabaseName"/> accepts. It is PostgreSQL's
        /// limit, the shortest of the engines handled here.
        /// </summary>
        public const int MaxDatabaseNameLength = 63;

        private static readonly string[] s_databaseKeys = ["Database", "Initial Catalog"];
        private static readonly string[] s_sqliteFileKeys = ["Data Source", "DataSource", "Filename"];

        /// <summary>
        /// Returns the database a connection string names: the database name for a server engine, or
        /// the file path for SQLite.
        /// </summary>
        /// <param name="databaseType">The database engine.</param>
        /// <param name="provider">The engine's provider factory.</param>
        /// <param name="connectionString">The resolved connection string.</param>
        /// <returns>The database name, or an empty string when the connection string names none.</returns>
        public static string GetDatabaseName(DatabaseType databaseType, DbProviderFactory provider, string connectionString)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

            var builder = CreateBuilder(provider, connectionString);
            return databaseType switch
            {
                DatabaseType.SQLite => GetSqliteFilePath(builder),
                DatabaseType.Oracle => string.Empty,
                _ => ReadFirst(builder, s_databaseKeys),
            };
        }

        /// <summary>
        /// Creates the database the connection string names when it does not exist.
        /// </summary>
        /// <param name="databaseType">The database engine.</param>
        /// <param name="provider">The engine's provider factory.</param>
        /// <param name="connectionString">The resolved connection string.</param>
        /// <returns>Whether the database was created, already existed, or has nothing to create.</returns>
        /// <exception cref="InvalidOperationException">
        /// The connection string names no database, or the name fails <see cref="ValidateDatabaseName"/>.
        /// </exception>
        /// <exception cref="DbException">The engine refused the connection or the statement.</exception>
        /// <remarks>
        /// IMPORTANT: the name is concatenated into the DDL, and it has to be. A database name is an
        /// identifier, and no engine accepts a bind parameter in its place. What makes the
        /// concatenation safe is <see cref="ValidateDatabaseName"/>, called on the same value right
        /// before it. The existence probes compare a value rather than name an identifier, so they
        /// are parameterised.
        /// </remarks>
        public static DatabaseCreateResult EnsureExists(DatabaseType databaseType, DbProviderFactory provider, string connectionString)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

            switch (databaseType)
            {
                case DatabaseType.Oracle:
                    return DatabaseCreateResult.NotApplicable;
                case DatabaseType.SQLite:
                    return EnsureSqliteFile(provider, connectionString);
            }

            var builder = CreateBuilder(provider, connectionString);
            var databaseName = ReadFirst(builder, s_databaseKeys);
            if (databaseName.Length == 0)
            {
                throw new InvalidOperationException(
                    "The connection string names no database, so there is nothing to create. " +
                    "Set Database (or Initial Catalog) in the connection string, or DbName on the database item.");
            }
            ValidateDatabaseName(databaseName);

            using var connection = provider.CreateConnection()
                ?? throw new InvalidOperationException($"The {databaseType} provider factory returned no connection.");
            connection.ConnectionString = GetAdminConnectionString(databaseType, builder);
            connection.Open();

            if (DatabaseExists(databaseType, connection, databaseName))
                return DatabaseCreateResult.AlreadyExists;

            using var command = connection.CreateCommand();
            command.CommandText = databaseType switch
            {
                // Guarded again in case another process created it after the probe.
                DatabaseType.SQLServer => $"IF DB_ID(N'{databaseName}') IS NULL CREATE DATABASE [{databaseName}]",
                DatabaseType.MySQL => $"CREATE DATABASE IF NOT EXISTS `{databaseName}`",
                // PostgreSQL has no IF NOT EXISTS here and refuses the statement inside a transaction.
                DatabaseType.PostgreSQL => $"CREATE DATABASE \"{databaseName}\"",
                _ => throw new NotSupportedException($"Creating a database on {databaseType} is not supported."),
            };
            command.ExecuteNonQuery();
            return DatabaseCreateResult.Created;
        }

        /// <summary>
        /// Rejects a database name that cannot be safely concatenated into DDL.
        /// </summary>
        /// <param name="databaseName">The name to check.</param>
        /// <exception cref="InvalidOperationException">
        /// The name is empty, longer than <see cref="MaxDatabaseNameLength"/>, or contains a character
        /// other than an ASCII letter, digit or underscore.
        /// </exception>
        public static void ValidateDatabaseName(string databaseName)
        {
            ArgumentNullException.ThrowIfNull(databaseName);

            if (databaseName.Length is 0 or > MaxDatabaseNameLength)
            {
                throw new InvalidOperationException(
                    $"Refusing to create a database named '{databaseName}': the name must be between 1 and " +
                    $"{MaxDatabaseNameLength} characters long.");
            }

            foreach (var character in databaseName)
            {
                if (!char.IsAsciiLetterOrDigit(character) && character != '_')
                {
                    throw new InvalidOperationException(
                        $"Refusing to create a database named '{databaseName}': only ASCII letters, digits and " +
                        "underscore are allowed, because a database name cannot be passed as a parameter.");
                }
            }
        }

        private static DbConnectionStringBuilder CreateBuilder(DbProviderFactory provider, string connectionString)
        {
            var builder = provider.CreateConnectionStringBuilder() ?? [];
            builder.ConnectionString = connectionString;
            return builder;
        }

        private static string ReadFirst(DbConnectionStringBuilder builder, string[] keys)
        {
            foreach (var key in keys)
            {
                if (builder.TryGetValue(key, out var value))
                {
                    var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                    if (text.Length > 0) return text;
                }
            }
            return string.Empty;
        }

        private static string GetAdminConnectionString(DatabaseType databaseType, DbConnectionStringBuilder builder)
        {
            foreach (var key in s_databaseKeys)
            {
                if (builder.ContainsKey(key)) builder.Remove(key);
            }

            // MySQL connects without a default database; the other two need their system database.
            switch (databaseType)
            {
                case DatabaseType.SQLServer:
                    builder["Initial Catalog"] = "master";
                    break;
                case DatabaseType.PostgreSQL:
                    builder["Database"] = "postgres";
                    break;
            }
            return builder.ConnectionString;
        }

        private static bool DatabaseExists(DatabaseType databaseType, DbConnection connection, string databaseName)
        {
            using var probe = connection.CreateCommand();
            var parameterName = databaseType.GetParameterName("name");
            probe.CommandText = databaseType switch
            {
                DatabaseType.SQLServer => $"SELECT DB_ID({parameterName})",
                DatabaseType.PostgreSQL => $"SELECT 1 FROM pg_database WHERE datname = {parameterName}",
                DatabaseType.MySQL => $"SELECT 1 FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = {parameterName}",
                _ => throw new NotSupportedException($"Creating a database on {databaseType} is not supported."),
            };
            var parameter = probe.CreateParameter();
            parameter.ParameterName = parameterName;
            parameter.Value = databaseName;
            probe.Parameters.Add(parameter);

            var result = probe.ExecuteScalar();
            return result is not null && result is not DBNull;
        }

        private static DatabaseCreateResult EnsureSqliteFile(DbProviderFactory provider, string connectionString)
        {
            var builder = CreateBuilder(provider, connectionString);
            var path = GetSqliteFilePath(builder);
            if (path.Length == 0) return DatabaseCreateResult.NotApplicable;

            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath)) return DatabaseCreateResult.AlreadyExists;

            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            // Opening the connection is what creates the file, under the provider's default
            // read-write-create mode.
            using var connection = provider.CreateConnection()
                ?? throw new InvalidOperationException("The SQLite provider factory returned no connection.");
            connection.ConnectionString = connectionString;
            connection.Open();
            return DatabaseCreateResult.Created;
        }

        /// <summary>
        /// Returns the file a SQLite connection string opens, or an empty string for an in-memory
        /// database or a path this method cannot resolve.
        /// </summary>
        private static string GetSqliteFilePath(DbConnectionStringBuilder builder)
        {
            if (builder.TryGetValue("Mode", out var mode)
                && string.Equals(Convert.ToString(mode, CultureInfo.InvariantCulture), "Memory", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var source = ReadFirst(builder, s_sqliteFileKeys);
            if (source.Length == 0 || string.Equals(source, ":memory:", StringComparison.Ordinal))
                return string.Empty;

            // The provider resolves `|DataDirectory|` itself, so this method cannot tell which file it
            // would open. Reporting nothing to create is safer than creating a folder of that name.
            if (source.Contains("|DataDirectory|", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            if (source.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                var query = source.IndexOf('?', StringComparison.Ordinal);
                if (query >= 0
                    && source.IndexOf("mode=memory", query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return string.Empty;
                }
                source = query >= 0 ? source[5..query] : source[5..];
            }
            return source;
        }
    }
}
