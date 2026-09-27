using System.ComponentModel;
using Polhem.Api.Client;
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
        [DisplayName("CreateFormConnector in Local mode uses LocalApiProvider")]
        public void Local_CreateFormConnector_UsesLocalProvider()
        {
            var factory = new PolhemApiConnectorFactory(new PolhemBlazorOptions().UseLocalProvider(), new ApiSessionContext());

            var connector = factory.CreateFormConnector(Guid.NewGuid(), "Employee");

            Assert.IsType<LocalApiProvider>(connector.Provider);
            Assert.Equal("Employee", connector.ProgId);
        }

        [Fact]
        [DisplayName("CreateFormConnector in Remote mode uses RemoteApiProvider and keeps ProgId")]
        public void Remote_CreateFormConnector_UsesRemoteProvider()
        {
            var options = new PolhemBlazorOptions().UseRemoteProvider("http://api.example.com/api");
            var factory = new PolhemApiConnectorFactory(options, new ApiSessionContext());

            var connector = factory.CreateFormConnector(Guid.NewGuid(), "Employee");

            Assert.IsType<RemoteApiProvider>(connector.Provider);
            Assert.Equal("Employee", connector.ProgId);
        }

        [Fact]
        [DisplayName("CreateSystemConnector in Local mode uses LocalApiProvider")]
        public void Local_CreateSystemConnector_UsesLocalProvider()
        {
            var factory = new PolhemApiConnectorFactory(new PolhemBlazorOptions(), new ApiSessionContext());

            var connector = factory.CreateSystemConnector(Guid.NewGuid());

            Assert.IsType<LocalApiProvider>(connector.Provider);
        }

        [Fact]
        [DisplayName("CreateSystemConnector in Remote mode uses RemoteApiProvider")]
        public void Remote_CreateSystemConnector_UsesRemoteProvider()
        {
            var options = new PolhemBlazorOptions().UseRemoteProvider("http://api.example.com/api");
            var factory = new PolhemApiConnectorFactory(options, new ApiSessionContext());

            var connector = factory.CreateSystemConnector(Guid.NewGuid());

            Assert.IsType<RemoteApiProvider>(connector.Provider);
        }

        [Fact]
        [DisplayName("CreateFormConnector throws ArgumentException for a blank progId")]
        public void CreateFormConnector_BlankProgId_Throws()
        {
            var factory = new PolhemApiConnectorFactory(new PolhemBlazorOptions(), new ApiSessionContext());
            Assert.Throws<ArgumentException>(() => factory.CreateFormConnector(Guid.NewGuid(), "  "));
        }

        [Fact]
        [DisplayName("PolhemApiConnectorFactory throws ArgumentNullException for null options")]
        public void Constructor_NullOptions_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new PolhemApiConnectorFactory(null!, new ApiSessionContext()));
        }

        [Fact]
        [DisplayName("PolhemApiConnectorFactory throws ArgumentNullException for a null session")]
        public void Constructor_NullSession_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new PolhemApiConnectorFactory(new PolhemBlazorOptions(), null!));
        }
    }
}
