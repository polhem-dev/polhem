using Polhem.Api.Core.Messages.System;
using Polhem.Definition;
using Polhem.Definition.Security;

namespace Polhem.Api.Client.Connectors
{
    public partial class SystemApiConnector
    {
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
    }
}
