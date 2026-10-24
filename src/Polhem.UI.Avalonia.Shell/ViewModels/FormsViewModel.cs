using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Polhem.Definition.Language;
using Polhem.UI.ApiBridge;
using Polhem.UI.Avalonia.Shell.Localization;
using Polhem.UI.Avalonia.Shell.Navigation;

namespace Polhem.UI.Avalonia.Shell.ViewModels
{
    /// <summary>
    /// Last step: the navigation menu and the pane state. The paired view opens each form of the menu in a tab.
    /// </summary>
    /// <remarks>
    /// The menu is the server's <see cref="Polhem.Definition.Settings.MenuSettings"/>, fetched once after sign-in
    /// through <see cref="ClientInfo.DefineAccess"/>, so adding a form to the menu is a definition change, not a code
    /// change. Captions come from the <c>Menu</c> language namespace in the account's language when the client-wide
    /// definition loader is on (<see cref="MenuLocalizer"/>), and from the menu file otherwise.
    /// <para>
    /// Signing out returns to the sign-in step. The next sign-in fetches every definition again
    /// (<see cref="ClientInfo.ApplyLoginResult"/> discards the client's definition cache), so a definition changed
    /// while the application runs shows after signing out and in again; the server notices the changed files itself.
    /// </para>
    /// </remarks>
    internal sealed partial class FormsViewModel : ViewModelBase
    {
        private readonly ShellOptions _options;
        private readonly Func<Task> _signOut;
        private readonly Action _onSignedOut;

        /// <summary>Gets or sets whether the navigation pane is open.</summary>
        [ObservableProperty]
        public partial bool IsPaneOpen { get; set; } = true;

        /// <summary>Initializes the forms screen, signing out through <see cref="ClientInfo.ApiClient"/>.</summary>
        /// <param name="options">The application's options.</param>
        /// <param name="onSignedOut">Invoked after signing out.</param>
        public FormsViewModel(ShellOptions options, Action onSignedOut)
            : this(options, LoadMenuAsync, SignOutOfServerAsync, onSignedOut)
        {
        }

        /// <summary>
        /// Initializes the forms screen with the calls that load the menu and sign out, so tests need no server.
        /// </summary>
        /// <param name="options">The application's options.</param>
        /// <param name="loadMenu">Loads the menu rows.</param>
        /// <param name="signOut">Ends the session.</param>
        /// <param name="onSignedOut">Invoked after signing out, whether or not the server answered.</param>
        internal FormsViewModel(ShellOptions options, Func<Task<IReadOnlyList<NavItem>>> loadMenu, Func<Task> signOut,
            Action onSignedOut)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(loadMenu);
            ArgumentNullException.ThrowIfNull(signOut);
            ArgumentNullException.ThrowIfNull(onSignedOut);
            _options = options;
            _signOut = signOut;
            _onSignedOut = onSignedOut;
            MenuLoaded = FillNavItemsAsync(loadMenu);
        }

        /// <summary>Gets the application title.</summary>
        public string AppTitle => _options.AppTitle;

        /// <summary>Gets the menu rows.</summary>
        public ObservableCollection<NavItem> NavItems { get; } = [];

        /// <summary>
        /// Gets a task that completes once <see cref="NavItems"/> holds the menu. A failed fetch does not fault it; the
        /// failure shows as a menu row instead.
        /// </summary>
        public Task MenuLoaded { get; }

        private async Task FillNavItemsAsync(Func<Task<IReadOnlyList<NavItem>>> loadMenu)
        {
            IReadOnlyList<NavItem> items;
            try
            {
                items = await loadMenu().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                // A failed menu load must not end as an unobserved task exception; an empty menu would hide it.
                NavItems.Add(NavItem.Header(ShellLocalizer.Current.Format("Forms_MenuLoadFailed", ex.Message)));
                return;
            }

            foreach (var item in items)
                NavItems.Add(item);
        }

        private static async Task<IReadOnlyList<NavItem>> LoadMenuAsync()
        {
            var lang = CultureInfo.CurrentUICulture.Name;
            var settings = await ClientInfo.DefineAccess.GetMenuSettingsAsync().ConfigureAwait(true);
            MenuLocalizer? localizer = ClientInfo.DefinitionLoader is { } loader
                ? await loader.GetMenuLocalizerAsync(lang).ConfigureAwait(true)
                : null;
            return NavMenuBuilder.Build(settings, localizer, lang);
        }

        private static async Task SignOutOfServerAsync()
        {
            var client = ClientInfo.ApiClient;
            try
            {
                await client.System.LogoutAsync().ConfigureAwait(true);
            }
            finally
            {
                // LogoutAsync signs the client out only when the server answers. A server that was restarted no longer
                // knows the token and answers with an error, and the viewer still expects to be signed out.
                client.SignOut();
                ClientInfo.ClearCompanyContext();
            }
        }

        [RelayCommand]
        private void TogglePane() => IsPaneOpen = !IsPaneOpen;

        [RelayCommand]
        private async Task SignOutAsync()
        {
            try
            {
                await _signOut().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                // NOTE: the local session is discarded either way, so a server that did not answer is no reason to
                // keep the forms on screen. Rethrowing from a command would end as an unobserved task exception.
                Trace.TraceWarning("Sign-out did not reach the server: {0}", ex.Message);
            }

            _onSignedOut();
        }
    }
}
