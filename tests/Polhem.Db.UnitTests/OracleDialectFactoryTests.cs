using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Db.Providers.Oracle;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;
using Polhem.Db.Manager;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Oracle provider/dialect smoke tests. Verifies that the test fixture
    /// (<see cref="SharedDatabaseState.EnsureRegistered"/>, run once per process) registers both the ADO.NET
    /// provider factory and the dialect factory at startup, so subsequent Oracle
    /// builder/integration tests can resolve them via the registries.
    /// </summary>
    /// <remarks>
    /// Only the factory wiring is validated here — that each create-builder method
    /// returns the Oracle-specific implementation. The builders' own SQL output is
    /// covered by their respective test classes.
    /// </remarks>
    public class OracleDialectFactoryTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public OracleDialectFactoryTests(SharedDbFixture fx) { _fx = fx; }
        [Fact]
        [DisplayName("Oracle DialectFactory is registered through DbDialectRegistry")]
        public void DialectFactory_IsRegistered()
        {
            var factory = DbDialectRegistry.Get(DatabaseType.Oracle);

            Assert.NotNull(factory);
            Assert.IsType<OracleDialectFactory>(factory);
        }

        [Fact]
        [DisplayName("Oracle ADO.NET provider is registered through DbProviderRegistry")]
        public void Provider_IsRegistered()
        {
            var factory = DbProviderRegistry.Get(DatabaseType.Oracle);

            Assert.Same(global::Oracle.ManagedDataAccess.Client.OracleClientFactory.Instance, factory);
        }

        [Fact]
        [DisplayName("Oracle DbProviderRegistry registers a connection-open initializer")]
        public void Provider_ConnectionInitializer_IsRegistered()
        {
            var initializer = DbProviderRegistry.GetConnectionInitializer(DatabaseType.Oracle);

            // `SharedDatabaseState.RegisterOracle` attaches the ALTER SESSION action. Only the hook's presence is checked.
            // The integration tests cover the actual execution when POLHEM_TEST_CONNSTR_ORACLE is enabled.
            Assert.NotNull(initializer);
        }

        [Fact]
        [DisplayName("Oracle DialectFactory GetDefaultValueExpression returns the matching expressions")]
        public void DialectFactory_DefaultValueExpression_ReturnsExpected()
        {
            var factory = new OracleDialectFactory();

            Assert.Equal("SYS_GUID()", factory.GetDefaultValueExpression(FieldDbType.Guid));
            Assert.Equal("SYS_EXTRACT_UTC(SYSTIMESTAMP)", factory.GetDefaultValueExpression(FieldDbType.DateTime));
            Assert.Equal("0", factory.GetDefaultValueExpression(FieldDbType.Integer));
            Assert.Equal(string.Empty, factory.GetDefaultValueExpression(FieldDbType.String));
        }

        [Fact]
        [DisplayName("Oracle DialectFactory creates each pure SQL generator")]
        public void DialectFactory_CreatesAllBuilders()
        {
            var factory = new OracleDialectFactory();

            // Only builders without external dependencies are checked: CREATE / ALTER / Rebuild produce plain strings
            // and need no connection or FormSchema lookup.
            Assert.IsType<OracleCreateTableCommandBuilder>(factory.CreateCreateTableCommandBuilder());
            Assert.IsType<OracleTableAlterCommandBuilder>(factory.CreateTableAlterCommandBuilder());
            Assert.IsType<OracleTableRebuildCommandBuilder>(factory.CreateTableRebuildCommandBuilder());
            // `CreateTableSchemaProvider` depends on a databaseId as in MySQL, so the integration tests cover it.
        }

        [Fact]
        [DisplayName("OracleDialectFactory CreateFormCommandBuilder returns an OracleFormCommandBuilder")]
        public void CreateFormCommandBuilder_ReturnsOracleImpl()
        {
            var factory = new OracleDialectFactory();
            var schema = new FormSchema("Foo", "Foo");
            var defineAccess = _fx.GetRequiredService<IDefineAccess>();

            var builder = factory.CreateFormCommandBuilder(schema, defineAccess);

            Assert.IsType<OracleFormCommandBuilder>(builder);
        }
    }
}
