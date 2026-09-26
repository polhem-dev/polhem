using System.ComponentModel;
using Polhem.Api.Core.MessagePack;
using Polhem.Api.Core.Messages.System;

namespace Polhem.Api.Core.UnitTests.System
{
    /// <summary>
    /// Wire-level round trips of the plugin maintenance API. Both directions carry the bindings as an XML string
    /// rather than a <c>PluginSettings</c> object: those are get-only nested collections, and in object form they
    /// silently fail to arrive at a .NET client.
    /// </summary>
    public class CustomizePluginSettingsMessagePackTests
    {
        private const string SampleXml =
            """<PluginSettings><Items><ProgramPluginItem ProgId="Order"><Plugins><PluginItem Type="A.B, A" /></Plugins></ProgramPluginItem></Items></PluginSettings>""";

        [Fact]
        [DisplayName("GetCustomizePluginSettingsRequest round-trip keeps CustomizeId")]
        public void GetRequest_RoundTrip_PreservesCustomizeId()
        {
            var request = new GetCustomizePluginSettingsRequest { CustomizeId = "acme" };

            var restored = MessagePackCodec.Deserialize<GetCustomizePluginSettingsRequest>(
                MessagePackCodec.Serialize(request));

            Assert.NotNull(restored);
            Assert.Equal("acme", restored!.CustomizeId);
        }

        [Fact]
        [DisplayName("GetCustomizePluginSettingsResponse round-trip keeps the XML text")]
        public void GetResponse_RoundTrip_PreservesXml()
        {
            var response = new GetCustomizePluginSettingsResponse { Xml = SampleXml };

            var restored = MessagePackCodec.Deserialize<GetCustomizePluginSettingsResponse>(
                MessagePackCodec.Serialize(response));

            Assert.NotNull(restored);
            Assert.Equal(SampleXml, restored!.Xml);
        }

        [Fact]
        [DisplayName("SaveCustomizePluginSettingsRequest round-trip keeps CustomizeId and Xml")]
        public void SaveRequest_RoundTrip_PreservesBothFields()
        {
            var request = new SaveCustomizePluginSettingsRequest { CustomizeId = "acme", Xml = SampleXml };

            var restored = MessagePackCodec.Deserialize<SaveCustomizePluginSettingsRequest>(
                MessagePackCodec.Serialize(request));

            Assert.NotNull(restored);
            Assert.Equal("acme", restored!.CustomizeId);
            Assert.Equal(SampleXml, restored.Xml);
        }

        [Fact]
        [DisplayName("SaveCustomizePluginSettingsResponse round-trip keeps PluginCount")]
        public void SaveResponse_RoundTrip_PreservesCount()
        {
            var response = new SaveCustomizePluginSettingsResponse { PluginCount = 3 };

            var restored = MessagePackCodec.Deserialize<SaveCustomizePluginSettingsResponse>(
                MessagePackCodec.Serialize(response));

            Assert.NotNull(restored);
            Assert.Equal(3, restored!.PluginCount);
        }

        [Fact]
        [DisplayName("Default values round-trip without a NullReferenceException, and empty strings come back empty")]
        public void DefaultValues_RoundTrip()
        {
            var restored = MessagePackCodec.Deserialize<SaveCustomizePluginSettingsRequest>(
                MessagePackCodec.Serialize(new SaveCustomizePluginSettingsRequest()));

            Assert.NotNull(restored);
            Assert.Equal(string.Empty, restored!.CustomizeId);
            Assert.Equal(string.Empty, restored.Xml);
        }
    }
}
