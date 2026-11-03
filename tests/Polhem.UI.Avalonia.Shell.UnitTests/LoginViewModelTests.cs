using System.ComponentModel;
using Polhem.UI.Avalonia.Shell.Preferences;
using Polhem.UI.Avalonia.Shell.ViewModels;

namespace Polhem.UI.Avalonia.Shell.UnitTests;

public class LoginViewModelTests
{
    private static readonly ShellOptions s_options = new()
    {
        DefaultCompanyId = "DEFAULT",
        DefaultUserId = "demo",
        DefaultPassword = "secret",
    };

    [Fact]
    [DisplayName("A blank company id is reported without calling the server")]
    public async Task Login_BlankCompany_DoesNotSignIn()
    {
        var calls = 0;
        var loggedIn = false;
        var vm = new LoginViewModel(s_options, new MemoryPreferences(),
            (_, _, _) => { calls++; return Task.FromResult(new SignInResult("Demo", string.Empty)); },
            () => loggedIn = true)
        {
            CompanyId = "   ",
        };

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.Equal(0, calls);
        Assert.False(loggedIn);
        Assert.True(vm.IsError);
    }

    [Fact]
    [DisplayName("The company id starts as the last one entered on this device")]
    public void CompanyId_Remembered_WinsOverDefault()
    {
        var preferences = new MemoryPreferences();
        preferences.Save(ShellPreferenceKeys.CompanyId, "LAST");

        var vm = new LoginViewModel(s_options, preferences, NeverCalled, () => { });

        Assert.Equal("LAST", vm.CompanyId);
    }

    [Fact]
    [DisplayName("The company id starts as the default when none was entered before")]
    public void CompanyId_NothingRemembered_UsesDefault()
    {
        var vm = new LoginViewModel(s_options, new MemoryPreferences(), NeverCalled, () => { });

        Assert.Equal("DEFAULT", vm.CompanyId);
        Assert.Equal("demo", vm.UserId);
    }

    [Fact]
    [DisplayName("A successful sign-in passes the trimmed company id and remembers it")]
    public async Task Login_Success_RemembersCompany()
    {
        var preferences = new MemoryPreferences();
        string? signedInto = null;
        var loggedIn = false;
        var vm = new LoginViewModel(s_options, preferences,
            (_, _, company) => { signedInto = company; return Task.FromResult(new SignInResult("Demo", string.Empty)); },
            () => loggedIn = true)
        {
            CompanyId = " ACME ",
        };

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.Equal("ACME", signedInto);
        Assert.True(loggedIn);
        Assert.False(vm.IsError);
        Assert.Equal("ACME", preferences.Load(ShellPreferenceKeys.CompanyId));
    }

    [Fact]
    [DisplayName("A failed sign-in reports the error and remembers nothing")]
    public async Task Login_Failure_RemembersNothing()
    {
        var preferences = new MemoryPreferences();
        var loggedIn = false;
        var vm = new LoginViewModel(s_options, preferences,
            (_, _, _) => Task.FromException<SignInResult>(new InvalidOperationException("unknown company")),
            () => loggedIn = true);

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.False(loggedIn);
        Assert.True(vm.IsError);
        Assert.Contains("unknown company", vm.Status, StringComparison.Ordinal);
        Assert.Equal(string.Empty, preferences.Load(ShellPreferenceKeys.CompanyId));
    }

    [Fact]
    [DisplayName("The sign-in links and their heading come from the options")]
    public void SignInLinks_FromOptions()
    {
        var options = new ShellOptions { SignInLinksTitle = "Other front ends" };
        options.SignInLinks.Add(new ShellLink("Blazor", new Uri("https://example.test/")));

        var vm = new LoginViewModel(options, new MemoryPreferences(), NeverCalled, () => { });

        Assert.True(vm.HasSignInLinks);
        Assert.True(vm.HasSignInLinksTitle);
        Assert.Equal("Other front ends", vm.SignInLinksTitle);
        Assert.Equal([new ShellLink("Blazor", new Uri("https://example.test/"))], vm.SignInLinks);
    }

    [Fact]
    [DisplayName("No sign-in links hides the link list")]
    public void SignInLinks_None_Hidden()
    {
        var vm = new LoginViewModel(s_options, new MemoryPreferences(), NeverCalled, () => { });

        Assert.False(vm.HasSignInLinks);
        Assert.False(vm.HasSignInLinksTitle);
    }

    private static Task<SignInResult> NeverCalled(string userId, string password, string companyId)
        => throw new InvalidOperationException("The sign-in should not be called.");

    private sealed class MemoryPreferences : IShellPreferenceStorage
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public string Load(string key) => _values.TryGetValue(key, out var value) ? value : string.Empty;

        public void Save(string key, string value) => _values[key] = value;
    }
}
