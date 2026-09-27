using System.ComponentModel;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Definition.Settings;

namespace Polhem.Db.UnitTests.Manager
{
    /// <summary>
    /// Tests for constructing <c>DbAccessFactory</c> and resolving the database type.
    /// </summary>
    /// <remarks>
    /// Uses its own isolated <see cref="DatabaseSettings"/> and does not touch the process-wide definition cache.
    /// The reason is in <see cref="IsolatedDatabaseSettingsProvider"/>. No connection is opened here; the tests only
    /// check that the <c>DbAccess</c> returned by the factory carries the right <c>DatabaseType</c>.
    /// </remarks>
    public sealed class DbAccessFactoryTests : IDisposable
    {
        private readonly IsolatedDatabaseSettingsProvider _provider = new();
        private readonly DbConnectionManagerService _manager;

        public DbAccessFactoryTests()
        {
            TestDbProviders.EnsureSqlServerRegistered();
            _manager = new DbConnectionManagerService(_provider);
        }

        public void Dispose() => _manager.Dispose();

        [Fact]
        [DisplayName("DbAccessFactory constructor requires an IDbConnectionManager")]
        public void DbAccessFactory_NullManager_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new DbAccessFactory(null!));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(30)]
        [InlineData(120)]
        [DisplayName("DbAccessFactory creates an instance with a maxCommandTimeout")]
        public void DbAccessFactory_WithTimeout_CreatesInstance(int timeout)
        {
            var factory = new DbAccessFactory(_manager, timeout, anomalyWriterFactory: null, anomalyOptions: null);
            Assert.NotNull(factory);
        }

        [Fact]
        [DisplayName("DbAccessFactory.Create returns a DbAccess for the matching DatabaseType")]
        public void Create_ValidDatabaseId_ReturnsDbAccessWithCorrectType()
        {
            string id = $"polhem_factory_{Guid.NewGuid():N}";
            _provider.Settings.Items!.Add(new DatabaseItem
            {
                Id = id,
                DatabaseType = DatabaseType.SQLServer,
                ConnectionString = "Server=test;"
            });

            var factory = new DbAccessFactory(_manager, 30, anomalyWriterFactory: null, anomalyOptions: null);
            var dbAccess = factory.Create(id);

            Assert.NotNull(dbAccess);
            Assert.Equal(DatabaseType.SQLServer, dbAccess.DatabaseType);
        }
    }
}
