using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Providers.MySql;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;
using Polhem.Db.Manager;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Additional integration paths of <see cref="MySqlTableSchemaProvider"/>: the Decimal branch of
    /// <c>ParseDbField</c> (assigning Precision/Scale) and the early return of <c>ParsePrimaryKey</c> when there is no
    /// primary key. Requires a MySQL connection; skipped automatically when the environment variable is not set.
    /// </summary>
    public class MySqlTableSchemaProviderExtraTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        public MySqlTableSchemaProviderExtraTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL SchemaProvider sets Precision and Scale when reading back a DECIMAL(15,3) field (ParseDbField Decimal branch)")]
        public void GetTableSchema_DecimalField_ReturnsPrecisionAndScale()
        {
            const string tableName = "tb_ex_decimal";
            string databaseId = TestDbConventions.GetDatabaseId(DatabaseType.MySQL);
            var dbAccess = _fx.NewDbAccess(databaseId);
            DropMySqlTable(dbAccess, tableName);

            try
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    "CREATE TABLE tb_ex_decimal (id VARCHAR(36) NOT NULL, " +
                    "amount DECIMAL(15,3) NOT NULL, " +
                    "CONSTRAINT PK_TB_EX_DECIMAL PRIMARY KEY (id)) " +
                    "ENGINE=InnoDB DEFAULT CHARSET=utf8mb4"));

                var provider = new MySqlTableSchemaProvider(databaseId, _fx.GetRequiredService<IDbConnectionManager>());
                var schema = provider.GetTableSchema(tableName);

                Assert.NotNull(schema);
                Assert.True(schema!.Fields!.Contains("amount"));
                var field = schema.Fields["amount"];
                Assert.Equal(FieldDbType.Decimal, field.DbType);
                Assert.Equal(15, field.Precision);
                Assert.Equal(3, field.Scale);
            }
            finally
            {
                DropMySqlTable(dbAccess, tableName);
            }
        }

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL SchemaProvider reading a table with only a unique index (no PK) returns early from ParsePrimaryKey and still parses the unique index")]
        public void GetTableSchema_TableWithUniqueIndexNoPk_ParsePrimaryKeyReturnsEarlyAndIndexPresent()
        {
            const string tableName = "tb_ex_nopk";
            string databaseId = TestDbConventions.GetDatabaseId(DatabaseType.MySQL);
            var dbAccess = _fx.NewDbAccess(databaseId);
            DropMySqlTable(dbAccess, tableName);

            try
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    "CREATE TABLE tb_ex_nopk (code VARCHAR(20) NOT NULL) " +
                    "ENGINE=InnoDB DEFAULT CHARSET=utf8mb4"));
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    "CREATE UNIQUE INDEX UX_TB_EX_NOPK ON tb_ex_nopk (code)"));

                var provider = new MySqlTableSchemaProvider(databaseId, _fx.GetRequiredService<IDbConnectionManager>());
                var schema = provider.GetTableSchema(tableName);

                Assert.NotNull(schema);
                Assert.True(schema!.Fields!.Contains("code"));
                Assert.NotEmpty(schema.Indexes!);
                Assert.DoesNotContain(schema.Indexes!, idx => idx.PrimaryKey);
            }
            finally
            {
                DropMySqlTable(dbAccess, tableName);
            }
        }

        private static void DropMySqlTable(DbAccess dbAccess, string tableName)
        {
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                "DROP TABLE IF EXISTS " + tableName));
        }
    }
}
