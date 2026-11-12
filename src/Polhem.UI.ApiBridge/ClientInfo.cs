using Polhem.Api.Client;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Client.Definitions;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;

namespace Polhem.UI.ApiBridge
{
    /// <summary>
    /// Provides client-side connection state and access to API connectors.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WARNING: this holds <b>one signed-in user's</b> state in process-wide statics — the access
    /// token, the capability snapshot, the entered company, and a definition cache carrying that
    /// user's tenant customization. That is correct for a desktop head, where one process serves one
    /// user, and <b>wrong for a host that serves several users from one process</b>: every session
    /// would write the same fields, so the last sign-in wins and earlier users would read someone
    /// else's identity.
    /// </para>
    /// <para>
    /// This is a stated limitation, not an oversight — but nothing in the type system marks the
    /// boundary, so a server-side UI head built on this package inherits the defect silently.
    /// <c>Polhem.Api.Client</c> already went through this: the connection and the signed-in identity
    /// live in a <see cref="PolhemApiClient"/>, one per user. This class holds exactly one of them,
    /// <see cref="ApiClient"/>; a multi-user head creates its own clients instead of using this type.
    /// </para>
    /// <para>
    /// Deployment-level values are a different matter and belong exactly where they are:
    /// <see cref="EndpointStorage"/> and <see cref="ApiKeyStorage"/> identify the application, not
    /// a user, so making them per-session would be wrong rather than safer.
    /// </para>
    /// </remarks>
    public static partial class ClientInfo
    {
        /// <summary>
        /// Guards the lazily created singletons and the resets that clear them.
        /// </summary>
        /// <remarks>
        /// The getters are reached from the UI thread and from continuations of
        /// <c>ConfigureAwait(false)</c> awaits, so two threads can enter the same <c>??=</c> at once.
        /// The wasted instance is not the problem — the orphan is: <see cref="ResetDefineCache"/>
        /// would clear whichever one it happens to hold while callers keep using the other, and a
        /// tenant switch would leave the old customization in play.
        /// <para>
        /// A <see cref="Lazy{T}"/> would not fit: these are deliberately resettable (a new access
        /// token drops the connector and the definition cache), and replacing the <c>Lazy</c>
        /// instance only moves the race.
        /// </para>
        /// </remarks>
        private static readonly Lock s_stateGate = new();

        private static readonly FileEndpointStorage s_defaultStorage = new(FileEndpointStorage.DefaultAppName);

        private static PolhemApiClient? s_client;
        private static string s_apiKey = string.Empty;
        private static ClientDefineAccess? s_defineAccess;
        private static FormDefinitionLoader? s_definitionLoader;
        private static IReadOnlyDictionary<string, PermissionActions>? s_capabilities;
        private static CompanyInfo? s_company;

        /// <summary>
        /// Command-line arguments parsed at <see cref="InitializeAsync(SupportedConnectTypes, CancellationToken)"/>.
        /// </summary>
        public static IReadOnlyDictionary<string, string>? Arguments { get; private set; }

        /// <summary>
        /// Gets or sets where the service endpoint is persisted.
        /// </summary>
        /// <remarks>
        /// Defaults to a <see cref="FileEndpointStorage"/> that keeps the endpoint under the per-user local
        /// application data directory, in a folder named after the entry assembly. It is the same instance as the
        /// default <see cref="ApiKeyStorage"/>. A browser WASM host has no persistent file system and replaces
        /// both; see <see cref="FileEndpointStorage"/>. Assign it before
        /// <see cref="InitializeAsync(SupportedConnectTypes, CancellationToken)"/> or <see cref="SetEndpointAsync(string, CancellationToken)"/>.
        /// </remarks>
        public static IEndpointStorage EndpointStorage { get; set; } = s_defaultStorage;

        /// <summary>
        /// Gets or sets where the API key is persisted.
        /// </summary>
        /// <remarks>
        /// Defaults to the same <see cref="FileEndpointStorage"/> instance as <see cref="EndpointStorage"/>, which
        /// keeps the key beside the endpoint in a file only its owner can read. A host that replaces
        /// <see cref="EndpointStorage"/> because its platform cannot use the file system (browser WASM) replaces
        /// this too.
        /// </remarks>
        public static IApiKeyStorage ApiKeyStorage { get; set; } = s_defaultStorage;

        /// <summary>
        /// Gets the client this head calls the backend with.
        /// </summary>
        /// <remarks>
        /// Replaced whenever the endpoint changes (<see cref="SetEndpointAsync"/>, <see cref="InitializeAsync(string, CancellationToken)"/>),
        /// which also signs out: a new connection always needs a fresh sign-in. Before any endpoint is set it is an
        /// in-process client over <see cref="LocalServiceProvider"/>.
        /// </remarks>
        public static PolhemApiClient ApiClient
        {
            get
            {
                lock (s_stateGate) { return s_client ??= CreateLocalClient(); }
            }
        }

        /// <summary>
        /// Gets the access token of the signed-in user; <see cref="Guid.Empty"/> before sign-in.
        /// </summary>
        public static Guid AccessToken => ApiClient.Session.Credentials.AccessToken;

        /// <summary>
        /// Gets how <see cref="ApiClient"/> reaches the backend.
        /// </summary>
        public static ConnectType ConnectType => ApiClient.IsLocal ? ConnectType.Local : ConnectType.Remote;

        /// <summary>
        /// Gets or sets the connection types this application allows.
        /// </summary>
        /// <remarks>
        /// <see cref="InitializeAsync(SupportedConnectTypes, CancellationToken)"/> sets it from its
        /// argument. An endpoint of another type is refused when it is set.
        /// </remarks>
        public static SupportedConnectTypes SupportedConnectTypes { get; set; } = SupportedConnectTypes.Both;

