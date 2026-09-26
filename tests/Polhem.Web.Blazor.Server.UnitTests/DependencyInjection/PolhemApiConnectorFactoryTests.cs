using System.ComponentModel;
using Polhem.Api.Client.Providers;
using Polhem.Web.Blazor.Server.DependencyInjection;

namespace Polhem.Web.Blazor.Server.UnitTests.DependencyInjection
{
    /// <summary>
    /// Verifies <see cref="PolhemApiConnectorFactory"/> emits the right connector
    /// flavour (Local vs Remote) per the supplied <see cref="PolhemBlazorOptions"/>.
    /// </summary>
    public class PolhemApiConnectorFactoryTests
    {
        [Fact]
        [DisplayName("Local 模式 CreateFormConnector 使用 LocalApiProvider")]
        public void Local_CreateFormConnector_UsesLocalProvider()
        {
            var factory = new PolhemApiConnectorFactory(new PolhemBlazorOptions().UseLocalProvider());

            var connector = factory.CreateFormConnector(Guid.NewGuid(), "Employee");

            Assert.IsType<LocalApiProvider>(connector.Provider);
            Assert.Equal("Employee", connector.ProgId);
        }

        [Fact]
        [DisplayName("Remote 模式 CreateFormConnector 使用 RemoteApiProvider 並保留 ProgId")]
        public void Remote_CreateFormConnector_UsesRemoteProvider()
        {
            var options = new PolhemBlazorOptions().UseRemoteProvider("http://api.example.com/api");
            var factory = new PolhemApiConnectorFactory(options);

            var connector = factory.CreateFormConnector(Guid.NewGuid(), "Employee");

            Assert.IsType<RemoteApiProvider>(connector.Provider);
            Assert.Equal("Employee", connector.ProgId);
        }

        [Fact]
        [DisplayName("Local 模式 CreateSystemConnector 使用 LocalApiProvider")]
        public void Local_CreateSystemConnector_UsesLocalProvider()
        {
            var factory = new PolhemApiConnectorFactory(new PolhemBlazorOptions());

            var connector = factory.CreateSystemConnector(Guid.NewGuid());

            Assert.IsType<LocalApiProvider>(connector.Provider);
        }

        [Fact]
        [DisplayName("Remote 模式 CreateSystemConnector 使用 RemoteApiProvider")]
        public void Remote_CreateSystemConnector_UsesRemoteProvider()
        {
            var options = new PolhemBlazorOptions().UseRemoteProvider("http://api.example.com/api");
            var factory = new PolhemApiConnectorFactory(options);

            var connector = factory.CreateSystemConnector(Guid.NewGuid());

            Assert.IsType<RemoteApiProvider>(connector.Provider);
        }

        [Fact]
        [DisplayName("CreateFormConnector 對空白 progId 拋 ArgumentException")]
        public void CreateFormConnector_BlankProgId_Throws()
        {
            var factory = new PolhemApiConnectorFactory(new PolhemBlazorOptions());
            Assert.Throws<ArgumentException>(() => factory.CreateFormConnector(Guid.NewGuid(), "  "));
        }

        [Fact]
        [DisplayName("PolhemApiConnectorFactory 對 null options 拋 ArgumentNullException")]
        public void Constructor_NullOptions_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new PolhemApiConnectorFactory(null!));
        }
    }
}
