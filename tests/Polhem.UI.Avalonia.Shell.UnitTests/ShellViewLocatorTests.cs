using System.ComponentModel;
using Polhem.UI.Avalonia.Shell.Navigation;
using Polhem.UI.Avalonia.Shell.Preferences;
using Polhem.UI.Avalonia.Shell.ViewModels;
using Polhem.UI.Avalonia.Shell.Views;

namespace Polhem.UI.Avalonia.Shell.UnitTests;

[Collection(AvaloniaControlsCollection.Name)]
public class ShellViewLocatorTests(HeadlessSessionFixture session)
{
    private static readonly PolhemShellOptions s_options = new() { AppTitle = "Demo" };

    private static ConnectionViewModel Connection() => new(s_options, () => { });

    private static LoginViewModel Login() => new(s_options, new NullPreferences(),
        (_, _, _) => Task.FromResult(new SignInResult("Demo", string.Empty)), () => { });

    private static FormsViewModel Forms() => new(s_options,
        () => Task.FromResult<IReadOnlyList<NavItem>>([]), () => Task.CompletedTask, () => { });

    [Fact]
    [DisplayName("The connection step is shown by the connection view")]
    public Task Build_ConnectionViewModel_ReturnsConnectionView() => session.RunAsync(() =>
    {
        Assert.IsType<ConnectionView>(new ShellViewLocator().Build(Connection()));
    });

    [Fact]
    [DisplayName("The sign-in step is shown by the sign-in view")]
    public Task Build_LoginViewModel_ReturnsLoginView() => session.RunAsync(() =>
    {
        Assert.IsType<LoginView>(new ShellViewLocator().Build(Login()));
    });

    [Fact]
    [DisplayName("The forms step is shown by the forms view")]
    public Task Build_FormsViewModel_ReturnsFormsView() => session.RunAsync(() =>
    {
        Assert.IsType<FormsView>(new ShellViewLocator().Build(Forms()));
    });

    [Fact]
    [DisplayName("An object that is not a shell step builds no view")]
    public Task Build_OtherObject_ReturnsNull() => session.RunAsync(() =>
    {
        Assert.Null(new ShellViewLocator().Build("not a view model"));
    });

    [Fact]
    [DisplayName("The locator matches each shell step")]
    public Task Match_ShellSteps_ReturnsTrue() => session.RunAsync(() =>
    {
        var locator = new ShellViewLocator();

        Assert.True(locator.Match(Connection()));
        Assert.True(locator.Match(Login()));
        Assert.True(locator.Match(Forms()));
    });

    [Fact]
    [DisplayName("The locator does not match other objects or null")]
    public Task Match_OtherObjects_ReturnsFalse() => session.RunAsync(() =>
    {
        var locator = new ShellViewLocator();

        Assert.False(locator.Match("not a view model"));
        Assert.False(locator.Match(null));
    });

    private sealed class NullPreferences : IShellPreferenceStorage
    {
        public string Load(string key) => string.Empty;

        public void Save(string key, string value)
        {
            // Nothing is remembered; the locator tests never save.
        }
    }
}
