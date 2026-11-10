using Polhem.Db;
using Polhem.Db.CacheNotify;
using Polhem.Db.Manager;
using Polhem.Definition.Storage;
using Polhem.Repository.Abstractions;

namespace Polhem.Repository
{
    /// <summary>
    /// Default <see cref="IRepositoryContext"/>. Built once by <see cref="Factories.RepositoryFactory"/>
    /// from its own injected services and handed to every repository it creates.
    /// </summary>
    /// <remarks>
    /// Holds application-lifetime services, plus the request's company scope snapshot on a copy made by
    /// <see cref="WithCompanyScope"/>. Other per-call state, the access token and the progId, stays in the
    /// constructor parameters beside it, so the factory's own instance is safely shared by every repository it builds.
    /// </remarks>
    public sealed class RepositoryContext : IRepositoryContext
    {
        /// <inheritdoc/>
        public required IDefineAccess DefineAccess { get; init; }

        /// <inheritdoc/>
        public required IDbConnectionManager ConnectionManager { get; init; }

        /// <inheritdoc/>
        public required IDbAccessFactory DbAccessFactory { get; init; }

        /// <inheritdoc/>
        public required IRepositoryDatabaseRouter Router { get; init; }

        /// <inheritdoc/>
        public ICacheNotifyService? CacheNotify { get; init; }

        /// <inheritdoc/>
        public required IServiceProvider Services { get; init; }

        /// <inheritdoc/>
        public Polhem.Definition.Identity.SessionCompanyScope? CompanyScope { get; init; }

        /// <summary>
        /// Returns a copy of this context that carries <paramref name="companyScope"/>, for the repositories of one
        /// request.
        /// </summary>
        /// <param name="companyScope">The request's snapshot of the session's company scope.</param>
        public RepositoryContext WithCompanyScope(Polhem.Definition.Identity.SessionCompanyScope companyScope)
            => new()
            {
                DefineAccess = DefineAccess,
                ConnectionManager = ConnectionManager,
                DbAccessFactory = DbAccessFactory,
                Router = Router,
                CacheNotify = CacheNotify,
                Services = Services,
                CompanyScope = companyScope ?? throw new ArgumentNullException(nameof(companyScope)),
            };
    }
}
