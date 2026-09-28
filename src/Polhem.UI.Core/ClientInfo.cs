using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Polhem.Api.Client;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Client.Definitions;
using Polhem.Api.Core.Messages.System;
using Polhem.Base;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;
using System.Net.Sockets;

namespace Polhem.UI.Core
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
    /// <c>Polhem.Api.Client</c> already went through this: its per-user statics moved to
    /// <see cref="ApiSessionContext"/>, one instance per session. A multi-user head needs the same
    /// treatment here before using this type.
    /// </para>
    /// <para>
    /// Deployment-level values are a different matter and belong exactly where they are:
    /// <see cref="EndpointStorage"/> and <see cref="ApiKeyStorage"/> identify the application, not
    /// a user, so making them per-session would be wrong rather than safer.
    /// </para>
    /// </remarks>
    public static class ClientInfo
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

        private static SystemApiConnector? s_systemConnector;
        private static ClientDefineAccess? s_defineAccess;
        private static FormDefinitionLoader? s_definitionLoader;
        private static Guid s_accessToken = Guid.Empty;
        private static IReadOnlyDictionary<string, PermissionActions>? s_capabilities;
        private static CompanyInfo? s_company;

        /// <summary>
        /// Command-line arguments parsed at <see cref="InitializeAsync(IUIViewService, SupportedConnectTypes, CancellationToken)"/>.
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
        /// <see cref="InitializeAsync(IUIViewService, SupportedConnectTypes, CancellationToken)"/> or <see cref="SetEndpointAsync(string, CancellationToken)"/>.
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
        /// Access token issued on a successful login.
        /// </summary>
        public static Guid AccessToken
        {
            get { return s_accessToken; }
            private set
            {
                // NOTE: resetting the access token has to clear the `SystemApiConnector` and
                // `ClientDefineAccess` caches with it. Otherwise later calls carry the old token
                // and fail against the server.
                if (value != s_accessToken)
                {
                    // One identity change, one visible step: a reader must not be able to catch a
                    // new token paired with the previous identity's capability snapshot.
                    lock (s_stateGate)
                    {
                        s_accessToken = value;
                        s_systemConnector = null;
                        s_defineAccess = null;
                        s_definitionLoader = null;
                        // A new (or cleared) token means a different identity — the cached capability
                        // snapshot no longer applies. Reset to null so degradation is disabled until
                        // the next EnterCompany populates it.
                        s_capabilities = null;
                    }
                }
            }
        }

        /// <summary>
        /// System-level API connector. Recreated whenever the endpoint changes.
        /// </summary>
        public static SystemApiConnector SystemApiConnector
        {
            get
            {
                lock (s_stateGate) { return s_systemConnector ??= CreateSystemApiConnector(); }
            }
        }

        private static SystemApiConnector CreateSystemApiConnector()
        {
            return ApiClientInfo.ConnectType == ConnectType.Local
                ? new SystemApiConnector(LocalServicesForConnector(), AccessToken)
                : new SystemApiConnector(ApiClientInfo.Endpoint, AccessToken);
        }

        /// <summary>
        /// Gets or sets the in-process backend's service provider, which connectors use when
        /// <see cref="ApiClientInfo.ConnectType"/> is <see cref="ConnectType.Local"/>.
        /// </summary>
        /// <remarks>
        /// A head that runs the backend in its own process assigns the provider built by
        /// <c>services.AddPolhemFramework(...)</c> before connecting. A remote-only head leaves it
        /// <c>null</c>. It lives here, with the rest of this head's process-wide state, rather than
        /// in <c>Polhem.Api.Client</c>, whose connectors take the provider as a constructor argument.
        /// </remarks>
        public static IServiceProvider? LocalServiceProvider { get; set; }

        // A connector is created before anything is sent (the getter is also read by code that never
        // dispatches), so a missing provider is reported on the first call rather than on creation.
        private static IServiceProvider LocalServicesForConnector()
            => LocalServiceProvider ?? UnsetLocalServiceProvider.Instance;

        private sealed class UnsetLocalServiceProvider : IServiceProvider
        {
            public static readonly UnsetLocalServiceProvider Instance = new();

            public object? GetService(Type serviceType)
                => throw new InvalidOperationException(
                    "ClientInfo.LocalServiceProvider is not set. A local connection runs the backend in this process; " +
                    "assign the service provider built by services.AddPolhemFramework(...) before connecting.");
        }

        /// <summary>
        /// Creates a form-level API connector for the specified program.
        /// </summary>
        /// <param name="progId">Program identifier.</param>
        public static FormApiConnector CreateFormApiConnector(string progId)
        {
            return ApiClientInfo.ConnectType == ConnectType.Local
                ? new FormApiConnector(LocalServicesForConnector(), AccessToken, progId)
                : new FormApiConnector(ApiClientInfo.Endpoint, AccessToken, progId);
        }

        /// <summary>
        /// Creates an audit-log API connector (read-only queries over the <c>st_log_*</c> tables).
        /// </summary>
        public static AuditLogApiConnector CreateAuditLogApiConnector()
        {
            return ApiClientInfo.ConnectType == ConnectType.Local
                ? new AuditLogApiConnector(LocalServicesForConnector(), AccessToken)
                : new AuditLogApiConnector(ApiClientInfo.Endpoint, AccessToken);
        }

        /// <summary>
        /// Definition-data accessor. Recreated whenever the endpoint changes.
        /// </summary>
        public static ClientDefineAccess DefineAccess
        {
            get
            {
                lock (s_stateGate) { return s_defineAccess ??= new ClientDefineAccess(SystemApiConnector); }
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
        /// company accessor and <see cref="Polhem.Api.Client.ApiClientInfo.DefaultLanguage"/> as its
        /// default language, and discarded together with <see cref="DefineAccess"/> when the access
        /// token changes, so it never serves a previous identity's definitions.
        /// </remarks>
        public static FormDefinitionLoader? DefinitionLoader
        {
            get
            {
                if (!UseDefinitionLoader) { return null; }
                lock (s_stateGate)
                {
                    return s_definitionLoader ??= new FormDefinitionLoader(
                        s_defineAccess ??= new ClientDefineAccess(SystemApiConnector))
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
        /// UI view service supplied by the host application.
        /// </summary>
        public static IUIViewService? UIViewService { get; private set; }

        /// <summary>
        /// Whether <c>System.Settings.xml</c> and <c>Database.Settings.xml</c> should be auto-generated
        /// when the local endpoint is missing the expected files.
        /// </summary>
        public static bool AllowGenerateSettings { get; set; }

        /// <summary>
        /// Authenticated user information set by <see cref="ApplyLoginResult"/>.
        /// </summary>
        public static UserInfo? UserInfo { get; private set; }

        /// <summary>
        /// The per-model capability snapshot for the entered company, or <c>null</c> when no company
        /// context is active (before <see cref="ApplyEnterCompanyResult"/>, or after
        /// <see cref="ClearCompanyContext"/> / a token change).
        /// </summary>
        /// <remarks>
        /// <c>null</c> means capability enforcement is inactive and the element capability resolver
        /// leaves every element at full capability — so an app that never enters a company (or does
        /// not use permissions) renders unchanged. When non-null, a model absent from the map means
        /// no permission on that model. This is UX degradation only; the backend remains the
        /// authoritative security boundary.
        /// </remarks>
        public static IReadOnlyDictionary<string, PermissionActions>? Capabilities => s_capabilities;

        /// <summary>
        /// Gets the current company entered through <c>EnterCompany</c>, or <c>null</c> when no company
        /// context is active. Carries the company-level decimal-place overrides and default (home)
        /// currency used to round computed numeric fields client-side. Read-only UX aid; the server rounds authoritatively on save.
        /// </summary>
        public static CompanyInfo? Company => s_company;

        /// <summary>
        /// Caches the capability snapshot and company info from an <c>EnterCompany</c> response, and
        /// discards definitions cached for the previous tenant. The host calls this after
        /// <see cref="SystemApiConnector.EnterCompanyAsync"/>.
        /// </summary>
        /// <remarks>
        /// The cache flush is done here rather than left to the caller. Entering a company is exactly
        /// the moment the tenant changes, and a host that forgets to flush gets the previous tenant's
        /// customized layouts and captions with no error to point at it — a cross-tenant leak that
        /// only shows up as wrong text on screen.
        /// </remarks>
        /// <param name="response">The EnterCompany response carrying the capability snapshot and company.</param>
        public static void ApplyEnterCompanyResult(EnterCompanyResponse response)
        {
            ArgumentNullException.ThrowIfNull(response);
            s_capabilities = response.Capabilities;
            s_company = response.Company;
            ResetDefineCache();
        }

        /// <summary>
        /// Clears the cached capability snapshot, company info and definition cache. The host calls
        /// this on <c>LeaveCompany</c> / logout so nothing from the previous tenant survives.
        /// </summary>
        /// <remarks>
        /// Mirrors <see cref="ApplyEnterCompanyResult"/>: leaving a company is a tenant change too,
        /// so the definition cache is flushed here rather than left to the caller.
        /// </remarks>
        public static void ClearCompanyContext()
        {
            s_capabilities = null;
            s_company = null;
            ResetDefineCache();
        }

        private static void SetConnectType(ConnectType connectType, string endpoint)
        {
            if (connectType == ConnectType.Local)
            {
                ApiClientInfo.ConnectType = ConnectType.Local;
                ApiClientInfo.Endpoint = string.Empty;
            }
            else
            {
                ApiClientInfo.ConnectType = ConnectType.Remote;
                ApiClientInfo.Endpoint = endpoint;
            }
            // NOTE: changing the connection method always invalidates the existing token, so a
            // fresh sign-in is required.
            AccessToken = Guid.Empty;
            // The time zone goes with it. Once the session is gone that zone belongs to nobody,
            // and leaving it behind means it would be used for conversions before the next sign-in
            // (ADR-032 D13). `ApplyLoginResult` fills it in again on the way back.
            ApiSessionContext.Ambient.UserTimeZoneId = string.Empty;
        }

        /// <summary>
        /// Sets the service endpoint and persists it, awaiting the validation and connector
        /// initialization instead of blocking on them.
        /// </summary>
        /// <param name="endpoint">URL for remote connections; local file path for local connections.</param>
        /// <param name="cancellationToken">A token that cancels the validation and the connector initialization.</param>
        /// <remarks>
        /// Validates the endpoint and initializes the connector without blocking, so it is safe on
        /// single-threaded runtimes (browser WASM), where blocking on async work throws
        /// "Cannot wait on monitors on this runtime".
        /// </remarks>
        public static async Task SetEndpointAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            var connectType = await ApiConnectValidator.ValidateAsync(endpoint, AllowGenerateSettings, cancellationToken)
                .ConfigureAwait(false);
            SetConnectType(connectType, endpoint);
            await SystemApiConnector.InitializeAsync(cancellationToken).ConfigureAwait(false);
            EndpointStorage.SaveEndpoint(endpoint);
        }

        /// <summary>
        /// Returns the currently configured service endpoint.
        /// </summary>
        public static string GetEndpoint()
        {
            return EndpointStorage.LoadEndpoint();
        }

        /// <summary>
        /// Returns the currently configured API key.
        /// </summary>
        public static string GetApiKey()
        {
            return ApiKeyStorage.LoadApiKey();
        }

        /// <summary>
        /// Persists the API key and applies it to subsequent API calls.
        /// </summary>
        /// <param name="apiKey">The API key issued for this application.</param>
        /// <remarks>
        /// Synchronous, unlike <see cref="SetEndpointAsync"/>: changing the endpoint has to
        /// revalidate the connection and rebuild the connector, whereas the key is simply a header
        /// value the next call carries.
        /// </remarks>
        public static void SetApiKey(string apiKey)
        {
            ApiKeyStorage.SaveApiKey(apiKey);
            ApiClientInfo.ApiKey = apiKey;
        }

        /// <summary>
        /// Applies the stored API key to <see cref="ApiClientInfo.ApiKey"/>, falling back to
        /// <paramref name="defaultApiKey"/> — and persisting it — the first time an application runs
        /// with nothing stored.
        /// </summary>
        /// <param name="defaultApiKey">
        /// The key the application ships with, used only to seed empty storage. Pass an empty string
        /// for deployments that expect the key to be configured out of band.
        /// </param>
        /// <remarks>
        /// This is what lets an application drop its hard-coded key without losing out-of-the-box
        /// behaviour: the shipped value becomes a first-run seed, and from then on the stored value
        /// wins and can be changed without recompiling.
        /// </remarks>
        public static void ApplyApiKey(string defaultApiKey = "")
        {
            string stored = ApiKeyStorage.LoadApiKey();
            if (StringUtilities.IsEmpty(stored) && StringUtilities.IsNotEmpty(defaultApiKey))
            {
                ApiKeyStorage.SaveApiKey(defaultApiKey);
                stored = defaultApiKey;
            }
            ApiClientInfo.ApiKey = stored;
        }

        private static async Task<bool> InitializeConnectAsync(SupportedConnectTypes supportedConnectTypes,
            CancellationToken cancellationToken)
        {
            ApiClientInfo.SupportedConnectTypes = supportedConnectTypes;
            try
            {
                string endpoint = GetEndpoint();
                var connectType = await ApiConnectValidator.ValidateAsync(endpoint, AllowGenerateSettings, cancellationToken)
                    .ConfigureAwait(false);
                SetConnectType(connectType, endpoint);
                await SystemApiConnector.InitializeAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }
            // A cancellation the caller asked for is not an unreachable endpoint: it propagates rather
            // than sending the user to the connection-setup view.
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException
                or IOException or SocketException or UriFormatException
                || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
            {
                // Returning false sends the caller to the connection-setup view, so only "the
                // endpoint is missing, malformed or unreachable" belongs here. The first two entries
                // are the vocabulary this path actually raises — `ApiConnectValidator` reports an
                // unreachable endpoint as `InvalidOperationException` and an empty one as
                // `ArgumentException`; the rest cover the transport failures underneath it.
                return false;
            }
        }

        /// <summary>
        /// Initializes from settings. Falls back to the connection setup view when the endpoint is
        /// missing or unreachable.
        /// </summary>
        /// <param name="service">UI view service supplied by the host application.</param>
        /// <param name="connectTypes">Connection types supported by the application.</param>
        /// <param name="cancellationToken">
        /// A token that cancels the connection attempt and is passed on to
        /// <see cref="IUIViewService.ShowApiConnectAsync"/>. A cancelled call throws
        /// <see cref="OperationCanceledException"/> rather than falling back to the setup view.
        /// </param>
        [SuppressMessage("ApiDesign", "RS0026:Do not add multiple overloads with optional parameters",
            Justification = "The only optional parameter is the trailing cancellation token, and the two overloads differ in their first parameter (an IUIViewService or an endpoint string), so no call can rebind from one to the other, which is the hazard RS0026 guards against.")]
        public static async Task<bool> InitializeAsync(IUIViewService service, SupportedConnectTypes connectTypes,
            CancellationToken cancellationToken = default)
        {
            UIViewService = service;
            Arguments = ParseCommandLineArgs();
            if (Arguments.TryGetValue("Endpoint", out string? endpointArg))
            {
                EndpointStorage.SetEndpoint(endpointArg);
            }
            // NOTE: in-memory only, like the endpoint argument — a command line switch overrides
            // this run without rewriting stored settings. Suitable because the key identifies an
            // application rather than authenticating a user; a real credential does not belong in
            // an argument list, which is readable from the process table.
            if (Arguments.TryGetValue("ApiKey", out string? apiKeyArg))
            {
                ApiKeyStorage.SetApiKey(apiKeyArg);
            }
            if (!await InitializeConnectAsync(connectTypes, cancellationToken).ConfigureAwait(false)
                && !await UIViewService.ShowApiConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Initializes with an explicit endpoint, awaiting the validation and connector
        /// initialization without blocking.
        /// </summary>
        /// <param name="endpoint">URL for remote connections; local file path for local connections.</param>
        /// <param name="cancellationToken">A token that cancels the validation and the connector initialization.</param>
        /// <remarks>
        /// Safe on single-threaded runtimes (browser WASM), where blocking on async work throws
        /// "Cannot wait on monitors".
        /// </remarks>
        [SuppressMessage("ApiDesign", "RS0026:Do not add multiple overloads with optional parameters",
            Justification = "The only optional parameter is the trailing cancellation token, and the two overloads differ in their first parameter (an IUIViewService or an endpoint string), so no call can rebind from one to the other, which is the hazard RS0026 guards against.")]
        public static Task InitializeAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            return SetEndpointAsync(endpoint, cancellationToken);
        }

        /// <summary>
        /// Applies the login response, populating <see cref="AccessToken"/> and <see cref="UserInfo"/>,
        /// and makes the user's culture this process's culture.
        /// </summary>
        /// <param name="loginResponse">Result returned from the login API.</param>
        /// <remarks>
        /// <para>
        /// The culture the server returns — <c>st_user.culture</c>, or the deployment's default
        /// language — becomes <see cref="CultureInfo.CurrentUICulture"/> and
        /// <see cref="CultureInfo.CurrentCulture"/>, for the current flow and as the process default
        /// (<see cref="CultureInfo.DefaultThreadCurrentUICulture"/>). Everything that localizes reads
        /// those: definition captions, the framework's own UI text, and the display and input of
        /// numbers and dates. So a user whose account says <c>en-US</c> gets English on a Chinese
        /// operating system.
        /// </para>
        /// <para>
        /// An empty culture, or one this runtime does not know, leaves the process culture as it was.
        /// Process-wide state is correct here for the same reason the rest of this class is: one
        /// process serves one signed-in user.
        /// </para>
        /// </remarks>
        public static void ApplyLoginResult(LoginResponse loginResponse)
        {
            ArgumentNullException.ThrowIfNull(loginResponse);

            AccessToken = loginResponse.AccessToken;
            UserInfo = new UserInfo()
            {
                UserId = loginResponse.UserId,
                UserName = loginResponse.UserName,
                // The server's value is authoritative; an empty one leaves the UserInfo default
                // rather than silently adopting the device zone, which ADR-032 D4 rules out.
                TimeZone = StringUtilities.IsNotEmpty(loginResponse.TimeZone)
                    ? loginResponse.TimeZone
                    : new UserInfo().TimeZone,
                Culture = loginResponse.Culture ?? string.Empty,
            };
            // The Connector layer sits below this one, so it cannot read UserInfo — hand it the zone
            // it needs to convert payloads with (ADR-032 D4).
            ApiSessionContext.Ambient.UserTimeZoneId = UserInfo.TimeZone;
            ApplyCulture(UserInfo.Culture);
            // NOTE: any further post-sign-in state belongs here.
        }

        /// <summary>
        /// Makes <paramref name="culture"/> the current and default culture of this process.
        /// </summary>
        /// <param name="culture">A BCP-47 culture name; empty or unknown leaves the culture unchanged.</param>
        /// <returns><c>true</c> when the culture was applied.</returns>
        internal static bool ApplyCulture(string culture)
        {
            if (StringUtilities.IsEmpty(culture)) { return false; }

            CultureInfo info;
            try
            {
                info = CultureInfo.GetCultureInfo(culture);
            }
            catch (CultureNotFoundException)
            {
                return false;
            }

            CultureInfo.DefaultThreadCurrentCulture = info;
            CultureInfo.DefaultThreadCurrentUICulture = info;
            CultureInfo.CurrentCulture = info;
            CultureInfo.CurrentUICulture = info;
            return true;
        }

        private static Dictionary<string, string> ParseCommandLineArgs()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                int sep = args[i].IndexOf('=');
                if (sep > 0)
                {
                    string key = args[i].Substring(0, sep);
                    string value = args[i].Substring(sep + 1);
                    result[key] = value;
                }
            }
            return result;
        }

    }
}
