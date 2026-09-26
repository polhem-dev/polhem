using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Providers.PostgreSql;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;
using Polhem.Db.Manager;

namespace Polhem.Db.UnitTests
{
    public class PgTableSchemaProviderTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        public PgTableSchemaProviderTests(SharedDbFixture fx) { _fx = fx; }

        private const string DatabaseId = "common_postgresql";

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PgTableSchemaProvider gets the table schema")]
        public void GetTableSchema_ValidTableName_ReturnsSchema()
        {
            var helper = new PgTableSchemaProvider(DatabaseId, _fx.GetRequiredService<IDbConnectionManager>());
            var dbTable = helper.GetTableSchema("st_user");
            Assert.NotNull(dbTable);
        }

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PgTableSchemaProvider returns null for a table that does not exist")]
        public void GetTableSchema_NonExistentTable_ReturnsNull()
        {
            var helper = new PgTableSchemaProvider(DatabaseId, _fx.GetRequiredService<IDbConnectionManager>());
            var dbTable = helper.GetTableSchema("polhem_nonexistent_table_xyz_99999");
            Assert.Null(dbTable);
        }

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PgTableSchemaProvider DatabaseId equals the value passed to the constructor")]
        public void Constructor_DatabaseId_IsSet()
        {
            var helper = new PgTableSchemaProvider(DatabaseId, _fx.GetRequiredService<IDbConnectionManager>());
            Assert.Equal(DatabaseId, helper.DatabaseId);
        }

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("GetTableSchema reads the table-level DisplayName back from COMMENT ON TABLE")]
        public void GetTableSchema_WithComment_ReturnsDisplayName()
        {
            string tableName = $"polhem_test_desc_{Guid.NewGuid():N}";
            var dbAccess = _fx.NewDbAccess(DatabaseId);
            try
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"CREATE TABLE \"{tableName}\" (\"id\" integer NOT NULL);"));
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"COMMENT ON TABLE \"{tableName}\" IS '測試表說明';"));

                var provider = new PgTableSchemaProvider(DatabaseId, _fx.GetRequiredService<IDbConnectionManager>());
                var schema = provider.GetTableSchema(tableName);

                Assert.NotNull(schema);
                Assert.Equal("測試表說明", schema!.DisplayName);
            }
            finally
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"DROP TABLE IF EXISTS \"{tableName}\";"));
            }
        }

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("GetTableSchema sets DisplayName to an empty string when there is no COMMENT")]
        public void GetTableSchema_WithoutComment_ReturnsEmptyDisplayName()
        {
            string tableName = $"polhem_test_desc_{Guid.NewGuid():N}";
            var dbAccess = _fx.NewDbAccess(DatabaseId);
            try
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"CREATE TABLE \"{tableName}\" (\"id\" integer NOT NULL);"));

                var provider = new PgTableSchemaProvider(DatabaseId, _fx.GetRequiredService<IDbConnectionManager>());
                var schema = provider.GetTableSchema(tableName);

                Assert.NotNull(schema);
                Assert.Equal(string.Empty, schema!.DisplayName);
            }
            finally
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"DROP TABLE IF EXISTS \"{tableName}\";"));
            }
        }

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("GetTableSchema parses the Precision and Scale of a Decimal field")]
        public void GetTableSchema_DecimalColumn_ParsesPrecisionAndScale()
        {
            string tableName = $"polhem_test_decimal_{Guid.NewGuid():N}";
            var dbAccess = _fx.NewDbAccess(DatabaseId);
            try
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"CREATE TABLE \"{tableName}\" (\"id\" integer NOT NULL, \"amount\" numeric(12,3) NOT NULL);"));

                var provider = new PgTableSchemaProvider(DatabaseId, _fx.GetRequiredService<IDbConnectionManager>());
                var schema = provider.GetTableSchema(tableName);

                Assert.NotNull(schema);
                var amount = schema!.Fields![@"amount"];
                Assert.Equal(FieldDbType.Decimal, amount.DbType);
                Assert.Equal(12, amount.Precision);
                Assert.Equal(3, amount.Scale);
            }
            finally
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"DROP TABLE IF EXISTS \"{tableName}\";"));
            }
        }

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("GetTableSchema parses both the primary key and the non-primary-key indexes")]
        public void GetTableSchema_PrimaryKeyAndSecondaryIndex_BothParsed()
        {
            string tableName = $"polhem_test_idx_{Guid.NewGuid():N}";
            string idxName = $"ix_{tableName}_name";
            var dbAccess = _fx.NewDbAccess(DatabaseId);
            try
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"CREATE TABLE \"{tableName}\" (\"id\" integer NOT NULL, \"name\" varchar(50) NOT NULL, " +
                    $"CONSTRAINT \"pk_{tableName}\" PRIMARY KEY (\"id\"));"));
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"CREATE INDEX \"{idxName}\" ON \"{tableName}\" (\"name\");"));

                var provider = new PgTableSchemaProvider(DatabaseId, _fx.GetRequiredService<IDbConnectionManager>());
                var schema = provider.GetTableSchema(tableName);

                Assert.NotNull(schema);
                // Primary key index
                var pk = schema!.GetPrimaryKey();
                Assert.NotNull(pk);
                Assert.True(pk!.PrimaryKey);
                Assert.Single(pk.IndexFields!);
                Assert.Equal("id", pk.IndexFields![0].FieldName);

                // Secondary (non-primary) index
                var secondary = schema.Indexes!.Cast<DbTableIndex>().FirstOrDefault(i => !i.PrimaryKey);
                Assert.NotNull(secondary);
                Assert.Equal(idxName, secondary!.Name);
                Assert.Single(secondary.IndexFields!);
                Assert.Equal("name", secondary.IndexFields![0].FieldName);
            }
            finally
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"DROP TABLE IF EXISTS \"{tableName}\";"));
            }
        }

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("GetTableSchema returns an empty PK for a table without a primary key and still parses the fields")]
        public void GetTableSchema_NoPrimaryKey_ReturnsSchemaWithoutPk()
        {
            string tableName = $"polhem_test_nopk_{Guid.NewGuid():N}";
            var dbAccess = _fx.NewDbAccess(DatabaseId);
            try
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"CREATE TABLE \"{tableName}\" (\"value\" integer NOT NULL);"));

                var provider = new PgTableSchemaProvider(DatabaseId, _fx.GetRequiredService<IDbConnectionManager>());
                var schema = provider.GetTableSchema(tableName);

                Assert.NotNull(schema);
                Assert.Null(schema!.GetPrimaryKey());
                Assert.NotNull(schema.Fields!["value"]);
                Assert.Equal(FieldDbType.Integer, schema.Fields!["value"].DbType);
            }
            finally
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"DROP TABLE IF EXISTS \"{tableName}\";"));
            }
        }

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("GetTableSchema parses an AutoIncrement field as FieldDbType.AutoIncrement")]
        public void GetTableSchema_AutoIncrementColumn_MapsToAutoIncrement()
        {
            string tableName = $"polhem_test_identity_{Guid.NewGuid():N}";
            var dbAccess = _fx.NewDbAccess(DatabaseId);
            try
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"CREATE TABLE \"{tableName}\" (\"id\" integer GENERATED BY DEFAULT AS IDENTITY NOT NULL);"));

                var provider = new PgTableSchemaProvider(DatabaseId, _fx.GetRequiredService<IDbConnectionManager>());
                var schema = provider.GetTableSchema(tableName);

                Assert.NotNull(schema);
                Assert.Equal(FieldDbType.AutoIncrement, schema!.Fields!["id"].DbType);
            }
            finally
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"DROP TABLE IF EXISTS \"{tableName}\";"));
            }
        }
    }
}
