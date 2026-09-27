using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Manager;
using Polhem.Db.Providers.PostgreSql;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// PostgreSQL provider/dialect smoke tests. Verifies that the test fixture
    /// (<see cref="SharedDbFixture"/>) registers both the ADO.NET provider factory
    /// and the dialect factory at startup, so subsequent PG builder/integration
    /// tests can resolve them via the registries.
    /// </summary>
    public class PgDialectFactoryTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public PgDialectFactoryTests(SharedDbFixture fx) { _fx = fx; }
        [Fact]
        [DisplayName("PG DialectFactory is registered through DbDialectRegistry")]
        public void DialectFactory_IsRegistered()
        {
            var factory = DbDialectRegistry.Get(DatabaseType.PostgreSQL);

            Assert.NotNull(factory);
            Assert.IsType<PgDialectFactory>(factory);
        }

        [Fact]
        [DisplayName("PG ADO.NET provider is registered through DbProviderRegistry")]
        public void Provider_IsRegistered()
        {
            var factory = DbProviderRegistry.Get(DatabaseType.PostgreSQL);

            Assert.Same(Npgsql.NpgsqlFactory.Instance, factory);
        }

        [Fact]
        [DisplayName("PG DialectFactory GetDefaultValueExpression returns the matching expressions")]
        public void DialectFactory_DefaultValueExpression_ReturnsExpected()
        {
            var factory = new PgDialectFactory();

            Assert.Equal("gen_random_uuid()", factory.GetDefaultValueExpression(FieldDbType.Guid));
            Assert.Equal("(NOW() AT TIME ZONE 'UTC')", factory.GetDefaultValueExpression(FieldDbType.DateTime));
            Assert.Equal("(NOW() AT TIME ZONE 'UTC')", factory.GetDefaultValueExpression(FieldDbType.Date));
            Assert.Equal("0", factory.GetDefaultValueExpression(FieldDbType.Integer));
            Assert.Equal("0", factory.GetDefaultValueExpression(FieldDbType.Boolean));
            Assert.Equal(string.Empty, factory.GetDefaultValueExpression(FieldDbType.String));
            Assert.Equal(string.Empty, factory.GetDefaultValueExpression(FieldDbType.Text));
        }

        [Fact]
        [DisplayName("PG DialectFactory creates each pure SQL generator")]
        public void DialectFactory_CreatesAllBuilders()
        {
            var factory = new PgDialectFactory();

            // Only builders without external dependencies are checked: CREATE / ALTER / Rebuild produce plain strings
            // and need no connection or FormSchema lookup.
            Assert.IsType<PgCreateTableCommandBuilder>(factory.CreateCreateTableCommandBuilder());
            Assert.IsType<PgTableAlterCommandBuilder>(factory.CreateTableAlterCommandBuilder());
            Assert.IsType<PgTableRebuildCommandBuilder>(factory.CreateTableRebuildCommandBuilder());
        }

        [Fact]
        [DisplayName("PG DialectFactory creates a FormCommandBuilder")]
        public void DialectFactory_CreateFormCommandBuilder_ReturnsInstance()
        {
            var factory = new PgDialectFactory();
            var defineAccess = _fx.GetRequiredService<IDefineAccess>();
            var schema = new FormSchema("Foo", "Foo");

            Assert.IsType<PgFormCommandBuilder>(factory.CreateFormCommandBuilder(schema, defineAccess));
        }

        // The constructor of `PgTableSchemaProvider` eagerly creates `new DbAccess(databaseId)`. Without
        // POLHEM_TEST_CONNSTR_POSTGRESQL, 'common_postgresql' is not registered in `DbConnectionManager`, so a bare
        // `[Fact]` would fail hard instead of skipping. `[DbFact]` skips it automatically where there is no container.
        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PG DialectFactory creates a TableSchemaProvider")]
        public void DialectFactory_CreateTableSchemaProvider_ReturnsInstance()
        {
            var factory = new PgDialectFactory();

            Assert.IsType<PgTableSchemaProvider>(factory.CreateTableSchemaProvider("common_postgresql", _fx.GetRequiredService<IDbConnectionManager>()));
        }
    }
}
