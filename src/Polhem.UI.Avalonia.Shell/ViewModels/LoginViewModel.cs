using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Polhem.UI.ApiBridge;
using Polhem.UI.Avalonia.Shell.Localization;
using Polhem.UI.Avalonia.Shell.Preferences;

namespace Polhem.UI.Avalonia.Shell.ViewModels
{
    /// <summary>
    /// Second step: signs in and enters the company, storing both results on <see cref="ClientInfo"/>, then moves the
    /// shell on to the forms.
    /// </summary>
    /// <remarks>
    /// Sign-in is two calls even for a single company: <c>Login</c> answers who the caller is, and
    /// <c>EnterCompany</c> fills the company-scoped half of the session. Without it the token authenticates but
    /// opens no <c>CategoryId="company"</c> form.
    /// <para>
    /// The company starts as the last one entered on this device, else the application's default. A successful
    /// sign-in remembers it, and makes the account's culture the shell's language.
    /// </para>
    /// </remarks>
    internal sealed partial class LoginViewModel : ViewModelBase
    {
        private readonly IShellPreferenceStorage _preferences;
        private readonly Func<string, string, string, Task<SignInResult>> _signIn;
        private readonly Action _onLoggedIn;

        /// <summary>Gets or sets the company id entered after sign-in.</summary>
        [ObservableProperty]
        public partial string CompanyId { get; set; }

        /// <summary>Gets or sets the user id.</summary>
        [ObservableProperty]
        public partial string UserId { get; set; }

        /// <summary>Gets or sets the password.</summary>
        [ObservableProperty]
        public partial string Password { get; set; }

        /// <summary>Gets whether the sign-in round trip is in flight.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        public partial bool IsBusy { get; set; }

        /// <summary>
        /// Initializes a new instance that signs in through <see cref="ClientInfo.SystemApiConnector"/>.
        /// </summary>
        public LoginViewModel(ShellOptions options, IShellPreferenceStorage preferences, Action onLoggedIn)
            : this(options, preferences, SignInAsync, onLoggedIn)
        {
        }

        /// <summary>
        /// Initializes a new instance with the call that signs in, so tests need no server.
        /// </summary>
        /// <param name="options">The application's options.</param>
        /// <param name="preferences">Where the last company is remembered.</param>
        /// <param name="signIn">Signs in with a user id, a password and a company id.</param>
        /// <param name="onLoggedIn">Invoked after a successful sign-in.</param>
        internal LoginViewModel(ShellOptions options, IShellPreferenceStorage preferences,
            Func<string, string, string, Task<SignInResult>> signIn, Action onLoggedIn)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(preferences);
            ArgumentNullException.ThrowIfNull(signIn);
            ArgumentNullException.ThrowIfNull(onLoggedIn);
            _preferences = preferences;
            _signIn = signIn;
            _onLoggedIn = onLoggedIn;

            SignInHint = options.SignInHint;
            UserId = options.DefaultUserId;
            Password = options.DefaultPassword;
            var remembered = preferences.Load(ShellPreferenceKeys.CompanyId);
            CompanyId = string.IsNullOrWhiteSpace(remembered) ? options.DefaultCompanyId : remembered;
        }

        /// <summary>Gets the line shown under the title; empty shows none.</summary>
        public string SignInHint { get; }

        /// <summary>Gets whether <see cref="SignInHint"/> has text.</summary>
        public bool HasSignInHint => SignInHint.Length > 0;

        /// <summary>Gets the inverse of <see cref="IsBusy"/>, bound to the inputs' <c>IsEnabled</c>.</summary>
        public bool IsNotBusy => !IsBusy;

        [RelayCommand]
        private async Task LoginAsync()
        {
            if (IsBusy) return;

            var companyId = CompanyId?.Trim() ?? string.Empty;
            if (companyId.Length == 0)
            {
                SetStatus("Status_CompanyEmpty", isError: true);
                return;
            }

            IsBusy = true;
            SetStatus("Status_Authenticating", isError: false);
            try
            {
                var result = await _signIn(UserId?.Trim() ?? string.Empty, Password ?? string.Empty, companyId)
                    .ConfigureAwait(true);

                _preferences.Save(ShellPreferenceKeys.CompanyId, companyId);
                ApplyAccountLanguage(result.Culture);
                SetStatus("Status_Welcome", isError: false, result.UserName);
                _onLoggedIn();
            }
            catch (Exception ex)
            {
                // NOTE: any failure is the viewer's to see; rethrowing from a command would end as an unobserved
                // task exception.
                SetStatus("Status_LoginFailed", isError: true, ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static void ApplyAccountLanguage(string culture)
        {
            if (string.IsNullOrWhiteSpace(culture)) return;

            CultureInfo info;
            try
            {
                info = CultureInfo.GetCultureInfo(culture);
            }
            catch (CultureNotFoundException)
            {
                // An unknown culture leaves the language as it was, as ClientInfo does for the process.
                return;
            }

            // IMPORTANT: ClientInfo.ApplyLoginResult set the culture inside the sign-in method, and an async method's
            // culture reverts when it returns. The forms screen is built further down this flow, and the menu and the
            // first form read the culture as they load, so it is set again here. The UI thread keeps it afterwards
            // through the callback ClientInfo posted.
            CultureInfo.CurrentCulture = info;
            CultureInfo.CurrentUICulture = info;
            ShellLocalizer.Current.Culture = info;
        }

        private static async Task<SignInResult> SignInAsync(string userId, string password, string companyId)
        {
            // Both calls go through the same connector: the login signed in the client it belongs to.
            // ApplyEnterCompanyResult also flushes the definition cache, which is why the company is entered before
            // any form opens rather than lazily on the first one that needs it. ConfigureAwait(true) keeps the
            // culture ApplyLoginResult posts on the UI thread.
            var connector = ClientInfo.SystemApiConnector;
            var login = await connector.LoginAsync(userId, password).ConfigureAwait(true);
            ClientInfo.ApplyLoginResult(login);

            var company = await connector.EnterCompanyAsync(companyId).ConfigureAwait(true);
            ClientInfo.ApplyEnterCompanyResult(company);

            return new SignInResult(login.UserName, ClientInfo.UserInfo?.Culture ?? string.Empty);
        }
    }
}
