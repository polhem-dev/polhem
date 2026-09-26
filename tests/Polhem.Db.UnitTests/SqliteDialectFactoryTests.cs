using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Ddl;
using Polhem.Db.Providers.Sqlite;
using Polhem.Db.Schema;
using Polhem.Definition.Database;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;
using Polhem.Db.Manager;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Pure syntax tests covering the factory methods of <see cref="SqliteDialectFactory"/>. They only check the
    /// concrete types returned and the consistency of the delegation to
    /// <c>SqliteSchemaSyntax.GetDefaultValueExpression</c>, without touching any database connection.
    /// </summary>
    public class SqliteDialectFactoryTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public SqliteDialectFactoryTests(SharedDbFixture fx) { _fx = fx; }
        private readonly SqliteDialectFactory _factory = new();

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SqliteDialectFactory CreateTableSchemaProvider returns a SqliteTableSchemaProvider")]
        public void CreateTableSchemaProvider_ReturnsSqliteImpl()
        {
            // A real databaseId is needed (constructing `DbAccess` looks up the connection registry), so `DbFact`
            // restricts when it runs.
            var databaseId = TestDbConventions.GetDatabaseId(DatabaseType.SQLite);
            ITableSchemaProvider provider = _factory.CreateTableSchemaProvider(databaseId, _fx.GetRequiredService<IDbConnectionManager>());
            Assert.IsType<SqliteTableSchemaProvider>(provider);
        }

        [Fact]
        [DisplayName("SqliteDialectFactory CreateCreateTableCommandBuilder returns a SqliteCreateTableCommandBuilder")]
        public void CreateCreateTableCommandBuilder_ReturnsSqliteImpl()
        {
            ICreateTableCommandBuilder builder = _factory.CreateCreateTableCommandBuilder();
            Assert.IsType<SqliteCreateTableCommandBuilder>(builder);
        }

        [Fact]
        [DisplayName("SqliteDialectFactory CreateTableAlterCommandBuilder returns a SqliteTableAlterCommandBuilder")]
        public void CreateTableAlterCommandBuilder_ReturnsSqliteImpl()
        {
            ITableAlterCommandBuilder builder = _factory.CreateTableAlterCommandBuilder();
            Assert.IsType<SqliteTableAlterCommandBuilder>(builder);
        }

        [Fact]
        [DisplayName("SqliteDialectFactory CreateTableRebuildCommandBuilder returns a SqliteTableRebuildCommandBuilder")]
        public void CreateTableRebuildCommandBuilder_ReturnsSqliteImpl()
        {
            ITableRebuildCommandBuilder builder = _factory.CreateTableRebuildCommandBuilder();
            Assert.IsType<SqliteTableRebuildCommandBuilder>(builder);
        }

        [Fact]
        [DisplayName("SqliteDialectFactory CreateFormCommandBuilder returns a SqliteFormCommandBuilder")]
        public void CreateFormCommandBuilder_ReturnsSqliteImpl()
        {
            var defineAccess = _fx.GetRequiredService<IDefineAccess>();
            var schema = new Polhem.Definition.Forms.FormSchema("Foo", "Foo");

            var builder = _factory.CreateFormCommandBuilder(schema, defineAccess);

            Assert.IsType<SqliteFormCommandBuilder>(builder);
        }

        [Theory]
        [InlineData(FieldDbType.String, "")]
        [InlineData(FieldDbType.Integer, "0")]
        [InlineData(FieldDbType.DateTime, "CURRENT_TIMESTAMP")]
        [InlineData(FieldDbType.Guid, "(hex(randomblob(16)))")]
        [DisplayName("SqliteDialectFactory GetDefaultValueExpression delegates to SqliteSchemaSyntax")]
        public void GetDefaultValueExpression_DelegatesToHelper(FieldDbType dbType, string expected)
        {
            Assert.Equal(expected, _factory.GetDefaultValueExpression(dbType));
        }
    }
}
