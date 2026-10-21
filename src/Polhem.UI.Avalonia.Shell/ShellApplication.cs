using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Client;
using Polhem.UI.ApiBridge;
using Polhem.UI.ApiBridge.DependencyInjection;
using Polhem.UI.Avalonia.Shell.Localization;
using Polhem.UI.Avalonia.Shell.Preferences;
using Polhem.UI.Avalonia.Shell.ViewModels;
using Polhem.UI.Avalonia.Shell.Views;
using Polhem.UI.Core;
using Semi.Avalonia;
using Semi.Avalonia.DataGrid;

namespace Polhem.UI.Avalonia.Shell
{
    /// <summary>
    /// The Avalonia application of the shell: connect to a Polhem backend, sign in to a company, and open the forms
    /// of the server's menu, each in a tab.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A platform head starts it like any Avalonia application, with <c>AppBuilder.Configure&lt;T&gt;()</c>. An
    /// application derives from it and overrides <see cref="CreateOptions"/> to give its title and the values the
    /// screens start with; the shell itself names no application.
    /// </para>
    /// <para>
    /// The shell applies the Semi theme (dark by default; the forms screen has a light/dark switch), registers the
    /// API bridge as <see cref="UIServices.Provider"/>, and configures <see cref="ClientInfo"/> for a remote
    /// backend with <see cref="ShellOptions.DefaultApiKey"/> as the first-run key. Fonts are the head's choice, so the
    /// shell adds none.
    /// </para>
    /// </remarks>
    public class ShellApplication : Application
    {
        /// <summary>
        /// Gets or sets a callback that adjusts the options after <see cref="CreateOptions"/>, for what differs per
        /// platform head, such as the preference storage of a browser head.
        /// </summary>
        public Action<ShellOptions>? ConfigureOptions { get; set; }

        /// <summary>
        /// Gets the options in effect, available once <see cref="Initialize"/> has run.
        /// </summary>
        protected ShellOptions? Options { get; private set; }

        /// <summary>
        /// Returns the application's options. The default returns <see cref="ShellOptions"/> with its defaults.
        /// </summary>
        protected virtual ShellOptions CreateOptions() => new();

        /// <inheritdoc/>
        public override void Initialize()
        {
            var options = CreateOptions();
            ConfigureOptions?.Invoke(options);
            Options = options;
            // NOTE: the name must be set here, before the platform builds its menus; the macOS application menu
            // reads it once and shows "Avalonia Application" otherwise.
            Name = options.AppTitle;

            RequestedThemeVariant = ThemeVariant.Dark;
            Styles.Add(new SemiTheme());
            Styles.Add(new DataGridSemiTheme());
            DataTemplates.Add(new ShellViewLocator());
        }

        /// <inheritdoc/>
        public override void OnFrameworkInitializationCompleted()
        {
            var options = Options ?? throw new InvalidOperationException("Initialize has not run.");

            // Before any view model reads them: the connection screen pre-fills the stored key.
            ClientInfo.SupportedConnectTypes = SupportedConnectTypes.Remote;
            ClientInfo.ApplyApiKey(options.DefaultApiKey);

            // The views resolve their connector, definitions and user context from the provider; the bridge reads
            // them from ClientInfo, so this registration holds across the connection, sign-in and company steps.
            UIServices.Provider = new ServiceCollection().AddPolhemUIApiBridge().BuildServiceProvider();

            var preferences = options.PreferenceStorage ?? new FileShellPreferenceStorage(options.ResolveStorageName());
            var remembered = options.AllowLanguageSwitch ? preferences.Load(ShellPreferenceKeys.Language) : string.Empty;
            ShellLocalizer.Current.Culture = CultureInfo.GetCultureInfo(
                ShellLanguage.ResolveInitial(remembered, CultureInfo.CurrentUICulture));

            var viewModel = new ShellViewModel(options, preferences);
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new ShellWindow(viewModel);
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            {
                singleView.MainView = new ShellView { DataContext = viewModel };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
