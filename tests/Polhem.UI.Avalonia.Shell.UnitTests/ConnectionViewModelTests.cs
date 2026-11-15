using System.ComponentModel;
using Polhem.Api.Client;
using Polhem.UI.ApiBridge;
using Polhem.UI.Avalonia.Shell.ViewModels;

namespace Polhem.UI.Avalonia.Shell.UnitTests;

/// <summary>
/// The connection screen. A successful connection replaces the client of <see cref="ClientInfo"/> and needs a
/// backend, so it is not exercised here; a failed one stops before the client is replaced.
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

    [Fact]
    [DisplayName("An unreachable endpoint is reported as an error, stays on the screen and is not remembered")]
    public async Task Connect_UnreachableEndpoint_ReportsErrorAndKeepsStoredEndpoint()
    {
        var storage = new MemoryConnectionStorage();
        var originalEndpoints = ClientInfo.EndpointStorage;
        var originalKeys = ClientInfo.ApiKeyStorage;
        var originalTypes = ClientInfo.SupportedConnectTypes;
        try
        {
            ClientInfo.EndpointStorage = storage;
            ClientInfo.ApiKeyStorage = storage;
            ClientInfo.SupportedConnectTypes = SupportedConnectTypes.Both;
            var connected = false;
            var vm = new ConnectionViewModel(new PolhemShellOptions(), () => connected = true)
            {
                // Nothing listens on this port, so the ping is refused at once.
                Endpoint = " http://localhost:19999 ",
                ApiKey = " key ",
            };

            await vm.ConnectCommand.ExecuteAsync(null);

            Assert.True(vm.IsError);
            Assert.False(vm.IsBusy);
            Assert.False(connected);
            Assert.Empty(storage.SavedEndpoints);
            // The key is saved before the attempt, so a wrong key shows up on this screen and not on the first call.
            Assert.Equal("key", storage.LoadApiKey());
        }
        finally
        {
            ClientInfo.EndpointStorage = originalEndpoints;
            ClientInfo.ApiKeyStorage = originalKeys;
            ClientInfo.SupportedConnectTypes = originalTypes;
        }
    }

    [Fact]
    [DisplayName("The endpoint starts as the stored one, and as the application's default when none is stored")]
    public void Constructor_StoredEndpoint_TakesPrecedenceOverDefault()
    {
        var storage = new MemoryConnectionStorage();
        var originalEndpoints = ClientInfo.EndpointStorage;
        var options = new PolhemShellOptions { DefaultEndpoint = "http://default.test/" };
        try
        {
            ClientInfo.EndpointStorage = storage;
            Assert.Equal("http://default.test/", new ConnectionViewModel(options, () => { }).Endpoint);

            storage.SaveEndpoint("http://stored.test/");
            Assert.Equal("http://stored.test/", new ConnectionViewModel(options, () => { }).Endpoint);
        }
        finally
        {
            ClientInfo.EndpointStorage = originalEndpoints;
        }
    }

    private sealed class MemoryConnectionStorage : IEndpointStorage, IApiKeyStorage
    {
        private string _endpoint = string.Empty;
        private string _apiKey = string.Empty;

        public List<string> SavedEndpoints { get; } = [];

        public string LoadEndpoint() => _endpoint;
        public void SetEndpoint(string endpoint) => _endpoint = endpoint;
        public void SaveEndpoint(string endpoint)
        {
            _endpoint = endpoint;
            SavedEndpoints.Add(endpoint);
        }

        public string LoadApiKey() => _apiKey;
        public void SetApiKey(string apiKey) => _apiKey = apiKey;
        public void SaveApiKey(string apiKey) => _apiKey = apiKey;
    }
}
