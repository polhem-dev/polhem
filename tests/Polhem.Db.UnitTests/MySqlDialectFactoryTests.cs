using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Providers.MySql;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;
using Polhem.Db.Manager;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// MySQL provider/dialect smoke tests. Verifies that the test fixture
    /// (<see cref="Polhem.Tests.Shared.GlobalFixture"/>) registers both the ADO.NET
    /// provider factory and the dialect factory at startup, so subsequent MySQL
    /// builder/integration tests can resolve them via the registries.
    /// </summary>
    /// <remarks>
    /// Only the factory wiring is validated here — that each create-builder method
    /// returns the MySQL-specific implementation. The builders' own SQL output is
    /// covered by their respective test classes.
    /// </remarks>
    public class MySqlDialectFactoryTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public MySqlDialectFactoryTests(SharedDbFixture fx) { _fx = fx; }
        [Fact]
        [DisplayName("MySQL DialectFactory is registered through DbDialectRegistry")]
        public void DialectFactory_IsRegistered()
        {
            var factory = DbDialectRegistry.Get(DatabaseType.MySQL);

            Assert.NotNull(factory);
            Assert.IsType<MySqlDialectFactory>(factory);
        }

        [Fact]
        [DisplayName("MySQL ADO.NET provider is registered through DbProviderRegistry")]
        public void Provider_IsRegistered()
        {
            var factory = DbProviderRegistry.Get(DatabaseType.MySQL);

            Assert.NotNull(factory);
        }

        [Fact]
        [DisplayName("MySQL DialectFactory GetDefaultValueExpression returns the matching expressions")]
        public void DialectFactory_DefaultValueExpression_ReturnsExpected()
        {
            var factory = new MySqlDialectFactory();

            Assert.Equal("(UUID())", factory.GetDefaultValueExpression(FieldDbType.Guid));
            Assert.Equal("(UTC_TIMESTAMP(6))", factory.GetDefaultValueExpression(FieldDbType.DateTime));
            Assert.Equal("0", factory.GetDefaultValueExpression(FieldDbType.Integer));
            Assert.Equal(string.Empty, factory.GetDefaultValueExpression(FieldDbType.String));
        }

        [Fact]
        [DisplayName("MySQL DialectFactory creates each pure SQL generator")]
        public void DialectFactory_CreatesAllBuilders()
        {
            var factory = new MySqlDialectFactory();

            // Only builders without external dependencies are checked: CREATE / ALTER / Rebuild produce plain strings
            // and need no connection or FormSchema lookup.
            Assert.NotNull(factory.CreateCreateTableCommandBuilder());
            Assert.NotNull(factory.CreateTableAlterCommandBuilder());
            Assert.NotNull(factory.CreateTableRebuildCommandBuilder());
            // `CreateTableSchemaProvider` constructs `new DbAccess(databaseId)` in its constructor. When CI has no
            // POLHEM_TEST_CONNSTR_MYSQL, 'common_mysql' is not registered in `DbConnectionManager` and it throws
            // `KeyNotFoundException`, so `MySqlIntegrationTests` covers it instead.
        }

        [Fact]
        [DisplayName("MySqlDialectFactory CreateFormCommandBuilder returns a MySqlFormCommandBuilder")]
        public void CreateFormCommandBuilder_ReturnsMySqlImpl()
        {
            var factory = new MySqlDialectFactory();
            var schema = new FormSchema("Foo", "Foo");
            var defineAccess = _fx.GetRequiredService<IDefineAccess>();

            var builder = factory.CreateFormCommandBuilder(schema, defineAccess);

            Assert.IsType<MySqlFormCommandBuilder>(builder);
        }
    }
}
