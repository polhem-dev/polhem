using System.Globalization;
using Microsoft.AspNetCore.Components;
using Polhem.Api.Core.Messages.System;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Server.Identity;

namespace Polhem.Web.Blazor.Server.Shell
{
    /// <summary>
    /// The shell's sign-in screen: the application title and hint, the company, a <see cref="PolhemLoginPanel"/>, and
    /// a switch between the offered languages.
    /// </summary>
    /// <remarks>
    /// Signing in is two calls: the panel signs in and records the user on the circuit's
    /// <see cref="CircuitUserContext"/>, then this screen enters the company through that context, which records the
    /// capabilities the pages degrade by. When entering the company fails, the circuit is signed out again and the
    /// error is shown. <see cref="PolhemShellLayout"/> places it; an application with a layout of its own can place it
    /// too.
    /// <para>
    /// A circuit's culture is fixed when it connects, so the language switch goes through the endpoint of
    /// <see cref="PolhemShellEndpointRouteBuilderExtensions.MapPolhemShellCulture"/> and reloads the page. Nobody is
    /// signed in yet, so the reload loses nothing.
    /// </para>
    /// </remarks>
    public sealed partial class PolhemShellSignIn : ComponentBase
    {
        private readonly string _companyInputId = $"polhem-shell-company-{Guid.NewGuid():N}";
        private string _companyId = string.Empty;
        private string? _error;

        /// <summary>Gets or sets the callback raised once the user has signed in and entered the company.</summary>
        [Parameter]
        public EventCallback OnSignedIn { get; set; }

        [Inject]
        private CircuitUserContext UserContext { get; set; } = default!;

        [Inject]
        private NavigationManager Navigation { get; set; } = default!;

        [Inject]
        private PolhemShellOptions Options { get; set; } = default!;

        /// <inheritdoc/>
        protected override void OnInitialized() => _companyId = Options.DefaultCompanyId;

        private async Task OnLoggedInAsync(LoginResponse response)
        {
            _error = null;
            string companyId = _companyId.Trim();
            if (companyId.Length == 0)
            {
                _error = PolhemShellText.Get(PolhemShellText.CompanyEmpty);
                UserContext.SignOut();
                return;
            }

            try
            {
                await UserContext.EnterCompanyAsync(companyId).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                // A UI boundary, like the panel's own sign-in: a refusal (such as "Company access denied") arrives as
                // whichever exception the server's error maps to, and an escaped exception would end the circuit.
                _error = PolhemShellText.Format(PolhemShellText.EnterCompanyFailed, ex.Message);
                UserContext.SignOut();
                return;
            }
            await OnSignedIn.InvokeAsync().ConfigureAwait(true);
        }

        private static bool IsCurrent(PolhemShellLanguage language)
            => string.Equals(CultureInfo.CurrentUICulture.Name, language.Culture, StringComparison.OrdinalIgnoreCase);

        private void SwitchLanguage(PolhemShellLanguage language)
        {
            string returnPath = "/" + Navigation.ToBaseRelativePath(Navigation.Uri);
            Navigation.NavigateTo(PolhemShellCulture.BuildSwitchUri(language.Culture, returnPath), forceLoad: true);
        }
    }
}
