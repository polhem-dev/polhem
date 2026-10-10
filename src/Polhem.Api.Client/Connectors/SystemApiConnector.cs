using Polhem.Definition.Settings;
using Polhem.Api.Core.Transformers;
using Polhem.Core;
using Polhem.Core.Security;
using Polhem.Core.Serialization;
using Polhem.Api.Core.Messages.System;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Language;
using Polhem.Definition.Organization;
using Polhem.Definition.Security;
using Polhem.Api.Core.Messages;
using Polhem.JsonRpc.Payload;

namespace Polhem.Api.Client.Connectors
{
    /// <summary>
    /// System-level API service connector.
    /// </summary>
    public class SystemApiConnector : ApiConnector
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SystemApiConnector"/> class.
        /// </summary>
        /// <param name="client">The client whose connection and signed-in identity this connector calls with.</param>
        /// <remarks>
        /// <see cref="PolhemApiClient.System"/> is the instance most callers want. Construct one only to derive a
        /// connector that exposes a host's own system actions.
        /// </remarks>
        public SystemApiConnector(PolhemApiClient client) : base(client)
        {
        }

        /// <summary>
        /// Asynchronously executes an API method.
        /// </summary>
        /// <param name="action">The action name to execute.</param>
        /// <param name="value">The input parameter for the action.</param>
        /// <param name="format">The payload encoding format for transmission.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <remarks>
        /// Protected: every framework action on <c>System</c> has a typed method on this connector. A host
        /// that adds actions to its own system business object exposes them from a subclass.
        /// </remarks>
        protected async Task<T> ExecuteAsync<T>(string action, object value, PayloadFormat format = PayloadFormat.Encrypted,
            CancellationToken cancellationToken = default)
        {
            return await base.ExecuteAsync<T>(SysProgIds.System, action, value, format, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously executes a custom method; requires authentication.
        /// </summary>
        /// <param name="request">The custom method identifier and its parameters.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <remarks>
        /// Takes the request message rather than separate arguments because the call is an open
        /// parameter bag whose shape the application's custom method defines.
        /// </remarks>
        public virtual async Task<ExecFuncResponse> ExecFuncAsync(ExecFuncRequest request, CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync<ExecFuncResponse>(SystemActions.ExecFunc, request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously executes a custom method; allows anonymous access.
        /// </summary>
        /// <param name="request">The custom method identifier and its parameters.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public virtual async Task<ExecFuncResponse> ExecFuncAnonymousAsync(ExecFuncRequest request, CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync<ExecFuncResponse>(SystemActions.ExecFuncAnonymous, request, PayloadFormat.Encoded, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously executes the Ping method to test the server connection status.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <returns>The server's answer, whose status is <c>ok</c>.</returns>
        /// <exception cref="InvalidOperationException">
        /// The call failed or the server answered with a status other than <c>ok</c>; the original
        /// error is the inner exception.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// <paramref name="cancellationToken"/> was cancelled; this is not wrapped.
        /// </exception>
        public virtual async Task<PingResponse> PingAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new PingRequest()
                {
                    ClientName = "Connector",
                    TraceId = Guid.NewGuid().ToString()
                };
                var result = await ExecuteAsync<PingResponse>(SystemActions.Ping, request, PayloadFormat.Plain, cancellationToken)
                    .ConfigureAwait(false);
                if (result.Status != "ok")
                    throw new InvalidOperationException($"Ping method failed with status: {result.Status}");
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Preserve the original error message for callers to inspect or log
                throw new InvalidOperationException("Connection failed during Ping.", ex);
            }
        }

        /// <summary>
        /// Asynchronously retrieves the server's common parameters and environment configuration,
        /// without applying them.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <remarks>
        /// IMPORTANT: the answer arrives over an anonymous Plain call and nothing authenticates it.
        /// <see cref="InitializeAsync"/> is the step that applies it to this process, and it adopts
        /// only the parts a forged answer cannot use to weaken the client; a caller reading this
        /// response directly must apply the same caution.
        /// </remarks>
        public virtual async Task<GetCommonConfigurationResponse> GetCommonConfigurationAsync(CancellationToken cancellationToken = default)
        {
            var request = new GetCommonConfigurationRequest();
            return await ExecuteAsync<GetCommonConfigurationResponse>(SystemActions.GetCommonConfiguration, request,
                PayloadFormat.Plain, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously retrieves common parameters and environment configuration, then initializes the system.
        /// </summary>
        /// <remarks>
        /// <para>
        /// IMPORTANT: the answer arrives over an anonymous Plain call and nothing authenticates it, so
        /// it is adopted only where a forged answer cannot weaken this client. The payload options
        /// are taken, but a server-advertised <c>none</c> encryptor is refused unless this client is
        /// itself in debug mode (<see cref="SysInfo.IsDebugMode"/> as it stood before the call); the
        /// server's own debug flag is not adopted; and the server's
        /// <see cref="CommonConfiguration.AllowedTypeNamespaces"/> is ignored, because that list
        /// guards this client against what the server sends and cannot be the server's to widen.
        /// A client that must accept a deployment's own types configures its list locally, through
        /// <see cref="SysInfo.Initialize"/>, before calling this. The default language is taken as
        /// advertised into <see cref="PolhemApiClient.DefaultLanguage"/>: a forged one changes only
        /// which language a missing translation falls back to.
        /// </para>
        /// <para>
        /// The Encrypted payload format relies on TLS for protection against an active
        /// man-in-the-middle. The session key is exchanged in <see cref="LoginAsync"/> under a client
        /// public key that nothing authenticates, and this configuration call is Plain; over plain
        /// HTTP an intermediary can substitute either. Encryption protects the payload from passive
        /// observers and from intermediaries that terminate TLS, not in place of TLS.
        /// </para>
        /// </remarks>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <exception cref="InvalidOperationException">
        /// The server advertises no payload encryption and this client is not in debug mode.
        /// </exception>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            // Retrieve common parameters and environment configuration for initialization
            var result = await GetCommonConfigurationAsync(cancellationToken).ConfigureAwait(false);
            var serverConfiguration = XmlCodec.Deserialize<CommonConfiguration>(result.CommonConfiguration)!;
            var configuration = AdoptServerConfiguration(serverConfiguration, SysInfo.IsDebugMode, SysInfo.AllowedTypeNamespaces);
            SysInfo.Initialize(configuration);
            Client.DefaultLanguage = configuration.DefaultLanguage;
            // The server decides the compressor and the encryptor; the rest of the client's payload options stay.
            PolhemPayload.Apply(Client.PayloadOptions, configuration.ApiPayloadOptions, configuration.IsDebugMode);
        }

        /// <summary>
        /// Builds the configuration this client applies from the server's unauthenticated answer.
        /// </summary>
        /// <param name="serverConfiguration">The configuration the server advertised.</param>
        /// <param name="clientIsDebugMode">Whether this client was in debug mode before the call.</param>
        /// <param name="clientTypeNamespaces">The type namespaces this client already allows.</param>
        /// <returns>
        /// The server's version and payload options, with this client's own debug flag and type
        /// namespaces.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// The server advertises the <c>none</c> encryptor, or none at all, and
        /// <paramref name="clientIsDebugMode"/> is <c>false</c>.
        /// </exception>
        /// <remarks>
        /// Checked here, before anything is applied, so a refused answer leaves this client exactly
        /// as it was rather than half reconfigured.
        /// </remarks>
        internal static CommonConfiguration AdoptServerConfiguration(
            CommonConfiguration serverConfiguration, bool clientIsDebugMode, IEnumerable<string> clientTypeNamespaces)
        {
            ArgumentNullException.ThrowIfNull(serverConfiguration);
            ArgumentNullException.ThrowIfNull(clientTypeNamespaces);

            var payloadOptions = serverConfiguration.ApiPayloadOptions ?? new ApiPayloadOptions();
            if (!clientIsDebugMode && IsNoEncryption(payloadOptions.Encryptor))
            {
                throw new InvalidOperationException(
                    "The server advertises no payload encryption. This client accepts that only when it is itself in debug mode.");
            }

            return new CommonConfiguration
            {
                Version = serverConfiguration.Version,
                IsDebugMode = clientIsDebugMode,
                DefaultLanguage = serverConfiguration.DefaultLanguage,
                AllowedTypeNamespaces = string.Join('|', clientTypeNamespaces),
                ApiPayloadOptions = payloadOptions,
            };
        }

        /// <summary>
        /// Says whether an encryptor name selects no encryption, by the same names
        /// <see cref="PolhemPayload.Apply"/> accepts for it.
        /// </summary>
        private static bool IsNoEncryption(string? encryptor)
            => string.IsNullOrEmpty(encryptor) || string.Equals(encryptor, "none", StringComparison.Ordinal);

        /// <summary>
        /// Asynchronously creates a new user session.
        /// </summary>
        /// <param name="userId">The user account identifier.</param>
        /// <param name="expiresIn">The expiration time in seconds. Defaults to 3600.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <returns>The response carrying the new session's access token and expiry.</returns>
        public virtual async Task<CreateSessionResponse> CreateSessionAsync(string userId, int expiresIn = 3600,
            CancellationToken cancellationToken = default)
        {
            var request = new CreateSessionRequest()
            {
                UserId = userId,
                ExpiresIn = expiresIn
            };
            return await ExecuteAsync<CreateSessionResponse>(SystemActions.CreateSession, request, PayloadFormat.Plain,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously performs the login operation.
        /// </summary>
        /// <remarks>
        /// On Blazor WebAssembly (<see cref="OperatingSystem.IsBrowser"/> returns true), the RSA
        /// handshake is skipped because .NET's RSA key generation is not implemented on the
        /// browser-wasm runtime. <see cref="LoginRequest.ClientPublicKey"/> is sent empty, the
        /// server returns an empty <see cref="LoginResponse.ApiEncryptionKey"/>, and subsequent
        /// <see cref="PayloadFormat.Encrypted"/> requests are auto-downgraded to
        /// <see cref="PayloadFormat.Encoded"/> by <see cref="ApiConnector"/>.
        /// <para>
        /// On success the client's <see cref="PolhemApiClient.Session"/> is signed in with the access
        /// token, the session key and the user's time zone (<see cref="LoginResponse.TimeZone"/>), as
        /// one replacement. Every connector of the client calls with them from then on, and date-time
        /// conversion uses the zone.
        /// </para>
        /// </remarks>
        /// <param name="userId">The user account identifier.</param>
        /// <param name="password">The user password.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public virtual async Task<LoginResponse> LoginAsync(string userId, string password, CancellationToken cancellationToken = default)
        {
            string publicKey = string.Empty;
            string privateKey = string.Empty;
            bool useRsaHandshake = !OperatingSystem.IsBrowser();
            if (useRsaHandshake)
            {
                RsaCryptor.GenerateRsaKeyPair(out publicKey, out privateKey);
            }

            var request = new LoginRequest()
            {
                UserId = userId,
                Password = password,
                ClientPublicKey = publicKey
            };
            var result = await ExecuteAsync<LoginResponse>(SystemActions.Login, request, PayloadFormat.Encoded, cancellationToken)
                .ConfigureAwait(false);

            byte[] sessionKey = [];
            if (useRsaHandshake && !string.IsNullOrEmpty(result.ApiEncryptionKey))
            {
                sessionKey = Convert.FromBase64String(RsaCryptor.DecryptWithPrivateKey(result.ApiEncryptionKey, privateKey));
            }

            // The connector signs in the session itself, the zone included. Doing it here rather than
            // in a UI head is what gives every head, a multi-user Blazor circuit among them, the
            // ADR-032 conversion of this user's date-time values.
            Client.Session.SignIn(new ApiSessionCredentials(result.AccessToken, sessionKey, result.TimeZone ?? string.Empty));

            return result;
        }

        /// <summary>
        /// Asynchronously gets definition data.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="defineType">The definition data type.</param>
        /// <param name="keys">The keys used to locate the definition data.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public virtual async Task<T> GetDefineAsync<T>(DefineType defineType, string[]? keys = null,
            CancellationToken cancellationToken = default)
        {
            var request = new GetDefineRequest()
            {
                DefineType = defineType,
                Keys = keys
            };
            var result = await ExecuteAsync<GetDefineResponse>(SystemActions.GetDefine, request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (StringUtilities.IsNotEmpty(result.Xml))
                return XmlCodec.Deserialize<T>(result.Xml)!;
            else
                return default!;
        }

        /// <summary>
        /// Asynchronously gets the tenant customization layer of a form layout definition;
        /// <c>null</c> when this session's tenant supplies no override.
        /// </summary>
        /// <param name="progId">The program identifier.</param>
        /// <param name="layoutId">The layout identifier; empty resolves to <paramref name="progId"/>.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <remarks>
        /// The companion of the base-layer fetch: obtain both layers and pick between them with
        /// <see cref="Polhem.Definition.Customization.CustomizeOverlay"/>. There is deliberately no customization-code parameter — the
        /// server takes it from the session, so a caller cannot ask for another tenant's overrides.
        /// </remarks>
        public virtual async Task<FormLayout?> GetCustomizeFormLayoutAsync(string progId, string layoutId = "",
            CancellationToken cancellationToken = default)
        {
            var request = new GetFormLayoutRequest { ProgId = progId, LayoutId = layoutId };
            var result = await ExecuteAsync<GetFormLayoutResponse>(SystemActions.GetCustomizeFormLayout, request,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return StringUtilities.IsNotEmpty(result.Xml) ? XmlCodec.Deserialize<FormLayout>(result.Xml!) : null;
        }

        /// <summary>
        /// Asynchronously gets the tenant customization layer of a language resource;
        /// <c>null</c> when this session's tenant supplies no override.
        /// </summary>
        /// <param name="lang">The BCP-47 language code.</param>
        /// <param name="ns">The resource namespace.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <remarks>
        /// See <see cref="GetCustomizeFormLayoutAsync"/> for why there is no customization-code parameter.
        /// </remarks>
        public virtual async Task<LanguageResource?> GetCustomizeLanguageAsync(string lang, string ns,
            CancellationToken cancellationToken = default)
        {
            var request = new GetLanguageRequest { Lang = lang, Namespace = ns };
            var result = await ExecuteAsync<GetLanguageResponse>(SystemActions.GetCustomizeLanguage, request,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return StringUtilities.IsNotEmpty(result.Xml) ? XmlCodec.Deserialize<LanguageResource>(result.Xml!) : null;
        }

        /// <summary>
        /// Asynchronously gets the base form schema of a program, exactly as stored.
        /// </summary>
        /// <param name="progId">The program identifier.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <returns>The schema, or <c>null</c> when the server has none for <paramref name="progId"/>.</returns>
        /// <remarks>
        /// <para>
        /// The per-type counterpart of <see cref="GetDefineAsync{T}"/> with
        /// <see cref="DefineType.FormSchema"/>. No localization or number formats are applied; a UI
        /// assembles those through <see cref="Polhem.Api.Client.Definitions.FormDefinitionLoader"/>.
        /// </para>
        /// <para>
        /// Definition types travel as XML rather than a JSON tree because they declare XML as their
        /// serialization contract: their nested collections are get-only, which XmlSerializer fills
        /// in place, while JSON and MessagePack bind by writability and would drop them on the way
        /// back without an error.
        /// </para>
        /// </remarks>
        public virtual async Task<FormSchema?> GetFormSchemaAsync(string progId, CancellationToken cancellationToken = default)
        {
            var request = new GetFormSchemaRequest { ProgId = progId };
            var result = await ExecuteAsync<GetFormSchemaResponse>(SystemActions.GetFormSchema, request,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return StringUtilities.IsNotEmpty(result.Xml) ? XmlCodec.Deserialize<FormSchema>(result.Xml!) : null;
        }

        /// <summary>
        /// Asynchronously gets the base layer of a form layout definition, exactly as stored.
        /// </summary>
        /// <param name="progId">The program identifier.</param>
        /// <param name="layoutId">The layout identifier; empty resolves to <paramref name="progId"/>.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <returns>The layout, or <c>null</c> when the base layer stores none.</returns>
        /// <remarks>
        /// The tenant's layer comes from <see cref="GetCustomizeFormLayoutAsync"/>; pick between the
        /// two with <see cref="Polhem.Definition.Customization.CustomizeOverlay"/>.
        /// </remarks>
        public virtual async Task<FormLayout?> GetFormLayoutAsync(string progId, string layoutId = "",
            CancellationToken cancellationToken = default)
        {
            var request = new GetFormLayoutRequest { ProgId = progId, LayoutId = layoutId };
            var result = await ExecuteAsync<GetFormLayoutResponse>(SystemActions.GetFormLayout, request,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return StringUtilities.IsNotEmpty(result.Xml) ? XmlCodec.Deserialize<FormLayout>(result.Xml!) : null;
        }

        /// <summary>
        /// Asynchronously gets the base layer of a language resource, exactly as stored.
        /// </summary>
        /// <param name="lang">The BCP-47 language code.</param>
        /// <param name="ns">The resource namespace.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <returns>The resource, or <c>null</c> when the base layer stores none.</returns>
        /// <remarks>
        /// The tenant's layer comes from <see cref="GetCustomizeLanguageAsync"/>.
        /// </remarks>
        public virtual async Task<LanguageResource?> GetLanguageAsync(string lang, string ns,
            CancellationToken cancellationToken = default)
        {
            var request = new GetLanguageRequest { Lang = lang, Namespace = ns };
            var result = await ExecuteAsync<GetLanguageResponse>(SystemActions.GetLanguage, request,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return StringUtilities.IsNotEmpty(result.Xml) ? XmlCodec.Deserialize<LanguageResource>(result.Xml!) : null;
        }

        /// <summary>
        /// Asynchronously gets the current company's department tree (per-company organisation
        /// hierarchy). JSON-friendly for frontends; returns <c>null</c> when no company is entered.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public virtual async Task<DepartmentTree?> GetDepartmentTreeAsync(CancellationToken cancellationToken = default)
        {
            var request = new GetDepartmentTreeRequest();
            var result = await ExecuteAsync<GetDepartmentTreeResponse>(SystemActions.GetDepartmentTree, request,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return result.Tree;
        }

        /// <summary>
        /// Asynchronously saves definition data.
        /// </summary>
        /// <param name="defineType">The definition data type.</param>
        /// <param name="defineObject">The definition data object.</param>
        /// <param name="keys">The keys used to locate where the definition data is saved.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public virtual async Task<SaveDefineResponse> SaveDefineAsync(DefineType defineType, object defineObject, string[]? keys = null,
            CancellationToken cancellationToken = default)
        {
            var request = new SaveDefineRequest()
            {
                DefineType = defineType,
                Xml = XmlCodec.Serialize(defineObject),
                Keys = keys
            };
            return await ExecuteAsync<SaveDefineResponse>(SystemActions.SaveDefine, request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously issues a new API key and returns the complete plaintext key once.
        /// </summary>
        /// <param name="sysId">The key identifier to issue; lowercase letters, digits and hyphens.</param>
        /// <param name="sysName">The display name of the application the key is for.</param>
        /// <param name="keyType">The key classification; a label only.</param>
        /// <param name="contact">The contact for a third-party holder, so an incident has someone to reach.</param>
        /// <param name="expiredAt">The UTC expiry, or <c>null</c> for a key that does not expire.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <remarks>
        /// IMPORTANT: <see cref="CreateApiKeyResponse.ApiKey"/> is the only time the plaintext key exists
        /// outside the caller — the server keeps just a hash. Persist it here or issue a replacement.
        /// <para>
        /// A remote call requires the signed-in user to be a deployment administrator; an
        /// in-process call passes without one, so a deployment with no administrator yet can still
        /// mint its first key on the host.
        /// </para>
        /// </remarks>
        public virtual async Task<CreateApiKeyResponse> CreateApiKeyAsync(string sysId, string sysName,
            ApiKeyType keyType = ApiKeyType.Internal, string? contact = null, DateTime? expiredAt = null,
            CancellationToken cancellationToken = default)
        {
            var request = new CreateApiKeyRequest()
            {
                SysId = sysId,
                SysName = sysName,
                KeyType = keyType,
                Contact = contact,
                ExpiredAt = expiredAt
            };
            return await ExecuteAsync<CreateApiKeyResponse>(SystemActions.CreateApiKey, request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously lists the issued API keys, enabled and disabled alike.
        /// </summary>
        /// <remarks>
        /// The response carries no credential material — the stored hash never leaves the server.
        /// Like the rest of key management, a remote call requires a deployment administrator.
        /// </remarks>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public virtual async Task<ListApiKeysResponse> ListApiKeysAsync(CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync<ListApiKeysResponse>(SystemActions.ListApiKeys, new ListApiKeysRequest(),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously enables or disables an issued API key.
        /// </summary>
        /// <param name="sysId">The key identifier (<c>st_api_key.sys_id</c>).</param>
        /// <param name="enabled">Whether the key is accepted from now on.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <remarks>
        /// IMPORTANT: disabling revokes the key immediately across every server process, not when
        /// some cache lapses.
        /// </remarks>
        public virtual async Task<SetApiKeyEnabledResponse> SetApiKeyEnabledAsync(string sysId, bool enabled,
            CancellationToken cancellationToken = default)
        {
            var request = new SetApiKeyEnabledRequest()
            {
                SysId = sysId,
                Enabled = enabled
            };
            return await ExecuteAsync<SetApiKeyEnabledResponse>(SystemActions.SetApiKeyEnabled, request,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously sets or clears an issued API key's expiry.
        /// </summary>
        /// <param name="sysId">The key identifier (<c>st_api_key.sys_id</c>).</param>
        /// <param name="expiredAt">The UTC expiry, or <c>null</c> to clear it.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public virtual async Task<SetApiKeyExpiryResponse> SetApiKeyExpiryAsync(string sysId, DateTime? expiredAt,
            CancellationToken cancellationToken = default)
        {
            var request = new SetApiKeyExpiryRequest()
            {
                SysId = sysId,
                ExpiredAt = expiredAt
            };
            return await ExecuteAsync<SetApiKeyExpiryResponse>(SystemActions.SetApiKeyExpiry, request,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously grants or revokes a user's deployment administrator flag.
        /// </summary>
        /// <param name="userId">The user business id (<c>st_user.sys_id</c>) whose flag is being set.</param>
        /// <param name="isDeploymentAdmin">The flag value to store.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <remarks>
        /// WARNING: a deployment administrator may act on installation-wide assets such as API keys.
        /// The server restricts this to local calls, so this reaches it through an in-process
        /// connector only.
        /// </remarks>
        public virtual async Task<SetDeploymentAdminResponse> SetDeploymentAdminAsync(string userId, bool isDeploymentAdmin,
            CancellationToken cancellationToken = default)
        {
            var request = new SetDeploymentAdminRequest()
            {
                UserId = userId,
                IsDeploymentAdmin = isDeploymentAdmin
            };
            return await ExecuteAsync<SetDeploymentAdminResponse>(SystemActions.SetDeploymentAdmin, request,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously reads one tenant's business plugin bindings.
        /// </summary>
        /// <param name="customizeId">The tenant customization code.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <remarks>
        /// The server restricts this to local calls, so it reaches the server through an in-process
        /// connector only — the maintenance tool runs on the host.
        /// </remarks>
        public virtual async Task<GetCustomizePluginSettingsResponse> GetCustomizePluginSettingsAsync(string customizeId,
            CancellationToken cancellationToken = default)
        {
            var request = new GetCustomizePluginSettingsRequest() { CustomizeId = customizeId };
            return await ExecuteAsync<GetCustomizePluginSettingsResponse>(
                SystemActions.GetCustomizePluginSettings, request, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously stores one tenant's business plugin bindings, replacing whatever the
        /// tenant had.
        /// </summary>
        /// <param name="customizeId">The tenant customization code.</param>
        /// <param name="xml">The bindings as XML; an empty string clears them.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <remarks>
        /// WARNING: these bindings decide which code runs inside the save and delete pipelines. The
        /// server restricts this to local calls for that reason, and validates every bound type
        /// before storing anything — one bad entry rejects the whole definition.
        /// </remarks>
        public virtual async Task<SaveCustomizePluginSettingsResponse> SaveCustomizePluginSettingsAsync(string customizeId, string xml,
            CancellationToken cancellationToken = default)
        {
            var request = new SaveCustomizePluginSettingsRequest() { CustomizeId = customizeId, Xml = xml };
            return await ExecuteAsync<SaveCustomizePluginSettingsResponse>(
                SystemActions.SaveCustomizePluginSettings, request, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously enters the specified company for the current session.
        /// Also used to switch between companies — the previous company binding is overwritten.
        /// </summary>
        /// <param name="companyId">The id of the company to enter.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public virtual async Task<EnterCompanyResponse> EnterCompanyAsync(string companyId, CancellationToken cancellationToken = default)
        {
            var request = new EnterCompanyRequest()
            {
                CompanyId = companyId
            };
            return await ExecuteAsync<EnterCompanyResponse>(SystemActions.EnterCompany, request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously clears the company context from the current session.
        /// Idempotent — returns success even if the session has not entered a company.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public virtual async Task<LeaveCompanyResponse> LeaveCompanyAsync(CancellationToken cancellationToken = default)
        {
            var request = new LeaveCompanyRequest();
            return await ExecuteAsync<LeaveCompanyResponse>(SystemActions.LeaveCompany, request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously destroys the current session, clearing any company context first.
        /// Idempotent — succeeds even if the session is already expired or unknown.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <remarks>
        /// On success the client is signed out locally as well (<see cref="PolhemApiClient.SignOut"/>).
        /// </remarks>
        public virtual async Task<LogoutResponse> LogoutAsync(CancellationToken cancellationToken = default)
        {
            var request = new LogoutRequest();
            var result = await ExecuteAsync<LogoutResponse>(SystemActions.Logout, request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            Client.SignOut();
            return result;
        }
    }
}
