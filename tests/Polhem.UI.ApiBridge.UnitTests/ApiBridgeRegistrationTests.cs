using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Client;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Contracts.Form;
using Polhem.UI.ApiBridge.Connectors;
using Polhem.UI.ApiBridge.Definitions;
using Polhem.UI.ApiBridge.DependencyInjection;
using Polhem.UI.Core;
using Polhem.UI.Core.Definitions;

namespace Polhem.UI.ApiBridge.UnitTests
{
    /// <summary>
    /// Covers the API-backed implementations of the <c>Polhem.UI.Core</c> services and their registration.
    /// </summary>
    [Collection(ClientInfoStateCollection.Name)]
    public class ApiBridgeRegistrationTests
    {
        [Fact]
        [DisplayName("AddPolhemUIApiBridge registers the API-backed connector factory, definition source and user context")]
        public void AddPolhemUIApiBridge_RegistersApiBackedServices()
        {
            using var provider = new ServiceCollection().AddPolhemUIApiBridge().BuildServiceProvider();

            Assert.IsType<FormApiConnectorFactory>(provider.GetService<IFormApiConnectorFactory>());
            Assert.IsType<ApiFormDefinitionSource>(provider.GetService<IFormDefinitionSource>());
            Assert.IsType<ClientUserContext>(provider.GetService<IUserContext>());
        }

        [Fact]
        [DisplayName("FormApiConnectorFactory creates a form connector for the program from the client it reads")]
        public void Create_ReturnsFormConnectorForProgram()
        {
            var client = PolhemApiClient.CreateRemote("http://localhost:19999", string.Empty);
            var factory = new FormApiConnectorFactory(() => client);

            var connector = factory.Create("Customer");

            Assert.IsType<FormApiConnector>(connector);
            Assert.Equal("Customer", connector.ProgId);
        }

        [Fact]
        [DisplayName("FormApiConnectorFactory reads the client on every call, so a replaced client is picked up")]
        public void Create_ReadsClientOnEveryCall()
        {
            var first = PolhemApiClient.CreateRemote("http://localhost:19998", string.Empty);
            var second = PolhemApiClient.CreateRemote("http://localhost:19999", string.Empty);
            var current = first;
            var factory = new FormApiConnectorFactory(() => current);

            var before = Assert.IsType<FormApiConnector>(factory.Create("Customer"));
            current = second;
            var after = Assert.IsType<FormApiConnector>(factory.Create("Customer"));

            Assert.Same(first, before.Client);
            Assert.Same(second, after.Client);
        }

        [Fact]
        [DisplayName("ClientUserContext reflects the capabilities, company and user ClientInfo currently holds")]
        public void ClientUserContext_ReflectsClientInfo()
        {
            var context = new ClientUserContext();

            Assert.Same(ClientInfo.Capabilities, context.Capabilities);
            Assert.Same(ClientInfo.Company, context.Company);
            Assert.Same(ClientInfo.UserInfo, context.User);
        }
    }
}
