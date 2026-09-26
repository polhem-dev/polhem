using Polhem.Db;
using Polhem.Db.CacheNotify;
using Polhem.Db.Manager;
using Polhem.Definition.Storage;
using Polhem.Repository.Abstractions;

namespace Polhem.Repository
{
    /// <summary>
    /// Construction-time context handed to every repository. The data-access counterpart of
    /// <see cref="Polhem.Definition.IPolhemContext"/>: it aggregates the cross-cutting services a repository needs so that
    /// every repository can share one constructor signature.
    /// </summary>
    /// <remarks>
    /// Lives in <c>Polhem.Repository</c> rather than <c>Polhem.Repository.Abstractions</c> because its
    /// members are <c>Polhem.Db</c> types. Consumers only ever name <see cref="Polhem.Repository.Abstractions.Factories.IRepositoryFactory"/>, which
    /// does stay in the abstractions package, so <c>Polhem.Business</c> and <c>Polhem.ObjectCaching</c>
    /// keep their present dependencies. Anything that names this interface — a repository, or a
    /// host writing its own — already references <c>Polhem.Repository</c> for the base class.
    /// </remarks>
    public interface IRepositoryContext
    {
        /// <summary>The definition data access service (FormSchema / TableSchema lookups).</summary>
        IDefineAccess DefineAccess { get; }

        /// <summary>The connection manager (dialect + connection resolution).</summary>
        IDbConnectionManager ConnectionManager { get; }

        /// <summary>The database access factory.</summary>
        IDbAccessFactory DbAccessFactory { get; }

        /// <summary>Resolves a logical scope to a physical database id.</summary>
        IRepositoryDatabaseRouter Router { get; }

        /// <summary>
        /// Cross-process cache invalidation channel; <c>null</c> when the host does not poll it.
        /// </summary>
        /// <remarks>
        /// Held here rather than injected only into the one repository that writes cache-backed data.
        /// It is nullable and unused by default, so carrying it costs nothing, whereas a special
        /// constructor for its single consumer would defeat the uniform signature this context
        /// exists to enable. Which repositories actually use it is a grep away.
        /// </remarks>
        ICacheNotifyService? CacheNotify { get; }

        /// <summary>Escape hatch for services not in the typed core members. Use sparingly.</summary>
        IServiceProvider Services { get; }
    }
}
