using System.ComponentModel;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using Polhem.Db.Manager;
using Polhem.Db.Providers.Sqlite;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    public class DatabaseCreatorTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("a;b")]
        [InlineData("a b")]
        [InlineData("a'b")]
        [InlineData("a]b")]
        [InlineData("a\"b")]
        [InlineData("a-b")]
        [InlineData("名稱")]
        [DisplayName("ValidateDatabaseName rejects a name with characters that cannot be concatenated into DDL")]
        public void ValidateDatabaseName_UnsafeName_Throws(string name)
        {
            Assert.Throws<InvalidOperationException>(() => DatabaseCreator.ValidateDatabaseName(name));
        }

        [Fact]
        [DisplayName("ValidateDatabaseName rejects a name longer than MaxDatabaseNameLength")]
        public void ValidateDatabaseName_TooLong_Throws()
        {
            var name = new string('a', DatabaseCreator.MaxDatabaseNameLength + 1);
            Assert.Throws<InvalidOperationException>(() => DatabaseCreator.ValidateDatabaseName(name));
        }

        [Theory]
        [InlineData("common")]
        [InlineData("Polhem_Company_01")]
        [InlineData("_log")]
        [DisplayName("ValidateDatabaseName accepts ASCII letters, digits and underscore")]
        public void ValidateDatabaseName_SafeName_DoesNotThrow(string name)
        {
            var exception = Record.Exception(() => DatabaseCreator.ValidateDatabaseName(name));
            Assert.Null(exception);
        }

        [Theory]
        [InlineData(DatabaseType.SQLServer, "Data Source=localhost;Initial Catalog=northwind_common;User ID=sa", "northwind_common")]
        [InlineData(DatabaseType.SQLServer, "Server=localhost;Database=northwind_log", "northwind_log")]
        [InlineData(DatabaseType.PostgreSQL, "Host=localhost;Database=northwind_company;Username=u", "northwind_company")]
        [InlineData(DatabaseType.MySQL, "Server=localhost;Database=northwind_common;User=u", "northwind_common")]
        [InlineData(DatabaseType.SQLite, "Data Source=data/northwind.db", "data/northwind.db")]
        [InlineData(DatabaseType.SQLite, "Data Source=file:data/northwind.db?cache=shared", "data/northwind.db")]
        [InlineData(DatabaseType.SQLite, "Data Source=:memory:", "")]
        [InlineData(DatabaseType.SQLite, "Data Source=file:x?mode=memory&cache=shared", "")]
        [InlineData(DatabaseType.SQLite, "Data Source=x;Mode=Memory", "")]
        [InlineData(DatabaseType.Oracle, "Data Source=localhost:1521/FREEPDB1;User Id=u", "")]
        [DisplayName("GetDatabaseName reads the database or file a connection string names")]
        public void GetDatabaseName_ConnectionString_ReturnsName(DatabaseType databaseType, string connectionString, string expected)
        {
            Assert.Equal(expected, DatabaseCreator.GetDatabaseName(databaseType, GetFactory(databaseType), connectionString));
        }

        [Theory]
        [InlineData(DatabaseType.Oracle, "Data Source=localhost:1521/FREEPDB1;User Id=u")]
        [InlineData(DatabaseType.SQLite, "Data Source=:memory:")]
        [InlineData(DatabaseType.SQLite, "Data Source=file:x?mode=memory&cache=shared")]
        [DisplayName("EnsureExists reports NotApplicable for Oracle and in-memory SQLite without connecting")]
        public void EnsureExists_NothingToCreate_ReturnsNotApplicable(DatabaseType databaseType, string connectionString)
        {
            Assert.Equal(DatabaseCreateResult.NotApplicable,
                DatabaseCreator.EnsureExists(databaseType, GetFactory(databaseType), connectionString));
        }

        [Fact]
        [DisplayName("EnsureExists refuses a connection string that names no database")]
        public void EnsureExists_NoDatabaseInConnectionString_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                DatabaseCreator.EnsureExists(DatabaseType.SQLServer, SqlClientFactory.Instance, "Data Source=localhost;User ID=sa"));
        }

        [Fact]
        [DisplayName("EnsureExists refuses an unsafe database name before connecting")]
        public void EnsureExists_UnsafeName_ThrowsBeforeConnecting()
        {
            // The server does not exist; reaching it would fail with a DbException instead.
            Assert.Throws<InvalidOperationException>(() =>
                DatabaseCreator.EnsureExists(DatabaseType.PostgreSQL, NpgsqlFactory.Instance,
                    "Host=invalid.invalid;Database='x]; DROP DATABASE y; --';Username=u"));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("EnsureExists creates a SQLite file and its missing directory, then reports AlreadyExists")]
        public void EnsureExists_SqliteFile_CreatesThenExists()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"polhem-create-{Guid.NewGuid():N}");
            var file = Path.Combine(directory, "nested", "app.db");
            var connectionString = $"Data Source={file};Pooling=False";
            var factory = GetFactory(DatabaseType.SQLite);
            try
            {
                Assert.Equal(DatabaseCreateResult.Created, DatabaseCreator.EnsureExists(DatabaseType.SQLite, factory, connectionString));
                Assert.True(File.Exists(file));
                Assert.Equal(DatabaseCreateResult.AlreadyExists, DatabaseCreator.EnsureExists(DatabaseType.SQLite, factory, connectionString));
            }
            finally
            {
                try { Directory.Delete(directory, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("EnsureExists creates a SQL Server database and then reports AlreadyExists")]
        public void EnsureExists_SqlServer_CreatesThenExists() => RunCreateThenExists(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("EnsureExists creates a PostgreSQL database and then reports AlreadyExists")]
        public void EnsureExists_PostgreSql_CreatesThenExists() => RunCreateThenExists(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("EnsureExists creates a MySQL database and then reports AlreadyExists")]
        public void EnsureExists_MySql_CreatesThenExists() => RunCreateThenExists(DatabaseType.MySQL);

        private static void RunCreateThenExists(DatabaseType databaseType)
        {
            var template = Environment.GetEnvironmentVariable(TestDbConventions.GetConnectionStringEnvVar(databaseType))!;
            var name = $"polhem_create_{Guid.NewGuid():N}"[..30];
            var connectionString = ConnectionStringTemplate.Resolve(template, name, string.Empty, string.Empty);
            var factory = GetFactory(databaseType);
            try
            {
                Assert.Equal(name, DatabaseCreator.GetDatabaseName(databaseType, factory, connectionString));
                Assert.Equal(DatabaseCreateResult.Created, DatabaseCreator.EnsureExists(databaseType, factory, connectionString));
                Assert.Equal(DatabaseCreateResult.AlreadyExists, DatabaseCreator.EnsureExists(databaseType, factory, connectionString));
            }
            finally
            {
                TemporaryDatabase.Drop(databaseType, template, name);
            }
        }

        private static DbProviderFactory GetFactory(DatabaseType databaseType) => databaseType switch
        {
            DatabaseType.SQLServer => SqlClientFactory.Instance,
            DatabaseType.PostgreSQL => NpgsqlFactory.Instance,
            DatabaseType.MySQL => MySqlConnectorFactory.Instance,
            DatabaseType.SQLite => new SqliteProviderFactory(SqliteFactory.Instance),
            _ => OracleClientFactory.Instance,
        };
    }
}
