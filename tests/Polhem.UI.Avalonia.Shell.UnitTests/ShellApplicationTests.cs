using System.ComponentModel;
using Avalonia.Styling;

namespace Polhem.UI.Avalonia.Shell.UnitTests;

/// <summary>
/// <see cref="ShellApplication.Initialize"/>, which only configures the application instance.
/// <see cref="ShellApplication.OnFrameworkInitializationCompleted"/> configures the process-wide client and is covered
/// only for its guard here.
/// </summary>
[Collection(AvaloniaControlsCollection.Name)]
public class ShellApplicationTests(HeadlessSessionFixture session)
{
    private sealed class DemoApplication : ShellApplication
    {
        protected override PolhemShellOptions CreateOptions()
            => new() { AppTitle = "Northwind", DefaultThemeVariant = ThemeVariant.Dark };

        public PolhemShellOptions? CurrentOptions => Options;
    }

    [Fact]
    [DisplayName("Initialize names the application after the title of its options")]
    public Task Initialize_DerivedOptions_SetsName() => session.RunAsync(() =>
    {
        var app = new DemoApplication();

        app.Initialize();

        Assert.Equal("Northwind", app.Name);
    });

    [Fact]
    [DisplayName("Initialize requests the theme variant the options start with")]
    public Task Initialize_DerivedOptions_SetsThemeVariant() => session.RunAsync(() =>
    {
        var app = new DemoApplication();

        app.Initialize();

        Assert.Equal(ThemeVariant.Dark, app.RequestedThemeVariant);
    });

    [Fact]
    [DisplayName("Initialize applies the head's adjustments after the application's options")]
    public Task Initialize_ConfigureOptions_AdjustsOptions() => session.RunAsync(() =>
    {
        var app = new DemoApplication { ConfigureOptions = o => o.AppTitle = "Northwind (browser)" };

        app.Initialize();

        Assert.Equal("Northwind (browser)", app.CurrentOptions?.AppTitle);
        Assert.Equal("Northwind (browser)", app.Name);
    });

    [Fact]
    [DisplayName("Initialize registers the shell's view locator and theme styles")]
    public Task Initialize_Default_RegistersLocatorAndStyles() => session.RunAsync(() =>
    {
        var app = new ShellApplication();

        app.Initialize();

        Assert.Single(app.DataTemplates.OfType<ShellViewLocator>());
        Assert.NotEmpty(app.Styles);
        Assert.Equal("Polhem", app.Name);
    });

    [Fact]
    [DisplayName("Completing the framework initialization before Initialize has run throws")]
    public Task OnFrameworkInitializationCompleted_BeforeInitialize_Throws() => session.RunAsync(() =>
    {
        var app = new ShellApplication();

        Assert.Throws<InvalidOperationException>(app.OnFrameworkInitializationCompleted);
    });
}
