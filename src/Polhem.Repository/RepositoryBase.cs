using Microsoft.Extensions.DependencyInjection;
using Polhem.Db;
using Polhem.Definition;
using Polhem.Definition.Identity;

namespace Polhem.Repository
{
    /// <summary>
    /// Base class for every repository. Carries the shared construction context and resolves the
    /// target database once, so framework repositories no longer each repeat the same
    /// wiring.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The uniform constructor is what makes the registry work.</b> A repository can be created
    /// from a type name only if every repository is created the same way — the same property that
    /// lets <c>BusinessObject</c> be built by the business object factory.
    /// </para>
    /// <para>
    /// <b>Routing lives here, not in the factory.</b> Before this, the target database was reached
    /// three different ways: injected by the factory (form repositories), hard-coded per method
    /// (the common-scope ones), or handed in by the caller. Declaring the scope on the type makes
    /// it one thing, and one that can be read off the class rather than hunted for among its
    /// method bodies.
    /// </para>
    /// </remarks>
    public abstract class RepositoryBase
    {
        /// <summary>
        /// Initializes a new <see cref="RepositoryBase"/> and eagerly resolves
        /// <see cref="DatabaseId"/> when the scope calls for it.
        /// </summary>
        /// <param name="ctx">The shared construction context.</param>
        /// <param name="accessToken">The current request's access token; <see cref="Guid.Empty"/> outside a request.</param>
        /// <param name="progId">The program identifier; empty for the framework axis.</param>
        /// <param name="scope">
        /// The logical database this repository reads and writes, or <c>null</c> when it has none of
        /// its own and every method is told which database to use.
        /// </param>
        /// <exception cref="Polhem.Core.Exceptions.AuthenticationRequiredException">Thrown when the scope is company-bound and the session is missing or expired.</exception>
        /// <exception cref="Polhem.Core.Exceptions.UserMessageException">Thrown when the scope is company-bound and the session's company is unavailable.</exception>
        /// <exception cref="Polhem.Core.Exceptions.CompanyNotEnteredException">Thrown when the scope is company-bound and the session has not entered a company.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the scope is company-bound and no <see cref="ISessionInfoService"/> is registered, or the session entered another company while the repository was being built.</exception>
        /// <remarks>
        /// The resolution is eager so a bad route fails while the repository is being built, before
        /// any data has been touched — the timing the form factory already had. Scope is a
        /// constructor parameter rather than an overridable property so nothing virtual is called
        /// during construction. A company-bound repository also takes <see cref="CompanyId"/> from
        /// the session here, the same session the router read the company database from.
        /// </remarks>
        protected RepositoryBase(IRepositoryContext ctx, Guid accessToken, string progId, DbScope? scope)
        {
            Context = ctx ?? throw new ArgumentNullException(nameof(ctx));
            AccessToken = accessToken;
            ProgId = progId ?? string.Empty;
            Scope = scope;
            if (scope != DbScope.Company)
            {
                DatabaseId = scope is { } value ? ctx.Router.Resolve(value, accessToken) : string.Empty;
                CompanyId = string.Empty;
                return;
            }

            // WARNING: The router reads the session's company to pick the database, and the company is
            // read here again to confine the rows. An EnterCompany on the same token in between would
            // pair one company's database with another company's id, so the company is read on both
            // sides of the routing and a change refuses the request rather than misfiling its rows.
            var companyBefore = ResolveSessionCompanyId(ctx, accessToken);
            DatabaseId = ctx.Router.Resolve(DbScope.Company, accessToken);
            CompanyId = ResolveSessionCompanyId(ctx, accessToken);
            if (!string.Equals(companyBefore, CompanyId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The session entered another company while the request was being routed; retry the request.");
            }
        }

        /// <summary>
        /// Initializes a new <see cref="RepositoryBase"/> bound to an explicit database, skipping
        /// the router entirely.
        /// </summary>
        /// <param name="ctx">The shared construction context.</param>
        /// <param name="progId">The program identifier; empty for the framework axis.</param>
        /// <param name="databaseId">The database this repository reads and writes.</param>
        /// <remarks>
        /// For callers that already know the target database and have no token to route with, and
        /// for tests that would otherwise need a working router to build a repository at all. A
        /// separate constructor rather than an optional parameter, so the two construction paths are
        /// distinguishable at the call site.
        /// </remarks>
        protected RepositoryBase(IRepositoryContext ctx, string progId, string databaseId)
        {
            Context = ctx ?? throw new ArgumentNullException(nameof(ctx));
            ArgumentException.ThrowIfNullOrWhiteSpace(databaseId);
            AccessToken = Guid.Empty;
            ProgId = progId ?? string.Empty;
            Scope = null;
            DatabaseId = databaseId;
            CompanyId = string.Empty;
        }

        /// <summary>
        /// Initializes a new <see cref="RepositoryBase"/> bound to an explicit database and company,
        /// skipping the router and the session.
        /// </summary>
        /// <param name="ctx">The shared construction context.</param>
        /// <param name="progId">The program identifier; empty for the framework axis.</param>
        /// <param name="databaseId">The database this repository reads and writes.</param>
        /// <param name="companyId">The company whose rows this repository reads and writes.</param>
        /// <remarks>
        /// For callers that already know the target database and company and have no session to
        /// read them from, such as a cache loading one company's data.
        /// </remarks>
        protected RepositoryBase(IRepositoryContext ctx, string progId, string databaseId, string companyId)
            : this(ctx, progId, databaseId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(companyId);
            CompanyId = CompanyInfo.NormalizeId(companyId);
        }

        /// <summary>Gets the shared construction context.</summary>
        protected IRepositoryContext Context { get; }

        /// <summary>Gets the access token this repository was built for.</summary>
        protected Guid AccessToken { get; }

        /// <summary>Gets the program identifier; empty on the framework axis.</summary>
        public string ProgId { get; }

        /// <summary>
        /// Gets the logical database this repository is bound to, or <c>null</c> when its methods
        /// each take a database id instead.
        /// </summary>
        protected DbScope? Scope { get; }

        /// <summary>
        /// Gets the resolved physical database id, or an empty string when <see cref="Scope"/> is
        /// <c>null</c> and the caller supplies one per method.
        /// </summary>
        protected string DatabaseId { get; }

        /// <summary>
        /// Gets the company this repository is bound to, in its stored (lowercase) form, or an empty
        /// string when it is bound to none.
        /// </summary>
        /// <remarks>
        /// Set for a company-bound <see cref="Scope"/> from the session, or by the constructor that
        /// names a company. It is the value company rows are filtered on and stamped with
        /// (<see cref="SysFields.CompanyId"/>).
        /// </remarks>
        protected string CompanyId { get; }

        private static string ResolveSessionCompanyId(IRepositoryContext ctx, Guid accessToken)
        {
            var sessionService = ctx.Services.GetService<ISessionInfoService>()
                ?? throw new InvalidOperationException(
                    $"A company-bound repository needs an {nameof(ISessionInfoService)} to read the session's company.");
            // The router has already refused a missing session and one without a company, so both
            // are present here unless the session ended in between; an empty id then binds nothing
            // and the company-bound repositories refuse to run.
            var companyId = sessionService.Get(accessToken)?.CompanyId;
            return string.IsNullOrEmpty(companyId) ? string.Empty : CompanyInfo.NormalizeId(companyId);
        }

        /// <summary>
        /// Creates a <see cref="DbAccess"/> against this repository's resolved database.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when this repository has no database of its own.</exception>
        protected DbAccess CreateDbAccess()
        {
            if (string.IsNullOrEmpty(DatabaseId))
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} is not bound to a database; call the overload that takes a databaseId.");
            }
            return new DbAccess(DatabaseId, Context.ConnectionManager);
        }

        /// <summary>
        /// Creates a <see cref="DbAccess"/> against the supplied database.
        /// </summary>
        /// <param name="databaseId">The target database id.</param>
        protected DbAccess CreateDbAccess(string databaseId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(databaseId);
            return new DbAccess(databaseId, Context.ConnectionManager);
        }
    }
}
