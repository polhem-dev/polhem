using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Polhem.UI.Avalonia.Shell.Browser;

namespace Polhem.UI.Avalonia.Shell.UnitTests;

/// <summary>
/// The in-memory half of <see cref="BrowserLocalStorage"/>. Every test here stays on members that answer from the
/// cache and never reach <c>window.localStorage</c>, so they run outside a browser.
/// </summary>
[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility",
    Justification = "The tests only use members that answer from the in-memory cache and never call into JavaScript.")]
public class BrowserLocalStorageTests
{
    [Theory]
    [DisplayName("A blank application name is rejected")]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankAppName_Throws(string appName)
    {
        Assert.ThrowsAny<ArgumentException>(() => new BrowserLocalStorage(appName));
    }

    [Fact]
    [DisplayName("An endpoint set in memory is what LoadEndpoint returns")]
    public void LoadEndpoint_AfterSetEndpoint_ReturnsCachedValue()
    {
        var storage = new BrowserLocalStorage("Northwind");

        storage.SetEndpoint("https://example.test/api");

        Assert.Equal("https://example.test/api", storage.LoadEndpoint());
    }

    [Fact]
    [DisplayName("An API key set in memory is what LoadApiKey returns")]
    public void LoadApiKey_AfterSetApiKey_ReturnsCachedValue()
    {
        var storage = new BrowserLocalStorage("Northwind");

        storage.SetApiKey("demo-key");

        Assert.Equal("demo-key", storage.LoadApiKey());
    }
}