        /// <summary>
        /// System-level API connector of <see cref="ApiClient"/>.
        /// </summary>
        public static SystemApiConnector SystemApiConnector => ApiClient.System;

        /// <summary>
        /// Gets or sets the in-process backend's service provider, which <see cref="ApiClient"/> dispatches to when
        /// <see cref="ConnectType"/> is <see cref="ConnectType.Local"/>.
        /// </summary>
        /// <remarks>
        /// A head that runs the backend in its own process assigns the provider built by
        /// <c>services.AddPolhemFramework(...)</c> before connecting. A remote-only head leaves it
        /// <c>null</c>. It lives here, with the rest of this head's process-wide state, rather than
        /// in <c>Polhem.Api.Client</c>, whose clients take the provider as an argument.
        /// </remarks>
        public static IServiceProvider? LocalServiceProvider { get; set; }

        // The provider is read when a call is dispatched, not when the client is created: the client exists before
        // anything is sent (its getter is also read by code that never dispatches), and a head may assign the
        // provider after that. A missing provider is reported on the first call.
        private static PolhemApiClient CreateLocalClient()
        {
            var client = PolhemApiClient.CreateLocal(DeferredLocalServiceProvider.Instance);
            client.ApiKey = s_apiKey;
            return client;
        }

        private sealed class DeferredLocalServiceProvider : IServiceProvider
        {
            public static readonly DeferredLocalServiceProvider Instance = new();

            public object? GetService(Type serviceType)
                => (LocalServiceProvider ?? throw new InvalidOperationException(
                    "ClientInfo.LocalServiceProvider is not set. A local connection runs the backend in this process; " +
                    "assign the service provider built by services.AddPolhemFramework(...) before connecting."))
                    .GetService(serviceType);
        }

        /// <summary>
        /// Creates a form-level API connector for the specified program.
        /// </summary>
        /// <param name="progId">Program identifier.</param>
        public static FormApiConnector CreateFormApiConnector(string progId) => ApiClient.Form(progId);

        /// <summary>
        /// Creates an audit-log API connector (read-only queries over the <c>st_log_*</c> tables).
        /// </summary>
        public static AuditLogApiConnector CreateAuditLogApiConnector() => ApiClient.AuditLog;

        /// <summary>
        /// Definition-data accessor. Recreated whenever the endpoint changes or a user signs in.
        /// </summary>
        public static ClientDefineAccess DefineAccess
        {
            get
            {
                lock (s_stateGate) { return s_defineAccess ??= new ClientDefineAccess((s_client ??= CreateLocalClient()).System); }
            }
        }

        /// <summary>
        /// Gets or sets whether views that were not given a definition loader of their own assemble
        /// their definitions through <see cref="DefinitionLoader"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// One switch for the whole client: the record form, the list and the lookup dialog all consult
        /// it, and a view's own loader still wins. <c>true</c> — the default — gives every view
        /// customized layouts, localized captions and company number formats. <c>false</c> renders
        /// definitions exactly as stored, which saves the extra round trips the loader makes and needs
        /// nothing but the stored files.
        /// </para>
        /// <para>
        /// Views localize when they load. A language switch takes effect in views opened after it;
        /// a view already on screen keeps the language it was opened in until it is reopened.
        /// </para>
        /// </remarks>
        public static bool UseDefinitionLoader { get; set; } = true;

        /// <summary>
        /// Gets the client-wide definition loader, or <c>null</c> when
        /// <see cref="UseDefinitionLoader"/> is off.
        /// </summary>
        /// <remarks>
        /// Built over <see cref="DefineAccess"/> with the entered <see cref="Company"/> as its
        /// company accessor and the <see cref="PolhemApiClient.DefaultLanguage"/> of <see cref="ApiClient"/> as its
        /// default language, and discarded together with <see cref="DefineAccess"/> when the endpoint
        /// changes or a user signs in, so it never serves a previous identity's definitions.
        /// </remarks>
        public static FormDefinitionLoader? DefinitionLoader
        {
            get
            {
                if (!UseDefinitionLoader) { return null; }
                lock (s_stateGate)
                {
                    return s_definitionLoader ??= new FormDefinitionLoader(
                        s_defineAccess ??= new ClientDefineAccess((s_client ??= CreateLocalClient()).System))
                    {
                        CompanyAccessor = static () => Company,
                    };
                }
            }
        }

        /// <summary>
        /// Discards the locally cached definition data.
        /// </summary>
        /// <remarks>
        /// Called automatically by <see cref="ApplyEnterCompanyResult"/> and
        /// <see cref="ClearCompanyContext"/>, which is where a tenant switch actually happens —
        /// hosts do not need to remember it. Exposed for the rare case of discarding the cache
        /// without a tenant change. No-op when the accessor has not been created yet.
        /// <para>
        /// The flush matters because <see cref="ClientDefineAccess"/> keys its cache by
        /// progId / layoutId / namespace alone. The customization layer it holds belongs to whichever
        /// tenant was current when it was fetched, so entries that outlive the switch would serve the
        /// previous tenant's customization to the next one.
        /// </para>
        /// </remarks>
        public static void ResetDefineCache()
        {
            ClientDefineAccess? defineAccess;
            lock (s_stateGate) { defineAccess = s_defineAccess; }
            defineAccess?.ClearCache();
        }

        /// <summary>
        /// Whether <c>System.Settings.xml</c> and <c>Database.Settings.xml</c> should be auto-generated
        /// when the local endpoint is missing the expected files.
        /// </summary>
        public static bool AllowGenerateSettings { get; set; }

        /// <summary>
        /// Authenticated user information set by <see cref="ApplyLoginResult"/>.
        /// </summary>
        public static UserInfo? UserInfo { get; private set; }

    }
}
