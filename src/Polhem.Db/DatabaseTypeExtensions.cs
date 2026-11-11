using Polhem.Db.Providers.MySql;
using Polhem.Db.Providers.Oracle;
using Polhem.Db.Providers.PostgreSql;
using Polhem.Db.Providers.Sqlite;
using Polhem.Db.Providers.SqlServer;
using Polhem.Definition.Database;

namespace Polhem.Db
{
    /// <summary>
    /// Extension methods for <see cref="DatabaseType"/> covering SQL dialect specifics
    /// such as parameter prefix and identifier quoting.
    /// </summary>
    public static class DatabaseTypeExtensions
    {
        /// <summary>
        /// Dictionary mapping database types to their parameter name prefix characters.
        /// </summary>
        private static readonly Dictionary<DatabaseType, string> s_dbParameterPrefixes = new Dictionary<DatabaseType, string>
        {
            { DatabaseType.SQLServer, "@" },
            { DatabaseType.PostgreSQL, "@" },
            { DatabaseType.MySQL, "@" },
            { DatabaseType.Oracle, ":" },
            { DatabaseType.SQLite, "@" }
        };

        /// <summary>
        /// Maps each database type to its dialect's identifier quoting, which the DDL builders use as well, so a query
        /// and the table it reads quote a name the same way.
        /// </summary>
        private static readonly Dictionary<DatabaseType, Func<string, string>> s_quoteIdentifiers = new Dictionary<DatabaseType, Func<string, string>>
        {
            { DatabaseType.SQLServer, SqlSchemaSyntax.QuoteName },
            { DatabaseType.PostgreSQL, PgSchemaSyntax.QuoteName },
            { DatabaseType.MySQL, MySqlSchemaSyntax.QuoteName },
            { DatabaseType.Oracle, OracleSchemaSyntax.QuoteName },
            { DatabaseType.SQLite, SqliteSchemaSyntax.QuoteName }
        };

        /// <summary>
        /// Gets the parameter name prefix character for the specified database type.
        /// </summary>
        /// <param name="databaseType">The database type.</param>
        /// <returns>The parameter prefix character.</returns>
        /// <exception cref="NotSupportedException">Thrown when the database type is not supported.</exception>
        public static string GetParameterPrefix(this DatabaseType databaseType)
        {
            return s_dbParameterPrefixes.TryGetValue(databaseType, out var prefix)
                ? prefix
                : throw new NotSupportedException($"Unsupported database type: {databaseType}.");
        }

        /// <summary>
        /// Gets the full parameter name including its prefix character.
        /// </summary>
        /// <param name="databaseType">The database type.</param>
        /// <param name="name">The parameter name without the prefix character.</param>
        public static string GetParameterName(this DatabaseType databaseType, string name)
        {
            string parameterPrefix = databaseType.GetParameterPrefix();
            return string.IsNullOrEmpty(parameterPrefix) ? name : parameterPrefix + name;
        }

        /// <summary>
        /// Returns the properly quoted identifier string for the specified database type.
        /// </summary>
        /// <param name="databaseType">The database type.</param>
        /// <param name="identifier">The identifier name to quote.</param>
        /// <returns>The quoted identifier.</returns>
        /// <exception cref="NotSupportedException">Thrown when the database type is not supported.</exception>
        public static string QuoteIdentifier(this DatabaseType databaseType, string identifier)
        {
            if (s_quoteIdentifiers.TryGetValue(databaseType, out var func))
                return func(identifier);
            throw new NotSupportedException($"Unsupported database type: {databaseType}.");
        }
    }
}
