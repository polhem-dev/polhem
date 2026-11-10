using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Client;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages.System;
using Polhem.Definition.Language;
using Polhem.Web.Blazor.Identity;

namespace Polhem.Web.Blazor.Components
{
    /// <summary>
    /// The state of a minimal user-id / password panel, which a UI package draws. It calls
    /// <see cref="SystemApiConnector.LoginAsync"/> and raises <see cref="OnLoggedIn"/> with the resulting
    /// <see cref="LoginResponse"/>.
    /// </summary>
    /// <remarks>
    /// Validation is intentionally minimal: the backend BO decides what counts as a valid credential, and the panel
    /// shows its rejection as an inline error message.
    /// <para>
    /// The <see cref="LoginResponse"/> passed to <see cref="OnLoggedIn"/> carries the user's culture
    /// (<see cref="LoginResponse.Culture"/>). A circuit's culture is fixed when it starts, so a host
    /// that renders in the user's language persists that culture (typically as the ASP.NET Core
    /// request-localization cookie) and reloads.
    /// </para>
    /// <para>
    /// A successful sign-in is also recorded on the <see cref="BlazorUserContext"/>, before
    /// <see cref="OnLoggedIn"/> runs, so the host only has to enter the company through
    /// <see cref="BlazorUserContext.EnterCompanyAsync"/>.
    /// </para>
    /// </remarks>
    public abstract class LoginPanelBase : ComponentBase
    {
        private bool _isBusy;
        private string? _error;

        /// <summary>
        /// Gets or sets the label rendered above the user-id input. <c>null</c>, the default,
        /// shows the localized <see cref="PolhemUIText.UserId"/> text.
        /// </summary>
        [Parameter]
        public string? UserIdLabel { get; set; }

        /// <summary>
        /// Gets or sets the label rendered above the password input. <c>null</c>, the default,
        /// shows the localized <see cref="PolhemUIText.Password"/> text.
        /// </summary>
        [Parameter]
        public string? PasswordLabel { get; set; }

        /// <summary>
        /// Gets or sets the caption shown on the submit button. <c>null</c>, the default, shows
        /// the localized <see cref="PolhemUIText.SignIn"/> text.
        /// </summary>
        [Parameter]
        public string? SubmitLabel { get; set; }

        /// <summary>
        /// Gets or sets the user id the panel starts with. Read once, when the panel is initialized.
        /// </summary>
        [Parameter]
        public string? InitialUserId { get; set; }

        /// <summary>
        /// Gets or sets the password the panel starts with, meant for demonstration accounts only. Read once, when the
        /// panel is initialized; the panel clears it after a successful sign-in.
        /// </summary>
        [Parameter]
        public string? InitialPassword { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked after a successful login.
        /// </summary>
        [Parameter]
        public EventCallback<LoginResponse> OnLoggedIn { get; set; }

        [Inject]
        private PolhemApiClient Client { get; set; } = default!;

        // Nullable: a component created outside a renderer has no services, and still renders.
        [Inject]
        private IServiceProvider? Services { get; set; }

        /// <summary>Gets or sets the user id in the input.</summary>
        protected string UserId { get; set; } = string.Empty;

        /// <summary>Gets or sets the password in the input.</summary>
        protected string Password { get; set; } = string.Empty;

        /// <summary>Gets whether a sign-in is running.</summary>
        protected bool IsBusy => _isBusy;

        /// <summary>Gets the message of a failed sign-in.</summary>
        protected string? Error => _error;

        /// <summary>Gets the user-id label to show.</summary>
        protected string DisplayedUserIdLabel => UserIdLabel ?? Text(PolhemUIText.UserId);

        /// <summary>Gets the password label to show.</summary>
        protected string DisplayedPasswordLabel => PasswordLabel ?? Text(PolhemUIText.Password);

        /// <summary>Gets the submit caption to show.</summary>
        protected string DisplayedSubmitLabel => SubmitLabel ?? Text(PolhemUIText.SignIn);

        private string Text(string key) => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(Services), key);

        /// <inheritdoc/>
        protected override void OnInitialized()
        {
            UserId = InitialUserId ?? string.Empty;
            Password = InitialPassword ?? string.Empty;
        }

        /// <summary>Signs in with the values in the inputs.</summary>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <returns>A task that completes when the sign-in has succeeded or its failure is recorded.</returns>
        protected async Task OnSubmitAsync(CancellationToken cancellationToken = default)
        {
            if (_isBusy) return;
            _isBusy = true;
            _error = null;
            try
            {
                var response = await Client.System.LoginAsync(UserId, Password, cancellationToken).ConfigureAwait(true);

                if (response.AccessToken == Guid.Empty)
                {
                    _error = Text(PolhemUIText.SignInEmptyToken);
                    return;
                }

                Password = string.Empty;
                Services?.GetService<BlazorUserContext>()?.ApplyLoginResult(response);

                if (OnLoggedIn.HasDelegate)
                {
                    await OnLoggedIn.InvokeAsync(response).ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                // Surface the failure inline rather than letting it tear down
                // the SignalR circuit. The backend already classifies the
                // error (bad credentials, locked account, network) into the
                // exception message, which is what the user sees.
                _error = ex.Message;
            }
            finally
            {
                _isBusy = false;
            }
        }
    }
}
