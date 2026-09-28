using System.ComponentModel;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Polhem.Definition.Database;
using Polhem.Db.Manager;

namespace Polhem.Db.UnitTests
{
    public class DbProviderRegistryTests
    {
        [Fact]
        [DisplayName("Register throws ArgumentNullException for a null factory")]
        public void Register_NullFactory_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                DbProviderRegistry.Register(DatabaseType.SQLServer, null!));
        }

        [Fact]
        [DisplayName("Get throws KeyNotFoundException for an unregistered type")]
        public void Get_UnregisteredType_Throws()
        {
            // `SharedDatabaseState.EnsureRegistered` registers every defined `DatabaseType`, so an integer outside the enum range serves as
            // a placeholder that is never registered.
            Assert.Throws<KeyNotFoundException>(() =>
                DbProviderRegistry.Get((DatabaseType)9999));
        }

        public class WithInitializedFixture : IClassFixture<Polhem.Tests.Shared.SharedDbFixture>
        {
            public WithInitializedFixture(Polhem.Tests.Shared.SharedDbFixture _) { }

            [Fact]
            [DisplayName("Get returns the matching factory for a registered type (registered by the fixture)")]
            public void Get_RegisteredType_ReturnsFactory()
            {
                var factory = DbProviderRegistry.Get(DatabaseType.SQLServer);

                Assert.NotNull(factory);
                Assert.IsType<DbProviderFactory>(factory, exactMatch: false);
            }

            [Fact]
            [DisplayName("Calling Register again replaces the old value with the new one")]
            public void Register_ReplacesExistingFactory()
            {
                // Remember the current factory so it can be restored after the test.
                var original = DbProviderRegistry.Get(DatabaseType.SQLServer);
                try
                {
                    DbProviderRegistry.Register(DatabaseType.SQLServer, SqlClientFactory.Instance);
                    var fetched = DbProviderRegistry.Get(DatabaseType.SQLServer);

                    Assert.Same(SqlClientFactory.Instance, fetched);
                }
                finally
                {
                    DbProviderRegistry.Register(DatabaseType.SQLServer, original);
                }
            }
        }
    }
}
