using System.ComponentModel;
using Polhem.Db.Providers.SqlServer;
using Polhem.Tests.Shared;
using Polhem.Definition.Database;
using Polhem.Db.Manager;

namespace Polhem.Db.UnitTests
{
    public class SqlTableSchemaProviderTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        public SqlTableSchemaProviderTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SqlTableSchemaProvider gets the table schema")]
        public void GetTableSchema_ValidTableName_ReturnsSchema()
        {
            var helper = new SqlTableSchemaProvider("common_sqlserver", _fx.GetRequiredService<IDbConnectionManager>());
            var dbTable = helper.GetTableSchema("st_user");
            Assert.Equal("st_user", dbTable!.TableName, ignoreCase: true);
            Assert.True(dbTable.Fields!.Contains("sys_id"));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SqlTableSchemaProvider returns null for a table that does not exist")]
        public void GetTableSchema_NonExistentTable_ReturnsNull()
        {
            var helper = new SqlTableSchemaProvider("common_sqlserver", _fx.GetRequiredService<IDbConnectionManager>());
            var dbTable = helper.GetTableSchema("polhem_nonexistent_table_xyz_99999");
            Assert.Null(dbTable);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SqlTableSchemaProvider DatabaseId equals the value passed to the constructor")]
        public void Constructor_DatabaseId_IsSet()
        {
            var helper = new SqlTableSchemaProvider("common_sqlserver", _fx.GetRequiredService<IDbConnectionManager>());
            Assert.Equal("common_sqlserver", helper.DatabaseId);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("GetTableSchema reads the table-level DisplayName back from the extended property")]
        public void GetTableSchema_WithExtendedProperty_ReturnsDisplayName()
        {
            string tableName = $"polhem_test_desc_{Guid.NewGuid():N}";
            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            try
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"CREATE TABLE [{tableName}] ([id] [int] NOT NULL);"));
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    "EXEC sp_addextendedproperty @name=N'MS_Description', @value=N'測試表說明'," +
                    $" @level0type=N'SCHEMA', @level0name=N'dbo', @level1type=N'TABLE', @level1name=N'{tableName}';"));

                var provider = new SqlTableSchemaProvider("common_sqlserver", _fx.GetRequiredService<IDbConnectionManager>());
                var schema = provider.GetTableSchema(tableName);

                Assert.NotNull(schema);
                Assert.Equal("測試表說明", schema!.DisplayName);
            }
            finally
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"IF (SELECT COUNT(*) FROM sys.tables WHERE name=N'{tableName}')>0 DROP TABLE [{tableName}];"));
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("GetTableSchema sets DisplayName to an empty string when there is no extended property")]
        public void GetTableSchema_WithoutExtendedProperty_ReturnsEmptyDisplayName()
        {
            string tableName = $"polhem_test_desc_{Guid.NewGuid():N}";
            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            try
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"CREATE TABLE [{tableName}] ([id] [int] NOT NULL);"));

                var provider = new SqlTableSchemaProvider("common_sqlserver", _fx.GetRequiredService<IDbConnectionManager>());
                var schema = provider.GetTableSchema(tableName);

                Assert.NotNull(schema);
                Assert.Equal(string.Empty, schema!.DisplayName);
            }
            finally
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"IF (SELECT COUNT(*) FROM sys.tables WHERE name=N'{tableName}')>0 DROP TABLE [{tableName}];"));
            }
        }
    }
}
