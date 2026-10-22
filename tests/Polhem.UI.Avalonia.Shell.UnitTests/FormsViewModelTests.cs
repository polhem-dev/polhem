using System.ComponentModel;
using Polhem.UI.Avalonia.Shell.Navigation;
using Polhem.UI.Avalonia.Shell.ViewModels;

namespace Polhem.UI.Avalonia.Shell.UnitTests;

public class FormsViewModelTests
{
    private static readonly ShellOptions s_options = new() { AppTitle = "Demo" };

    private static Task<IReadOnlyList<NavItem>> NoMenu() => Task.FromResult<IReadOnlyList<NavItem>>([]);

    [Fact]
    [DisplayName("Sign out ends the session and then returns to sign-in")]
    public async Task SignOut_ServerAnswers_EndsSessionThenNavigates()
    {
        var steps = new List<string>();
        var vm = new FormsViewModel(s_options, NoMenu,
            () => { steps.Add("signOut"); return Task.CompletedTask; },
            () => steps.Add("navigated"));

        await vm.SignOutCommand.ExecuteAsync(null);

        Assert.Equal(["signOut", "navigated"], steps);
    }

    [Fact]
    [DisplayName("Sign out returns to sign-in even when the server does not answer")]
    public async Task SignOut_ServerFails_StillNavigates()
    {
        var navigated = false;
        var vm = new FormsViewModel(s_options, NoMenu,
            () => Task.FromException(new InvalidOperationException("server restarted")),
            () => navigated = true);

        await vm.SignOutCommand.ExecuteAsync(null);

        Assert.True(navigated);
    }

    [Fact]
    [DisplayName("A failed menu load shows as a header row instead of faulting")]
    public async Task MenuLoaded_LoadFails_AddsHeaderRow()
    {
        var vm = new FormsViewModel(s_options,
            () => Task.FromException<IReadOnlyList<NavItem>>(new InvalidOperationException("boom")),
            () => Task.CompletedTask, () => { });

        await vm.MenuLoaded;

        var row = Assert.Single(vm.NavItems);
        Assert.True(row.IsHeader);
        Assert.Contains("boom", row.Title, StringComparison.Ordinal);
    }
}
