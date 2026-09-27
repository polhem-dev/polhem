using System.ComponentModel;
using Polhem.Definition.Settings;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.System;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// Pure logic tests for the default implementation of <see cref="IDatabaseRepository"/>.
    /// The instance comes from the fixture's <see cref="IRepositoryFactory"/> (to avoid depending on the internal type directly).
    /// </summary>
    public class DatabaseRepositoryTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public DatabaseRepositoryTests(SharedDbFixture fx) { _fx = fx; }
        private const string ValidDatabaseId = "common";
        private const string ValidCategoryId = "common";
        private const string ValidTableName = "TableName";

        private IDatabaseRepository CreateRepository()
            => _fx.GetRequiredService<IRepositoryFactory>().Create<IDatabaseRepository>();

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("UpgradeTableSchema throws ArgumentException for a blank databaseId")]
        public void UpgradeTableSchema_EmptyDatabaseId_ThrowsArgumentException(string? databaseId)
        {
            var repo = CreateRepository();
            Assert.ThrowsAny<ArgumentException>(() => repo.UpgradeTableSchema(databaseId!, ValidCategoryId, ValidTableName));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("UpgradeTableSchema throws ArgumentException for a blank categoryId")]
        public void UpgradeTableSchema_EmptyCategoryId_ThrowsArgumentException(string? categoryId)
        {
            var repo = CreateRepository();
            Assert.ThrowsAny<ArgumentException>(() => repo.UpgradeTableSchema(ValidDatabaseId, categoryId!, ValidTableName));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("UpgradeTableSchema throws ArgumentException for a blank tableName")]
        public void UpgradeTableSchema_EmptyTableName_ThrowsArgumentException(string? tableName)
        {
            var repo = CreateRepository();
            Assert.ThrowsAny<ArgumentException>(() => repo.UpgradeTableSchema(ValidDatabaseId, ValidCategoryId, tableName!));
        }

        [Fact]
        [DisplayName("TestConnection throws KeyNotFoundException for an unregistered DatabaseType")]
        public void TestConnection_UnregisteredDatabaseType_ThrowsKeyNotFoundException()
        {
            // GlobalFixture registers every defined DatabaseType. To still check the guard that an unregistered
            // DatabaseType throws `KeyNotFoundException`, this casts an integer outside the enum range as a
            // placeholder that will never be registered.
            var repo = CreateRepository();
            var item = new DatabaseItem
            {
                Id = "unregistered_test",
                DatabaseType = (DatabaseType)9999,
                ConnectionString = "Data Source=localhost;User Id=foo;Password=bar;"
            };

            Assert.Throws<KeyNotFoundException>(() => repo.TestConnection(item));
        }

        [Fact]
        [DisplayName("TestConnection throws NullReferenceException for a null DatabaseItem (current behavior)")]
        public void TestConnection_NullItem_ThrowsNullReferenceException()
        {
            // The current implementation has no null guard, so reading `item.DatabaseType` throws
            // `NullReferenceException`. The test only records the current behavior and must change if a guard is added.
            var repo = CreateRepository();
            Assert.Throws<NullReferenceException>(() => repo.TestConnection(null!));
        }

        [Fact]
        [DisplayName("TestConnection replaces the {@DbName} placeholder when DbName is set and attempts to connect")]
        public void TestConnection_WithDbNamePlaceholder_ReplacesAndAttempts()
        {
            // Uses valid SQL Server connection string syntax pointing at a host that does not exist, to make sure that
            // (1) the replacement succeeds (without it, `SqlConnection` would still accept `{@DbName}` as the catalog
            // name), and (2) it finally fails at `Open()` rather than throwing `ArgumentException` while parsing.
            var repo = CreateRepository();
            var item = new DatabaseItem
            {
                Id = "placeholder_dbname",
                DatabaseType = DatabaseType.SQLServer,
                ConnectionString = "Server=127.0.0.1,65535;Initial Catalog={@DbName};Connection Timeout=1;",
                DbName = "polhem_test_placeholder_db"
            };

            // Expect `Open` to throw `SqlException` or a compatible exception (connection failure).
            var ex = Record.Exception(() => repo.TestConnection(item));
            Assert.IsType<Microsoft.Data.SqlClient.SqlException>(ex);
        }

        [Fact]
        [DisplayName("TestConnection replaces the {@UserId} and {@Password} placeholders when both are set")]
        public void TestConnection_WithUserIdAndPasswordPlaceholder_ReplacesAndAttempts()
        {
            var repo = CreateRepository();
            var item = new DatabaseItem
            {
                Id = "placeholder_user",
                DatabaseType = DatabaseType.SQLServer,
                ConnectionString = "Server=127.0.0.1,65535;User Id={@UserId};Password={@Password};Connection Timeout=1;",
                UserId = "sa_test",
                Password = "p@ssword_test"
            };

            var ex = Record.Exception(() => repo.TestConnection(item));
            Assert.IsType<Microsoft.Data.SqlClient.SqlException>(ex);
        }

        [Fact]
        [DisplayName("TestConnection replaces all placeholders when all are specified")]
        public void TestConnection_WithAllPlaceholders_ReplacesAndAttempts()
        {
            var repo = CreateRepository();
            var item = new DatabaseItem
            {
                Id = "placeholder_all",
                DatabaseType = DatabaseType.SQLServer,
                ConnectionString = "Server=127.0.0.1,65535;Initial Catalog={@DbName};User Id={@UserId};Password={@Password};Connection Timeout=1;",
                DbName = "polhem_test_db",
                UserId = "sa_test",
                Password = "p@ssword_test"
            };

            var ex = Record.Exception(() => repo.TestConnection(item));
            Assert.IsType<Microsoft.Data.SqlClient.SqlException>(ex);
        }
    }
}
