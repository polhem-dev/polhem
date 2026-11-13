using Polhem.Api.Client;
using Polhem.Api.Core.Messages.System;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;
using Polhem.UI.Core;

namespace Polhem.Web.Blazor.Identity
{
    /// <summary>
    /// The signed-in user's context for one circuit: who signed in, the company they entered and their capabilities.
    /// <c>FormPage</c> reads it to hide the commands the user may not run,
    /// to degrade sensitive fields, and to preview numbers with the company's decimals.
    /// </summary>
    /// <remarks>
    /// <c>AddPolhemBlazor</c> registers it scoped, next to the circuit's
    /// <see cref="PolhemApiClient"/>, and as the circuit's <see cref="IUserContext"/>. It is the multi-user counterpart
    /// of the desktop client's process-wide context: each circuit has its own, so one user's capabilities never reach
    /// another user's page.
    /// <para>
    /// <c>PolhemLoginPanel</c> applies the sign-in itself. A host enters the
    /// company through <see cref="EnterCompanyAsync"/> and signs out through <see cref="SignOut"/>, which keep the
    /// context and the client in step. A company entered by calling the client's <c>System.EnterCompanyAsync</c>
    /// directly leaves the context without capabilities, and the pages then treat the user as unrestricted. The
    /// capabilities only shape the UI; the backend still checks every call.
    /// </para>
    /// </remarks>
    public sealed class BlazorUserContext : IUserContext
    {
        private readonly PolhemApiClient _client;
        private readonly string _defaultLanguage;

        /// <summary>
        /// Initializes a new instance of <see cref="BlazorUserContext"/> over a circuit's client.
        /// </summary>
        /// <param name="client">The circuit's client.</param>
        /// <param name="defaultLanguage">The value of <see cref="DefaultLanguage"/>.</param>
        public BlazorUserContext(PolhemApiClient client, string defaultLanguage)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(defaultLanguage);
            _client = client;
            _defaultLanguage = defaultLanguage;
        }

        /// <inheritdoc/>
        public UserInfo? User { get; private set; }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, PermissionActions>? Capabilities { get; private set; }

        /// <inheritdoc/>
        public CompanyInfo? Company { get; private set; }

        /// <inheritdoc/>
        public string DefaultLanguage => _defaultLanguage;

        /// <summary>
        /// Records the user a sign-in returned. A new sign-in drops the company and the capabilities of the previous
        /// one until <see cref="EnterCompanyAsync"/> runs again.
        /// </summary>
        /// <param name="response">The response of the sign-in.</param>
        public void ApplyLoginResult(LoginResponse response)
        {
            ArgumentNullException.ThrowIfNull(response);
            User = new UserInfo
            {
                UserId = response.UserId,
                UserName = response.UserName,
                TimeZone = response.TimeZone ?? string.Empty,
                Culture = response.Culture ?? string.Empty,
            };
            Company = null;
            Capabilities = null;
        }

        /// <summary>
        /// Enters <paramref name="companyId"/> through the circuit's client and records the company and the
        /// capabilities the server returned.
        /// </summary>
        /// <param name="companyId">The company to enter.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <returns>The server's response.</returns>
        public async Task<EnterCompanyResponse> EnterCompanyAsync(string companyId, CancellationToken cancellationToken = default)
        {
            // Back on the circuit's dispatcher, so a render never sees the new company with the old capabilities.
            var response = await _client.System.EnterCompanyAsync(companyId, cancellationToken).ConfigureAwait(true);
            Company = response.Company;
            Capabilities = new Dictionary<string, PermissionActions>(response.Capabilities, StringComparer.Ordinal);
            return response;
        }

        /// <summary>
        /// Signs the circuit's client out and clears this context.
        /// </summary>
        public void SignOut()
        {
            _client.SignOut();
            User = null;
            Company = null;
            Capabilities = null;
        }
    }
}
