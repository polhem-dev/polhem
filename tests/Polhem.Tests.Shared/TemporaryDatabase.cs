using System.Data.Common;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using Polhem.Db.Manager;
using Polhem.Definition.Database;

namespace Polhem.Tests.Shared
{
    /// <summary>
    /// Drops a database a test created on a server engine (SQL Server, PostgreSQL or MySQL).
    /// </summary>
    /// <remarks>
    /// Pooled connections into the database would keep SQL Server and PostgreSQL from dropping it,
    /// so the pools are cleared and the drop forces the remaining sessions out.
    /// </remarks>
    public static class TemporaryDatabase
    {
        /// <summary>
        /// Returns the provider factory a test uses for a server engine.
        /// </summary>
        /// <param name="databaseType">The engine.</param>
        public static DbProviderFactory GetServerFactory(DatabaseType databaseType) => databaseType switch
        {
            DatabaseType.SQLServer => SqlClientFactory.Instance,
            DatabaseType.PostgreSQL => NpgsqlFactory.Instance,
            DatabaseType.MySQL => MySqlConnectorFactory.Instance,
            _ => throw new NotSupportedException($"{databaseType} has no server-level database to drop."),
        };

        /// <summary>
        /// Drops the database when it exists.
        /// </summary>
        /// <param name="databaseType">The engine.</param>
        /// <param name="connectionStringTemplate">The connection string template with a <c>{@DbName}</c> placeholder.</param>
        /// <param name="databaseName">The database to drop; created by the test, so it is a safe identifier.</param>
        public static void Drop(DatabaseType databaseType, string connectionStringTemplate, string databaseName)
        {
            DatabaseCreator.ValidateDatabaseName(databaseName);
            SqlConnection.ClearAllPools();
            NpgsqlConnection.ClearAllPools();
            MySqlConnection.ClearAllPools();

            using var connection = GetServerFactory(databaseType).CreateConnection()!;
            connection.ConnectionString = GetAdminConnectionString(databaseType, connectionStringTemplate);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = databaseType switch
            {
                DatabaseType.SQLServer =>
                    $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN " +
                    $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}] END",
                DatabaseType.PostgreSQL => $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)",
                _ => $"DROP DATABASE IF EXISTS `{databaseName}`",
            };
            command.ExecuteNonQuery();
        }

        // MySQL connects with no database at all: a login granted only some databases cannot open `mysql`.
        private static string GetAdminConnectionString(DatabaseType databaseType, string connectionStringTemplate)
        {
            if (databaseType != DatabaseType.MySQL)
            {
                var admin = databaseType == DatabaseType.SQLServer ? "master" : "postgres";
                return ConnectionStringTemplate.Resolve(connectionStringTemplate, admin, string.Empty, string.Empty);
            }
            var builder = new MySqlConnectionStringBuilder(
                ConnectionStringTemplate.Resolve(connectionStringTemplate, "placeholder", string.Empty, string.Empty));
            builder.Remove("Database");
            return builder.ConnectionString;
        }
    }
}
