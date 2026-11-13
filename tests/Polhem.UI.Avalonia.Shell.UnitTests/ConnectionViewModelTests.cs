using System.ComponentModel;
using Polhem.UI.Avalonia.Shell.ViewModels;

namespace Polhem.UI.Avalonia.Shell.UnitTests;

/// <summary>
/// The parts of <see cref="ConnectionViewModel"/> that do not reach the process-wide connection: connecting itself
/// replaces the client of <see cref="Polhem.UI.ApiBridge.ClientInfo"/>, so it is not exercised here.
/// </summary>
public class ConnectionViewModelTests
{
    [Fact]
    [DisplayName("The application's description replaces the default hint")]
    public void Description_OptionsDescription_IsShown()
    {
        var vm = new ConnectionViewModel(new PolhemShellOptions { Description = "Northwind demo" }, () => { });

        Assert.Equal("Northwind demo", vm.Description);
    }

    [Fact]
    [DisplayName("Without an application description the hint text is shown")]
    public void Description_NoOptionsDescription_ShowsHint()
    {
        var vm = new ConnectionViewModel(new PolhemShellOptions(), () => { });

        Assert.False(string.IsNullOrWhiteSpace(vm.Description));
    }

    [Fact]
    [DisplayName("A new connection screen is idle, enabled and not in error")]
    public void Constructor_NewScreen_IsIdle()
    {
        var vm = new ConnectionViewModel(new PolhemShellOptions { AppTitle = "Demo" }, () => { });

        Assert.Equal("Demo", vm.AppTitle);
        Assert.False(vm.IsBusy);
        Assert.True(vm.IsNotBusy);
        Assert.False(vm.IsError);
        Assert.False(string.IsNullOrEmpty(vm.Status));
    }

    [Fact]
    [DisplayName("Marking the screen busy disables its inputs")]
    public void IsNotBusy_Busy_ReturnsFalse()
    {
        var vm = new ConnectionViewModel(new PolhemShellOptions(), () => { }) { IsBusy = true };

        Assert.False(vm.IsNotBusy);
    }

    [Fact]
    [DisplayName("A blank endpoint is reported as an error without connecting")]
    public async Task Connect_BlankEndpoint_ReportsErrorAndStaysPut()
    {
        var connected = false;
        var vm = new ConnectionViewModel(new PolhemShellOptions(), () => connected = true) { Endpoint = "   " };

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.True(vm.IsError);
        Assert.False(vm.IsBusy);
        Assert.False(connected);
    }

    [Fact]
    [DisplayName("Connecting while a connection attempt is in flight does nothing")]
    public async Task Connect_AlreadyBusy_DoesNothing()
    {
        var connected = false;
        var vm = new ConnectionViewModel(new PolhemShellOptions(), () => connected = true)
        {
            Endpoint = "   ",
            IsBusy = true,
        };

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.False(vm.IsError);
        Assert.True(vm.IsBusy);
        Assert.False(connected);
    }
}
