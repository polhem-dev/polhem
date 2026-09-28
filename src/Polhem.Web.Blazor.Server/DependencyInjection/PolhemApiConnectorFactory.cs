using Polhem.Api.Client;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Client.Definitions;

namespace Polhem.Web.Blazor.Server.DependencyInjection
{
    /// <summary>
    /// Builds <see cref="FormApiConnector"/> / <see cref="SystemApiConnector"/>
    /// instances honouring the <see cref="PolhemBlazorOptions"/> chosen at startup.
    /// </summary>
    /// <remarks>
    /// Registered as <b>scoped</b> by
    /// <see cref="PolhemBlazorServiceCollectionExtensions.AddPolhemBlazor"/> — one per circuit — and
    /// connectors are cheap to allocate per call so callers should construct one per logical
    /// operation rather than caching.
    /// <para>
    /// WARNING: The scope is what makes <see cref="ApiSessionContext"/> per-user. It was a singleton
    /// before, which meant every circuit shared one transmission key: in Remote mode the last login
    /// overwrote the rest, and the earlier users' encrypted requests failed to decrypt until they
    /// signed in again. Do not register this as a singleton.
    /// </para>
    /// </remarks>
    public class PolhemApiConnectorFactory
    {
        private readonly PolhemBlazorOptions _options;
        private readonly ApiSessionContext _session;
        private readonly IServiceProvider _services;

        /// <summary>
        /// Initializes a new instance of <see cref="PolhemApiConnectorFactory"/> for one session.
        /// </summary>
        /// <param name="options">The resolved Blazor options.</param>
        /// <param name="session">The per-circuit session state handed to every connector it creates.</param>
        /// <param name="services">
        /// The host's service provider. In <see cref="PolhemBlazorProviderMode.Local"/> mode it is the
        /// in-process backend that local connectors dispatch to, so the host must have called
        /// <c>AddPolhemFramework</c>.
        /// </param>
        public PolhemApiConnectorFactory(PolhemBlazorOptions options, ApiSessionContext session, IServiceProvider services)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(services);
            _options = options;
            _session = session;
            _services = services;
        }

        /// <summary>
        /// Gets the resolved provider mode.
        /// </summary>
        public PolhemBlazorProviderMode Mode => _options.Mode;

        /// <summary>
        /// Gets whether components assemble their definitions through
        /// <see cref="CreateDefinitionLoader"/> by default; see
        /// <see cref="PolhemBlazorOptions.UseDefinitionLoader"/>.
        /// </summary>
        public bool UseDefinitionLoader => _options.UseDefinitionLoader;

        /// <summary>
        /// Creates the <see cref="FormDefinitionLoader"/> a component assembles its runtime
        /// definitions through: a schema localized in the requested language and the tenant's
        /// layout, fetched through a <see cref="SystemApiConnector"/> from
        /// <see cref="CreateSystemConnector"/>.
        /// </summary>
        /// <param name="accessToken">
        /// The session access token; pass <see cref="Guid.Empty"/> for anonymous calls.
        /// </param>
        /// <remarks>
        /// <para>
        /// Each call builds a loader over a fresh <see cref="ClientDefineAccess"/>, so its definition
        /// cache lives as long as the component that holds it. A page opened after the user enters
        /// another company therefore fetches that tenant's customization, with no cache to flush.
        /// </para>
        /// <para>
        /// The loader has no company accessor, so number formats are baked with the framework's
        /// default decimal places. A host that wants the entered company's decimals passes its own
        /// loader to the page, with <see cref="FormDefinitionLoader.CompanyAccessor"/> set.
        /// </para>
        /// </remarks>
        public virtual FormDefinitionLoader CreateDefinitionLoader(Guid accessToken)
            => new(new ClientDefineAccess(CreateSystemConnector(accessToken)));

        /// <summary>
        /// Creates a <see cref="FormApiConnector"/> for the given progId and access token.
        /// </summary>
        /// <param name="accessToken">
        /// The session access token; pass <see cref="Guid.Empty"/> for anonymous calls
        /// (the BO method must declare <see cref="Polhem.Definition.Security.ApiAccessRequirement.Anonymous"/>).
        /// </param>
        /// <param name="progId">The program identifier (e.g. "Employee").</param>
        public virtual FormApiConnector CreateFormConnector(Guid accessToken, string progId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(progId);
            return _options.Mode == PolhemBlazorProviderMode.Local
                ? new FormApiConnector(_services, accessToken, progId, _session)
                : new FormApiConnector(_options.Endpoint, accessToken, progId, _session);
        }

        /// <summary>
        /// Creates a <see cref="SystemApiConnector"/> for the given access token.
        /// </summary>
        /// <param name="accessToken">
        /// The session access token; pass <see cref="Guid.Empty"/> for anonymous calls.
        /// </param>
        public virtual SystemApiConnector CreateSystemConnector(Guid accessToken)
        {
            return _options.Mode == PolhemBlazorProviderMode.Local
                ? new SystemApiConnector(_services, accessToken, _session)
                : new SystemApiConnector(_options.Endpoint, accessToken, _session);
        }
    }
}
