using System.ComponentModel;
using System.Globalization;
using Polhem.UI.Avalonia.Shell.Localization;
using Polhem.UI.Avalonia.Shell.Navigation;
using Polhem.UI.Avalonia.Shell.Preferences;
using Polhem.UI.Avalonia.Shell.ViewModels;

namespace Polhem.UI.Avalonia.Shell.UnitTests;

/// <summary>
/// The shell's navigation (connection, sign-in, forms, and back to sign-in) and its language switch. Each step is a
/// stand-in that hands its callback to the test, so no step needs a server.
/// </summary>
/// <remarks>
/// Each test gives the shell a localizer of its own. Switching the process-wide <see cref="ShellLocalizer.Current"/>
/// would update the views earlier headless tests left bound to it, from a thread that does not own them.
/// </remarks>
public sealed class ShellViewModelTests
{
    private readonly ShellLocalizer _localizer = new();
    private readonly MemoryPreferences _preferences = new();
    private Action? _onConnected;
    private Action? _onLoggedIn;
    private Action? _onSignedOut;

    [Fact]
    [DisplayName("The shell starts on the connection step with the language switch shown")]
    public void Constructor_StartsOnConnectionStep()
    {
        var shell = CreateShell(new PolhemShellOptions { AppTitle = "Demo" });

        Assert.IsType<StepViewModel>(shell.CurrentView);
        Assert.Equal("connection", ((StepViewModel)shell.CurrentView).Name);
        Assert.Equal("Demo", shell.AppTitle);
        Assert.True(shell.IsLanguageSwitchVisible);
    }

    [Fact]
    [DisplayName("Connecting moves to sign-in, and signing in moves to the forms and hides the language switch")]
    public void Navigation_ConnectThenSignIn_ReachesFormsWithoutSwitch()
    {
        var shell = CreateShell(new PolhemShellOptions());
        var changed = new List<string?>();
        shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _onConnected!();
        Assert.Equal("login", Assert.IsType<StepViewModel>(shell.CurrentView).Name);
        Assert.True(shell.IsLanguageSwitchVisible);

        _onLoggedIn!();
        Assert.IsType<FormsViewModel>(shell.CurrentView);
        Assert.False(shell.IsLanguageSwitchVisible);
        Assert.Contains(nameof(ShellViewModel.IsLanguageSwitchVisible), changed);
    }

    [Fact]
    [DisplayName("Signing out returns to sign-in in the language the switch names, not the account's")]
    public void Navigation_SignOut_ReturnsToSignInInSwitchLanguage()
    {
        var shell = CreateShell(new PolhemShellOptions());
        _onConnected!();
        _onLoggedIn!();
        // Signing in made the account's culture the shell's language.
        _localizer.Culture = CultureInfo.GetCultureInfo(ShellLanguage.TraditionalChinese);

        _onSignedOut!();

        Assert.Equal("login", Assert.IsType<StepViewModel>(shell.CurrentView).Name);
        Assert.True(shell.IsLanguageSwitchVisible);
        Assert.Equal(ShellLanguage.English, _localizer.Culture.Name);
    }

    [Fact]
    [DisplayName("The switch starts on the shell's current language and does not save a preference until someone chooses")]
    public void Constructor_SelectsCurrentLanguageWithoutSaving()
    {
        _localizer.Culture = CultureInfo.GetCultureInfo(ShellLanguage.TraditionalChinese);

        var shell = CreateShell(new PolhemShellOptions());

        Assert.Equal(ShellLanguage.TraditionalChinese, shell.SelectedLanguage.Code);
        Assert.Empty(_preferences.Load(ShellPreferenceKeys.Language));
    }

    [Fact]
    [DisplayName("Choosing a language switches the shell's text and remembers the choice")]
    public void SelectedLanguage_Changed_SwitchesAndSaves()
    {
        var shell = CreateShell(new PolhemShellOptions());

        shell.SelectedLanguage = shell.Languages.Single(o => o.Code == ShellLanguage.TraditionalChinese);

        Assert.Equal(ShellLanguage.TraditionalChinese, _localizer.Culture.Name);
        Assert.Equal(ShellLanguage.TraditionalChinese, _preferences.Load(ShellPreferenceKeys.Language));
    }

    [Fact]
    [DisplayName("A language change re-renders the text of the step on screen")]
    public void CultureChanged_RefreshesCurrentStep()
    {
        var shell = CreateShell(new PolhemShellOptions());
        var step = Assert.IsType<StepViewModel>(shell.CurrentView);

        _localizer.Culture = CultureInfo.GetCultureInfo(ShellLanguage.TraditionalChinese);

        Assert.Equal(1, step.RefreshCount);
    }

    [Fact]
    [DisplayName("An application that turns the language switch off never shows it")]
    public void IsLanguageSwitchVisible_SwitchDisabled_IsFalse()
    {
        var shell = CreateShell(new PolhemShellOptions { AllowLanguageSwitch = false });

        Assert.False(shell.IsLanguageSwitchVisible);
    }

    private ShellViewModel CreateShell(PolhemShellOptions options)
        => new(options, _preferences, _localizer,
            onConnected => { _onConnected = onConnected; return new StepViewModel("connection"); },
            onLoggedIn => { _onLoggedIn = onLoggedIn; return new StepViewModel("login"); },
            onSignedOut =>
            {
                _onSignedOut = onSignedOut;
                return new FormsViewModel(options, () => Task.FromResult<IReadOnlyList<NavItem>>([]),
                    () => Task.CompletedTask, onSignedOut);
            });

    private sealed class StepViewModel(string name) : ViewModelBase
    {
        public string Name { get; } = name;

        public int RefreshCount { get; private set; }

        public override void RefreshText()
        {
            base.RefreshText();
            RefreshCount++;
        }
    }

    private sealed class MemoryPreferences : IShellPreferenceStorage
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public string Load(string key) => _values.TryGetValue(key, out var value) ? value : string.Empty;

        public void Save(string key, string value) => _values[key] = value;
    }
}
