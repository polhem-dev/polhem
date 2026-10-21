using System.Collections.ObjectModel;
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
    /// </remarks>
    internal sealed partial class FormsViewModel : ViewModelBase
    {
        private readonly ShellOptions _options;

        /// <summary>Gets or sets whether the navigation pane is open.</summary>
        [ObservableProperty]
        private bool _isPaneOpen = true;

        /// <summary>Initializes the forms screen and starts loading the menu.</summary>
        public FormsViewModel(ShellOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            _options = options;
            MenuLoaded = LoadNavItemsAsync();
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

        private async Task LoadNavItemsAsync()
        {
            var lang = CultureInfo.CurrentUICulture.Name;
            IReadOnlyList<NavItem> items;
            try
            {
                var settings = await ClientInfo.DefineAccess.GetMenuSettingsAsync().ConfigureAwait(true);
                MenuLocalizer? localizer = ClientInfo.DefinitionLoader is { } loader
                    ? await loader.GetMenuLocalizerAsync(lang).ConfigureAwait(true)
                    : null;
                items = NavMenuBuilder.Build(settings, localizer, lang);
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

        [RelayCommand]
        private void TogglePane() => IsPaneOpen = !IsPaneOpen;
    }
}
