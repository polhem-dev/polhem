using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Identity;
using Polhem.Definition.Storage;
using Polhem.Repository.Abstractions;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Factories;

namespace Polhem.Tests.Shared
{
    /// <summary>
    /// Builds the framework's own <see cref="RepositoryFactory"/> over a connection manager a test set up, for code
    /// that takes its framework repositories from a factory, such as the database initializer.
    /// </summary>
    /// <remarks>
    /// The common and log scopes route to the database items named <c>common</c> and <c>log</c>, as the framework's
    /// router does. There is no session, so the company scope is refused.
    /// </remarks>
    public static class TestRepositoryFactory
    {
        /// <summary>Builds the factory.</summary>
        /// <param name="defineAccess">The definition access the repositories read schemas from.</param>
        /// <param name="connectionManager">The connection manager whose database items the repositories use.</param>
        public static IRepositoryFactory Create(IDefineAccess defineAccess, IDbConnectionManager connectionManager)
            => new RepositoryFactory(
                TestRepositoryContext.CreateServices(),
                defineAccess,
                new DbAccessFactory(connectionManager),
                connectionManager,
                new FrameworkScopeRouter(),
                new ProgramSettingsRepositoryTypeResolver(defineAccess));

        private sealed class FrameworkScopeRouter : IRepositoryDatabaseRouter
        {
            public string Resolve(DbScope scope, Guid accessToken) => Resolve(scope, SessionCompanyScope.None);

            public string Resolve(DbScope scope, SessionCompanyScope companyScope) => scope switch
            {
                DbScope.Common => DbCategoryIds.Common,
                DbScope.Log => DbCategoryIds.Log,
                _ => throw new InvalidOperationException("A test repository factory has no session to route a company with."),
            };
        }
    }
}
