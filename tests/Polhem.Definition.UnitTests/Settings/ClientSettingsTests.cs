using System.ComponentModel;
using Polhem.Base.Serialization;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Settings
{
    /// <summary>
    /// Tests for ClientSettings and the endpoint collection classes.
    /// </summary>
    public class ClientSettingsTests
    {
        [Fact]
        [DisplayName("EndpointItem default constructor initializes empty strings")]
        public void EndpointItem_DefaultConstructor_InitializesEmpty()
        {
            var item = new EndpointItem();

            Assert.Equal(string.Empty, item.Name);
            Assert.Equal(string.Empty, item.Endpoint);
        }

        [Fact]
        [DisplayName("EndpointItem parameterized constructor sets Name and Endpoint")]
        public void EndpointItem_ParameterizedConstructor_SetsProperties()
        {
            var item = new EndpointItem("primary", "https://api.example.com");

            Assert.Equal("primary", item.Name);
            Assert.Equal("https://api.example.com", item.Endpoint);
        }

        [Fact]
        [DisplayName("EndpointItemCollection Add(name, endpoint) adds and returns the item")]
        public void EndpointItemCollection_Add_AddsAndReturnsItem()
        {
            var collection = new EndpointItemCollection();

            var item = collection.Add("primary", "https://api.example.com");

            Assert.Single(collection);
            Assert.Same(item, collection[0]);
            Assert.Equal("primary", item.Name);
            Assert.Equal("https://api.example.com", item.Endpoint);
        }

        [Fact]
        [DisplayName("ClientSettings default constructor sets CreateTime and an empty Endpoint")]
        public void ClientSettings_DefaultConstructor_InitializesState()
        {
            var before = DateTime.UtcNow;
            var settings = new ClientSettings();
            var after = DateTime.UtcNow;

            Assert.InRange(settings.CreateTime, before.AddSeconds(-1), after.AddSeconds(1));
            Assert.Equal(string.Empty, settings.Endpoint);
            Assert.Equal(SerializeState.None, settings.SerializeState);
            Assert.Equal(string.Empty, settings.ObjectFilePath);
        }

        [Fact]
        [DisplayName("ClientSettings.EndpointItems returns the collection when not serializing")]
        public void ClientSettings_EndpointItems_ReturnsCollection()
        {
            var settings = new ClientSettings();

            var items = settings.EndpointItems;

            Assert.NotNull(items);
            items!.Add("primary", "https://api.example.com");
            Assert.Single(settings.EndpointItems!);
        }

        [Fact]
        [DisplayName("ClientSettings.Endpoint can be set and read")]
        public void ClientSettings_Endpoint_CanBeSet()
        {
            var settings = new ClientSettings
            {
                Endpoint = "https://api.example.com"
            };

            Assert.Equal("https://api.example.com", settings.Endpoint);
        }

        [Fact]
        [DisplayName("ClientSettings.SetSerializeState updates the serialize state")]
        public void ClientSettings_SetSerializeState_UpdatesState()
        {
            var settings = new ClientSettings();

            settings.SetSerializeState(SerializeState.Serialize);

            Assert.Equal(SerializeState.Serialize, settings.SerializeState);
        }

        [Fact]
        [DisplayName("ClientSettings.SetObjectFilePath updates the file path")]
        public void ClientSettings_SetObjectFilePath_UpdatesPath()
        {
            var settings = new ClientSettings();

            settings.SetObjectFilePath("/tmp/client.xml");

            Assert.Equal("/tmp/client.xml", settings.ObjectFilePath);
        }

        [Fact]
        [DisplayName("ClientSettings.EndpointItems returns null when serializing an empty collection")]
        public void ClientSettings_EndpointItems_EmptyDuringSerialize_ReturnsNull()
        {
            var settings = new ClientSettings();
            settings.SetSerializeState(SerializeState.Serialize);

            Assert.Null(settings.EndpointItems);
        }
    }
}
