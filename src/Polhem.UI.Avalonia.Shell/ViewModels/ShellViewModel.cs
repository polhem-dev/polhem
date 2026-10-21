using CommunityToolkit.Mvvm.ComponentModel;
using Polhem.UI.Avalonia.Shell.Localization;
using Polhem.UI.Avalonia.Shell.Preferences;

namespace Polhem.UI.Avalonia.Shell.ViewModels
{
    /// <summary>
    /// The root view model: drives the connection → sign-in → forms flow by swapping <see cref="CurrentView"/>, and
    /// owns the interface language before sign-in.
    /// </summary>
    /// <remarks>
    /// Each step gets a callback that advances to the next one, which keeps the navigation graph explicit and leaves
    /// nothing to unsubscribe. The language switch is offered only before sign-in; afterwards the account's culture
    /// decides.
    /// </remarks>
    internal sealed partial class ShellViewModel : ViewModelBase
    {
        private readonly ShellOptions _options;
        private readonly IShellPreferenceStorage _preferences;

        /// <summary>Gets the view model of the step on screen.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLanguageSwitchVisible))]
        private ViewModelBase _currentView;

        /// <summary>Gets or sets the language chosen in the switch.</summary>
        [ObservableProperty]
        private ShellLanguageOption _selectedLanguage;

        /// <summary>Initializes the shell on the connection step.</summary>
        public ShellViewModel(ShellOptions options, IShellPreferenceStorage preferences)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(preferences);
            _options = options;
            _preferences = preferences;

            var culture = ShellLocalizer.Current.Culture.Name;
            _selectedLanguage = Languages.FirstOrDefault(o => string.Equals(o.Code, culture, StringComparison.OrdinalIgnoreCase))
                ?? Languages[0];
            _currentView = new ConnectionViewModel(options, NavigateToLogin);

            // The shell lives as long as the application, so this subscription needs no removal.
            ShellLocalizer.Current.CultureChanged += (_, _) => CurrentView.RefreshText();
        }

        /// <summary>Gets the application title.</summary>
        public string AppTitle => _options.AppTitle;

        /// <summary>Gets the languages the switch offers.</summary>
        public IReadOnlyList<ShellLanguageOption> Languages { get; } = ShellLanguage.Options;

        /// <summary>Gets whether the language switch is shown.</summary>
        public bool IsLanguageSwitchVisible => _options.AllowLanguageSwitch && CurrentView is not FormsViewModel;

        partial void OnSelectedLanguageChanged(ShellLanguageOption value)
        {
            if (value is null) return;

            ShellLocalizer.Current.Culture = System.Globalization.CultureInfo.GetCultureInfo(value.Code);
            _preferences.Save(ShellPreferenceKeys.Language, value.Code);
        }

        private void NavigateToLogin()
            => CurrentView = new LoginViewModel(_options, _preferences, NavigateToForms);

        private void NavigateToForms()
            => CurrentView = new FormsViewModel(_options);
    }
}
