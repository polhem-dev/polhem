using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages.System;
using Polhem.Definition.Language;
using Polhem.Web.Blazor.Server.DependencyInjection;
using Microsoft.AspNetCore.Components;

namespace Polhem.Web.Blazor.Server.Components
{
    /// <summary>
    /// Minimal user-id / password panel that calls
    /// <see cref="SystemApiConnector.LoginAsync"/> and raises
    /// <see cref="OnLoggedIn"/> with the resulting <see cref="LoginResponse"/>.
    /// Pair it with <see cref="PolhemAccessTokenProvider"/> to keep the access
    /// token cascading through the component tree.
    /// </summary>
    /// <remarks>
    /// The panel ships with a no-frills default look so it can drop in unstyled;
    /// hosts that want richer UI should provide their own login page and call
    /// <see cref="PolhemAccessTokenProvider.SetToken"/> directly. Validation is
    /// also intentionally minimal — the backend BO decides what counts as a
    /// valid credential, and the panel shows its rejection as an inline error
    /// message.
    /// <para>
    /// The <see cref="LoginResponse"/> passed to <see cref="OnLoggedIn"/> carries the user's culture
    /// (<see cref="LoginResponse.Culture"/>). A circuit's culture is fixed when it starts, so a host
    /// that renders in the user's language persists that culture — typically as the ASP.NET Core
    /// request-localization cookie — and reloads.
    /// </para>
    /// </remarks>
    public sealed partial class PolhemLoginPanel : ComponentBase
    {
        private readonly string _userIdInputId = $"polhem-login-user-{Guid.NewGuid():N}";
        private readonly string _passwordInputId = $"polhem-login-pwd-{Guid.NewGuid():N}";

        private string _userId = string.Empty;
        private string _password = string.Empty;
        private bool _isBusy;
        private string? _error;

        /// <summary>
        /// Gets or sets the label rendered above the user-id input. <c>null</c> — the default —
        /// shows the localized <see cref="PolhemUIText.UserId"/> text.
        /// </summary>
        [Parameter]
        public string? UserIdLabel { get; set; }

        /// <summary>
        /// Gets or sets the label rendered above the password input. <c>null</c> — the default —
        /// shows the localized <see cref="PolhemUIText.Password"/> text.
        /// </summary>
        [Parameter]
        public string? PasswordLabel { get; set; }

        /// <summary>
        /// Gets or sets the caption shown on the submit button. <c>null</c> — the default — shows
        /// the localized <see cref="PolhemUIText.SignIn"/> text.
        /// </summary>
        [Parameter]
        public string? SubmitLabel { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked after a successful login. The
        /// host typically wires this to
        /// <c>response =&gt; auth.SetToken(response.AccessToken)</c> against
        /// an enclosing <see cref="PolhemAccessTokenProvider"/>.
        /// </summary>
        [Parameter]
        public EventCallback<LoginResponse> OnLoggedIn { get; set; }

        [Inject]
        private PolhemApiConnectorFactory Factory { get; set; } = default!;

        // Nullable: a component created outside a renderer has no services, and still renders.
        [Inject]
        private IServiceProvider? Services { get; set; }

        private string Text(string key) => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(Services), key);

        private string DisplayedUserIdLabel => UserIdLabel ?? Text(PolhemUIText.UserId);

        private string DisplayedPasswordLabel => PasswordLabel ?? Text(PolhemUIText.Password);

        private string DisplayedSubmitLabel => SubmitLabel ?? Text(PolhemUIText.SignIn);

        private async Task OnSubmitAsync()
        {
            if (_isBusy) return;
            _isBusy = true;
            _error = null;
            try
            {
                var system = Factory.CreateSystemConnector(Guid.Empty);
                var response = await system.LoginAsync(_userId, _password).ConfigureAwait(true);

                if (response.AccessToken == Guid.Empty)
                {
                    _error = Text(PolhemUIText.SignInEmptyToken);
                    return;
                }

                _password = string.Empty;

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
