using System.ComponentModel;
using System.Globalization;
using Polhem.Core.Data;
using Polhem.Db.Manager;
using Polhem.Db.Schema;
using Polhem.Tests.Shared;
using Polhem.Definition.Database;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// SQLite-specific integration tests that exercise the schema provider, schema upgrade
    /// orchestrator and provider-level constraints against a live SQLite database. The
    /// FormSchema-driven IUD round-trip is covered separately in
    /// <see cref="FormCommandBuilderIudIntegrationTests"/>.
    /// </summary>
    public class SqliteIntegrationTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        public SqliteIntegrationTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite SchemaProvider reads back the st_user table created by the fixture")]
        public void SchemaProvider_ReadsFixtureTable()
        {
            var databaseId = TestDbConventions.GetDatabaseId(DatabaseType.SQLite);
            var provider = new Polhem.Db.Providers.Sqlite.SqliteTableSchemaProvider(databaseId, _fx.GetRequiredService<IDbConnectionManager>());

            var schema = provider.GetTableSchema("st_user");

            Assert.NotNull(schema);
            Assert.Equal("st_user", schema!.TableName);
            // sys_no is INTEGER PRIMARY KEY AUTOINCREMENT — read back as AutoIncrement.
            Assert.True(schema.Fields!.Contains("sys_no"));
            Assert.Equal(FieldDbType.AutoIncrement, schema.Fields["sys_no"].DbType);
            Assert.True(schema.Fields.Contains("sys_rowid"));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite SchemaProvider returns null for a table that does not exist")]
        public void SchemaProvider_UnknownTable_ReturnsNull()
        {
            var databaseId = TestDbConventions.GetDatabaseId(DatabaseType.SQLite);
            var provider = new Polhem.Db.Providers.Sqlite.SqliteTableSchemaProvider(databaseId, _fx.GetRequiredService<IDbConnectionManager>());

            var schema = provider.GetTableSchema("__no_such_table__");

            Assert.Null(schema);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite compares text columns case-insensitively (COLLATE NOCASE)")]
        public void StringComparison_IsCaseInsensitive()
        {
            var databaseId = TestDbConventions.GetDatabaseId(DatabaseType.SQLite);
            var dbAccess = _fx.NewDbAccess(databaseId);

            // Minimal hand-written DDL, to focus on how SQLite executes with COLLATE NOCASE columns,
            // independently of the pure syntax tests of `SqliteCreateTableCommandBuilder`.
            dbAccess.Execute(new Polhem.Db.DbCommandSpec(Polhem.Db.DbCommandKind.NonQuery,
                "DROP TABLE IF EXISTS ci_test"));
            dbAccess.Execute(new Polhem.Db.DbCommandSpec(Polhem.Db.DbCommandKind.NonQuery,
                "CREATE TABLE ci_test (name VARCHAR(50) COLLATE NOCASE NOT NULL)"));

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

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite compares GUID columns case-insensitively (COLLATE NOCASE, matching across casing)")]
        public void GuidComparison_IsCaseInsensitive()
        {
            var databaseId = TestDbConventions.GetDatabaseId(DatabaseType.SQLite);
            var dbAccess = _fx.NewDbAccess(databaseId);

            // Creates the table with the actual declaration of a GUID column (UUID COLLATE NOCASE) to verify that the
            // SQLite runtime compares GUID strings case-insensitively. Master-detail reload relies on this when it
            // compares sys_master_rowid keys, so that details do not become orphans because of casing.
            dbAccess.Execute(new Polhem.Db.DbCommandSpec(Polhem.Db.DbCommandKind.NonQuery,
                "DROP TABLE IF EXISTS guid_ci_test"));
            dbAccess.Execute(new Polhem.Db.DbCommandSpec(Polhem.Db.DbCommandKind.NonQuery,
                "CREATE TABLE guid_ci_test (sys_rowid UUID COLLATE NOCASE NOT NULL)"));

            try
            {
                // Stored in upper case (matching the seed and the provider's TEXT convention), queried in lower case.
                dbAccess.Execute(new Polhem.Db.DbCommandSpec(Polhem.Db.DbCommandKind.NonQuery,
                    "INSERT INTO guid_ci_test (sys_rowid) VALUES ({0})", "6689B38C-39D5-43B0-9682-27F9ADEEEDC5"));

                var result = dbAccess.Execute(new Polhem.Db.DbCommandSpec(Polhem.Db.DbCommandKind.Scalar,
                    "SELECT COUNT(*) FROM guid_ci_test WHERE sys_rowid = {0}", "6689b38c-39d5-43b0-9682-27f9adeeedc5"));

                Assert.NotNull(result);
                Assert.Equal(1, Convert.ToInt32(result.Scalar!, CultureInfo.InvariantCulture));
            }
            finally
            {
                dbAccess.Execute(new Polhem.Db.DbCommandSpec(Polhem.Db.DbCommandKind.NonQuery,
                    "DROP TABLE IF EXISTS guid_ci_test"));
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite SchemaProvider reads back non-primary-key secondary indexes (including the unique flag)")]
        public void SchemaProvider_ReadsSecondaryIndexes()
        {
            var databaseId = TestDbConventions.GetDatabaseId(DatabaseType.SQLite);
            var dbAccess = _fx.NewDbAccess(databaseId);

            // A table with secondary indexes created with minimal DDL, to exercise the `ParseIndexes` /
            // `ReadIndexFields` path.
            dbAccess.Execute(new Polhem.Db.DbCommandSpec(Polhem.Db.DbCommandKind.NonQuery,
                "DROP TABLE IF EXISTS idx_test"));
            dbAccess.Execute(new Polhem.Db.DbCommandSpec(Polhem.Db.DbCommandKind.NonQuery,
                "CREATE TABLE idx_test (id INTEGER PRIMARY KEY, name VARCHAR(50) NOT NULL, code VARCHAR(20) NOT NULL)"));
            dbAccess.Execute(new Polhem.Db.DbCommandSpec(Polhem.Db.DbCommandKind.NonQuery,
                "CREATE INDEX ix_idx_test_name ON idx_test (name)"));
            dbAccess.Execute(new Polhem.Db.DbCommandSpec(Polhem.Db.DbCommandKind.NonQuery,
                "CREATE UNIQUE INDEX uk_idx_test_code ON idx_test (code)"));

            try
            {
                var provider = new Polhem.Db.Providers.Sqlite.SqliteTableSchemaProvider(databaseId, _fx.GetRequiredService<IDbConnectionManager>());
                var schema = provider.GetTableSchema("idx_test");

                Assert.NotNull(schema);
                // Expected: pk_idx_test (the internally renamed PK) + ix_idx_test_name + uk_idx_test_code.
                var nonPk = schema!.Indexes!.Where(i => !i.PrimaryKey).ToList();
                Assert.Equal(2, nonPk.Count);

                var nameIdx = nonPk.Single(i => i.Name == "ix_idx_test_name");
                Assert.False(nameIdx.Unique);
                Assert.Equal("name", nameIdx.IndexFields![0].FieldName);

                var codeIdx = nonPk.Single(i => i.Name == "uk_idx_test_code");
                Assert.True(codeIdx.Unique);
                Assert.Equal("code", codeIdx.IndexFields![0].FieldName);
            }
            finally
            {
                dbAccess.Execute(new Polhem.Db.DbCommandSpec(Polhem.Db.DbCommandKind.NonQuery,
                    "DROP TABLE IF EXISTS idx_test"));
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite SchemaProvider parses NUMERIC(precision,scale) and restores the precision of a Decimal field")]
        public void SchemaProvider_ReadsDecimalPrecisionAndScale()
        {
            var databaseId = TestDbConventions.GetDatabaseId(DatabaseType.SQLite);
            var dbAccess = _fx.NewDbAccess(databaseId);

            // NUMERIC(12,3) reaches the multi-argument branch of `ParseTypeFacets`
            // and the Decimal precision/scale restore.
            dbAccess.Execute(new Polhem.Db.DbCommandSpec(Polhem.Db.DbCommandKind.NonQuery,
                "DROP TABLE IF EXISTS dec_test"));
            dbAccess.Execute(new Polhem.Db.DbCommandSpec(Polhem.Db.DbCommandKind.NonQuery,
                "CREATE TABLE dec_test (id INTEGER PRIMARY KEY, amount NUMERIC(12,3) NOT NULL)"));

            try
            {
                var provider = new Polhem.Db.Providers.Sqlite.SqliteTableSchemaProvider(databaseId, _fx.GetRequiredService<IDbConnectionManager>());
                var schema = provider.GetTableSchema("dec_test");

                Assert.NotNull(schema);
                var amount = schema!.Fields!["amount"];
                Assert.Equal(FieldDbType.Decimal, amount.DbType);
                Assert.Equal(12, amount.Precision);
                Assert.Equal(3, amount.Scale);
            }
            finally
            {
                dbAccess.Execute(new Polhem.Db.DbCommandSpec(Polhem.Db.DbCommandKind.NonQuery,
                    "DROP TABLE IF EXISTS dec_test"));
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite comparison right after creating a table with a Guid field returns None (a database-side built-in default must not cause a permanent diff)")]
        public void SchemaComparer_AfterCreatingGuidColumn_ReportsNoUpgrade()
        {
            var databaseId = TestDbConventions.GetDatabaseId(DatabaseType.SQLite);
            var connectionManager = _fx.GetRequiredService<IDbConnectionManager>();
            var dbAccess = _fx.NewDbAccess(databaseId);
            const string tableName = "guid_default_test";

            // On SQLite a Guid field carries a database-side default (hex(randomblob(16))), while the definition's
            // DefaultValue is an empty string. Unless both are normalized to the same form when read back, the
            // comparison would always mark the field for Upgrade and the table would never converge.
            var define = new TableSchema { TableName = tableName };
            define.Fields!.Add("sys_rowid", "Row ID", FieldDbType.Guid);
            define.Fields!.Add("name", "Name", FieldDbType.String, 50);
            define.Indexes!.AddPrimaryKey("sys_rowid");

            dbAccess.Execute(new Polhem.Db.DbCommandSpec(Polhem.Db.DbCommandKind.NonQuery,
                $"DROP TABLE IF EXISTS {tableName}"));
            try
            {
                var orchestrator = new TableUpgradeOrchestrator(databaseId, connectionManager);
                var createDiff = new TableSchemaComparer(define, null, DatabaseType.SQLite).CompareToDiff();
                Assert.True(orchestrator.Execute(orchestrator.Plan(createDiff), databaseId));

                var provider = new Polhem.Db.Providers.Sqlite.SqliteTableSchemaProvider(databaseId, connectionManager);
                var real = provider.GetTableSchema(tableName);
                Assert.NotNull(real);
                Assert.Equal(string.Empty, real!.Fields!["sys_rowid"].DefaultValue);

                var diff = new TableSchemaComparer(define, real, DatabaseType.SQLite).CompareToDiff();
                Assert.Empty(diff.Changes);
            }
            finally
            {
                dbAccess.Execute(new Polhem.Db.DbCommandSpec(Polhem.Db.DbCommandKind.NonQuery,
                    $"DROP TABLE IF EXISTS {tableName}"));
            }
        }

    }
}
