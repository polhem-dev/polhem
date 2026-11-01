using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Core.Messages.System;
using Polhem.Web.Blazor.Identity;

namespace Polhem.Web.Blazor.Shell
{
    /// <summary>
    /// The state of the shell's sign-in screen, which a UI package draws: the application title and hint, the company,
    /// a login panel, and a switch between the offered languages.
    /// </summary>
    /// <remarks>
    /// Signing in is two calls: the panel signs in and records the user on the <see cref="BlazorUserContext"/>, then
    /// this screen enters the company through that context, which records the capabilities the pages degrade by. When
    /// entering the company fails, the user is signed out again and the error is shown. A language switch shows only
    /// when the host registers an <see cref="IShellLanguageSwitcher"/>.
    /// </remarks>
    public abstract class ShellSignInBase : ComponentBase
    {
        /// <summary>Gets or sets the callback raised once the user has signed in and entered the company.</summary>
        [Parameter]
        public EventCallback OnSignedIn { get; set; }

        /// <summary>Gets the shell's options.</summary>
        [Inject]
        protected PolhemShellOptions Options { get; private set; } = default!;

        [Inject]
        private BlazorUserContext UserContext { get; set; } = default!;

        [Inject]
        private IServiceProvider Services { get; set; } = default!;

        /// <summary>Gets the id of the company input.</summary>
        protected string CompanyInputId { get; } = $"polhem-shell-company-{Guid.NewGuid():N}";

        /// <summary>Gets or sets the company id in the input.</summary>
        protected string CompanyId { get; set; } = string.Empty;

        /// <summary>Gets the message of a failed sign-in.</summary>
        protected string? Error { get; private set; }

        /// <summary>Gets whether the screen offers a language switch.</summary>
        protected bool OffersLanguages => Options.Languages.Count > 1 && Switcher is not null;

        private IShellLanguageSwitcher? Switcher => Services.GetService<IShellLanguageSwitcher>();

        /// <inheritdoc/>
        protected override void OnInitialized() => CompanyId = Options.DefaultCompanyId;

        /// <summary>Enters the company after the login panel signed in.</summary>
        /// <param name="response">The panel's sign-in result.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <returns>A task that completes when the company is entered or the failure is recorded.</returns>
        protected async Task OnLoggedInAsync(LoginResponse response, CancellationToken cancellationToken = default)
        {
            Error = null;
            string companyId = CompanyId.Trim();
            if (companyId.Length == 0)
            {
                Error = PolhemShellText.Get(PolhemShellText.CompanyEmpty);
                UserContext.SignOut();
                return;
            }

            try
            {
                await UserContext.EnterCompanyAsync(companyId, cancellationToken).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                // A UI boundary, like the panel's own sign-in: a refusal (such as "Company access denied") arrives as
                // whichever exception the server's error maps to, and an escaped exception would end the circuit.
                Error = PolhemShellText.Format(PolhemShellText.EnterCompanyFailed, ex.Message);
                UserContext.SignOut();
                return;
            }
            await OnSignedIn.InvokeAsync().ConfigureAwait(true);
        }

        /// <summary>Returns whether <paramref name="language"/> is the current UI culture.</summary>
        /// <param name="language">An offered language.</param>
        /// <returns><see langword="true"/> when it is current.</returns>
        protected static bool IsCurrent(PolhemShellLanguage language)
            => string.Equals(CultureInfo.CurrentUICulture.Name, language.Culture, StringComparison.OrdinalIgnoreCase);

        /// <summary>Switches to <paramref name="language"/> through the host.</summary>
        /// <param name="language">An offered language.</param>
        protected void SwitchLanguage(PolhemShellLanguage language) => Switcher?.Switch(language);
    }
}
