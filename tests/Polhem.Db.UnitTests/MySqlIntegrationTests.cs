using System.ComponentModel;
using System.Globalization;
using Polhem.Core.Data;
using Polhem.Db.Manager;
using Polhem.Tests.Shared;
using Polhem.Definition.Database;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// MySQL-specific integration tests: schema reader round-trip against the live fixture
    /// table, and engine-level case-insensitive comparison provided by the day-1 collation
    /// <c>utf8mb4_0900_ai_ci</c>. Skipped when no MySQL connection string is configured
    /// (<see cref="DbFactAttribute"/> handles the env-var check).
    /// </summary>
    public class MySqlIntegrationTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        public MySqlIntegrationTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL SchemaProvider reads back the st_user table created by the fixture")]
        public void SchemaProvider_ReadsFixtureTable()
        {
            var databaseId = TestDbConventions.GetDatabaseId(DatabaseType.MySQL);
            var provider = new Polhem.Db.Providers.MySql.MySqlTableSchemaProvider(databaseId, _fx.GetRequiredService<IDbConnectionManager>());

            var schema = provider.GetTableSchema("st_user");

            Assert.NotNull(schema);
            Assert.Equal("st_user", schema!.TableName);
            // sys_no is BIGINT AUTO_INCREMENT PRIMARY KEY — read back as AutoIncrement.
            Assert.True(schema.Fields!.Contains("sys_no"));
            Assert.Equal(FieldDbType.AutoIncrement, schema.Fields["sys_no"].DbType);
            Assert.True(schema.Fields.Contains("sys_rowid"));
        }

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL SchemaProvider returns null for a table that does not exist")]
        public void SchemaProvider_UnknownTable_ReturnsNull()
        {
            var databaseId = TestDbConventions.GetDatabaseId(DatabaseType.MySQL);
            var provider = new Polhem.Db.Providers.MySql.MySqlTableSchemaProvider(databaseId, _fx.GetRequiredService<IDbConnectionManager>());

            var schema = provider.GetTableSchema("__no_such_table__");

            Assert.Null(schema);
        }

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL compares text columns case-insensitively (utf8mb4_0900_ai_ci)")]
        public void StringComparison_IsCaseInsensitive()
        {
            var databaseId = TestDbConventions.GetDatabaseId(DatabaseType.MySQL);
            var dbAccess = _fx.NewDbAccess(databaseId);

            // Minimal hand-written DDL, to focus on how MySQL executes with the utf8mb4_0900_ai_ci collation,
            // independently of the pure syntax tests of `MySqlCreateTableCommandBuilder`.
            dbAccess.Execute(new Polhem.Db.DbCommandSpec(Polhem.Db.DbCommandKind.NonQuery,
                "DROP TABLE IF EXISTS ci_test"));
            dbAccess.Execute(new Polhem.Db.DbCommandSpec(Polhem.Db.DbCommandKind.NonQuery,
                "CREATE TABLE ci_test (name VARCHAR(50) NOT NULL) " +
                "ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci"));

            try
            {
                dbAccess.Execute(new Polhem.Db.DbCommandSpec(Polhem.Db.DbCommandKind.NonQuery,
                    "INSERT INTO ci_test (name) VALUES ({0})", "Jeff"));

                var result = dbAccess.Execute(new Polhem.Db.DbCommandSpec(Polhem.Db.DbCommandKind.Scalar,
                    "SELECT COUNT(*) FROM ci_test WHERE name = {0}", "jeff"));

                Assert.NotNull(result);
                Assert.Equal(1, Convert.ToInt32(result.Scalar!, CultureInfo.InvariantCulture));
            }
            finally
            {
                dbAccess.Execute(new Polhem.Db.DbCommandSpec(Polhem.Db.DbCommandKind.NonQuery,
                    "DROP TABLE IF EXISTS ci_test"));
            }
        }
    }
}
