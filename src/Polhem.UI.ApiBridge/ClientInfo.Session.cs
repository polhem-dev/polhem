using System.Globalization;
using Polhem.Api.Client;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages.System;
using Polhem.Core;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;

namespace Polhem.UI.ApiBridge
{
    /// <summary>
    /// Signed-in state of <see cref="ClientInfo"/>: the login result, the entered company and its capabilities, and the
    /// user's culture. Split out for file size only; behaviour is unchanged.
    /// </summary>
    public static partial class ClientInfo
    {
        /// <summary>
        /// The per-model capability snapshot for the entered company, or <c>null</c> when no company
        /// context is active (before <see cref="ApplyEnterCompanyResult"/>, or after
        /// <see cref="ClearCompanyContext"/> / a sign-in / an endpoint change).
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

        // Callers hold `s_stateGate`.
        private static void DiscardIdentityState()
        {
            s_defineAccess = null;
            s_definitionLoader = null;
            // A new identity, or none: the cached capability snapshot no longer applies. Null disables degradation
            // until the next EnterCompany populates it.
            s_capabilities = null;
        }

        /// <summary>
        /// Applies the login response, populating <see cref="UserInfo"/> and the session's time zone,
        /// discarding the previous identity's cached state, and making the user's culture this process's culture.
        /// </summary>
        /// <param name="loginResponse">Result returned from the login API.</param>
        /// <remarks>
        /// <para>
        /// The culture the server returns — <c>st_user.culture</c>, or the deployment's default
        /// language — becomes <see cref="CultureInfo.CurrentUICulture"/> and
        /// <see cref="CultureInfo.CurrentCulture"/>, for the current flow and as the process default
        /// (<see cref="CultureInfo.DefaultThreadCurrentUICulture"/>). When the caller has a
        /// <see cref="SynchronizationContext"/>, as a UI thread does, the culture is also posted to it, so the UI
        /// thread keeps it after an async sign-in handler resumes. Everything that localizes reads
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
            // `LoginAsync` has signed the session in already. Signing it in again here does two things: it applies
            // the zone of UserInfo, which falls back to its default where the server sent none, and it accepts a
            // response from a sign-in made elsewhere. The key belongs to the token it was exchanged with, so it is
            // kept only when the token is the one the session already holds.
            var session = ApiClient.Session;
            var current = session.Credentials;
            lock (s_stateGate)
            {
                session.SignIn(new ApiSessionCredentials(loginResponse.AccessToken,
                    current.AccessToken == loginResponse.AccessToken ? current.ApiEncryptionKey : [],
                    UserInfo.TimeZone));
                DiscardIdentityState();
            }
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

            // IMPORTANT: the assignments above do not stick on a UI thread when the caller is an async method,
            // which a sign-in handler always is. The culture flows with the execution context, so the async
            // machinery reverts it at the caller's next `await`. Avalonia 12.0 left the UI thread without a culture
            // of its own, so it fell back to the defaults set above. Avalonia 12.1 (AvaloniaUI/Avalonia#21627)
            // writes the thread's live culture back after every dispatcher operation, which pins the reverted one.
            // A plain callback posted to the UI thread's context sets the culture outside any async method, and
            // Avalonia keeps whatever such a callback leaves.
            SynchronizationContext.Current?.Post(static state =>
            {
                var posted = (CultureInfo)state!;
                CultureInfo.CurrentCulture = posted;
                CultureInfo.CurrentUICulture = posted;
            }, info);
            return true;
        }
    }
}
