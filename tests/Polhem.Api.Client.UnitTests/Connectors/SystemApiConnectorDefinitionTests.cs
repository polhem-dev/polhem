using System.ComponentModel;
using System.Reflection;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages.System;
using Polhem.Core.Serialization;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.JsonRpc.Payload;
using Polhem.Tests.Shared;

namespace Polhem.Api.Client.UnitTests.Connectors
{
    /// <summary>
    /// The per-type definition methods of <see cref="SystemApiConnector"/>: <c>GetFormSchemaAsync</c>,
    /// <c>GetFormLayoutAsync</c>, <c>GetLanguageAsync</c> and <c>GetCommonConfigurationAsync</c>.
    /// </summary>
    /// <remarks>
    /// The form layout and language calls share their request and response types with the customization calls
    /// (<c>GetCustomizeFormLayout</c>, <c>GetCustomizeLanguage</c>), so a copy-paste slip in the action name would
    /// return a plausible answer from the wrong layer. These tests pin the method sent and the request fields.
    /// </remarks>
    public class SystemApiConnectorDefinitionTests
    {
        private static (SystemApiConnector Connector, FakeApiTransport Transport) Create(object resultValue)
        {
            var connector = new SystemApiConnector(EmptyServiceProvider.Instance, Guid.NewGuid(), new ApiSessionContext());
            var transport = new FakeApiTransport(call => FakeApiTransport.Answer(call, resultValue));
            typeof(ApiConnector)
                .GetProperty(nameof(ApiConnector.Provider), BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(connector, transport);
            return (connector, transport);
        }

        [Fact]
        [DisplayName("GetFormSchemaAsync sends GetFormSchema with the progId and deserializes the returned XML")]
        public async Task GetFormSchemaAsync_SendsProgId_ReturnsSchema()
        {
            var stored = new FormSchema("Employee", "Employee");
            var (connector, transport) = Create(new GetFormSchemaResponse { Xml = XmlCodec.Serialize(stored) });

            var schema = await connector.GetFormSchemaAsync("Employee");

            Assert.Equal($"{SysProgIds.System}.{SystemActions.GetFormSchema}", transport.LastCall!.Method);
            Assert.Equal("Employee", transport.LastCall!.SentValue<GetFormSchemaRequest>().ProgId);
            Assert.Equal("Employee", schema!.ProgId);
        }

        [Fact]
        [DisplayName("GetFormSchemaAsync returns null when the server sends no XML")]
        public async Task GetFormSchemaAsync_EmptyXml_ReturnsNull()
        {
            var (connector, _) = Create(new GetFormSchemaResponse { Xml = string.Empty });

            Assert.Null(await connector.GetFormSchemaAsync("Missing"));
        }

        [Fact]
        [DisplayName("GetFormLayoutAsync sends the base-layer action, not the customization one")]
        public async Task GetFormLayoutAsync_SendsBaseLayerAction()
        {
            var stored = new FormLayout { LayoutId = "EmployeeCard" };
            var (connector, transport) = Create(new GetFormLayoutResponse { Xml = XmlCodec.Serialize(stored) });

            var layout = await connector.GetFormLayoutAsync("Employee", "EmployeeCard");

            Assert.Equal($"{SysProgIds.System}.{SystemActions.GetFormLayout}", transport.LastCall!.Method);
            var sent = transport.LastCall!.SentValue<GetFormLayoutRequest>();
            Assert.Equal("Employee", sent.ProgId);
            Assert.Equal("EmployeeCard", sent.LayoutId);
            Assert.Equal("EmployeeCard", layout!.LayoutId);
        }

        [Fact]
        [DisplayName("GetLanguageAsync sends the base-layer action with the language and namespace")]
        public async Task GetLanguageAsync_SendsBaseLayerAction()
        {
            var stored = new LanguageResource { Lang = "en-US", Namespace = "Employee" };
            var (connector, transport) = Create(new GetLanguageResponse { Xml = XmlCodec.Serialize(stored) });

            var resource = await connector.GetLanguageAsync("en-US", "Employee");

            Assert.Equal($"{SysProgIds.System}.{SystemActions.GetLanguage}", transport.LastCall!.Method);
            var sent = transport.LastCall!.SentValue<GetLanguageRequest>();
            Assert.Equal("en-US", sent.Lang);
            Assert.Equal("Employee", sent.Namespace);
            Assert.Equal("Employee", resource!.Namespace);
        }

        [Fact]
        [DisplayName("GetCommonConfigurationAsync returns the server's answer as a Plain call without applying it")]
        public async Task GetCommonConfigurationAsync_ReturnsResponse()
        {
            var (connector, transport) = Create(new GetCommonConfigurationResponse { CommonConfiguration = "<CommonConfiguration />" });

            var response = await connector.GetCommonConfigurationAsync();

            Assert.Equal($"{SysProgIds.System}.{SystemActions.GetCommonConfiguration}", transport.LastCall!.Method);
            Assert.Equal(PayloadFormat.Plain, transport.LastCall.Params.Format);
            Assert.Equal("<CommonConfiguration />", response.CommonConfiguration);
        }
    }
}
