using Polhem.Db;
using Polhem.Db.CacheNotify;
using Polhem.Db.Manager;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Storage;
using Polhem.Repository;
using Polhem.Repository.Abstractions;

namespace Polhem.Tests.Shared
{
    /// <summary>
    /// Creates an <see cref="IRepositoryContext"/> for tests.
    /// </summary>
    /// <remarks>
    /// Repository constructors take <c>(ctx, accessToken, progId)</c>, so tests no longer inject services one by
    /// one. Most tests care about one or two members; the rest can be placeholders that are never touched.
    /// </remarks>
    public static class TestRepositoryContext
    {
        /// <summary>
        /// Builds a complete context from the fixture's DI container.
        /// </summary>
        /// <param name="fixture">The fixture that provides the framework services.</param>
        public static IRepositoryContext Create(PolhemTestFixture fixture)
        {
            ArgumentNullException.ThrowIfNull(fixture);
            return new RepositoryContext
            {
                DefineAccess = fixture.GetRequiredService<IDefineAccess>(),
                ConnectionManager = fixture.GetRequiredService<IDbConnectionManager>(),
                DbAccessFactory = fixture.GetRequiredService<IDbAccessFactory>(),
                Router = fixture.GetRequiredService<IRepositoryDatabaseRouter>(),
                CacheNotify = fixture.GetService<ICacheNotifyService>(),
                Services = fixture.Provider,
            };
        }

        /// <summary>
        /// Builds a context from the parts the caller supplies; parts left out are filled with placeholders that
        /// are not expected to be used.
        /// </summary>
        /// <param name="connectionManager">The connection manager.</param>
        /// <param name="defineAccess">The definition access service.</param>
        /// <param name="dbAccessFactory">The database access factory.</param>
        /// <param name="router">The database router.</param>
        /// <param name="cacheNotify">The cross-process cache invalidation channel.</param>
        /// <param name="services">The service provider.</param>
        public static IRepositoryContext Create(
            IDbConnectionManager? connectionManager = null,
            IDefineAccess? defineAccess = null,
            IDbAccessFactory? dbAccessFactory = null,
            IRepositoryDatabaseRouter? router = null,
            ICacheNotifyService? cacheNotify = null,
            IServiceProvider? services = null)
            => new RepositoryContext
            {
                ConnectionManager = connectionManager!,
                DefineAccess = defineAccess!,
                DbAccessFactory = dbAccessFactory!,
                Router = router ?? new FixedRouter(),
                CacheNotify = cacheNotify,
                Services = services ?? EmptyServiceProvider.Instance,
            };

        /// <summary>
        /// Gets an <see cref="IServiceProvider"/> that resolves nothing, for tests that only need to satisfy a signature.
        /// </summary>
        public static IServiceProvider CreateServices() => EmptyServiceProvider.Instance;

        /// <summary>
        /// Default router: Common and Log return fixed database ids like the production
        /// <c>RepositoryDatabaseRouter</c>; Company returns a test id because there is no session to look up.
        /// </summary>
        /// <remarks>
        /// Common and Log must match the production router. Otherwise a repository that declares Common scope, such
        /// as <c>SessionRepository</c>, is routed to a database that does not exist, and the test sees a KeyNotFound
        /// error instead of the behavior it means to check.
        /// </remarks>
        private sealed class FixedRouter : IRepositoryDatabaseRouter
        {
            public string Resolve(DbScope scope, Guid accessToken) => scope switch
            {
                DbScope.Common => DbCategoryIds.Common,
                DbScope.Log => DbCategoryIds.Log,
                _ => "testdb",
            };
        }

        private sealed class EmptyServiceProvider : IServiceProvider
        {
            public static readonly EmptyServiceProvider Instance = new();
            public object? GetService(Type serviceType) => null;
        }
    }
}
