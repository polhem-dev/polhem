using System.ComponentModel;
using Polhem.Base.Serialization;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Settings
{

    /// <summary>
    /// Tests for the program registry classes such as ProgramSettings and ProgramItem.
    /// </summary>
    public class ProgramSettingsDataTests
    {
        [Fact]
        [DisplayName("ProgramItem default constructor initializes empty strings")]
        public void ProgramItem_DefaultConstructor_InitializesEmpty()
        {
            var item = new ProgramItem();

            Assert.Equal(string.Empty, item.ProgId);
            Assert.Equal(string.Empty, item.DisplayName);
            Assert.Equal(string.Empty, item.BusinessObject);
        }

        [Fact]
        [DisplayName("ProgramItem.BusinessObject defaults to an empty string")]
        public void ProgramItem_BusinessObject_DefaultsToEmpty()
        {
            var item = new ProgramItem("P001", "客戶維護");

            Assert.Equal(string.Empty, item.BusinessObject);
        }

        [Fact]
        [DisplayName("ProgramItem.BusinessObject is not written to XML when empty")]
        public void ProgramItem_BusinessObject_EmptyOmittedFromXml()
        {
            var settings = new ProgramSettings();
            settings.Items!.Add("P001", "客戶維護");

            var xml = XmlCodec.Serialize(settings);

            Assert.DoesNotContain("BusinessObject=", xml);
        }

        [Fact]
        [DisplayName("ProgramItem.BusinessObject round-trips through XML as an XmlAttribute when set")]
        public void ProgramItem_BusinessObject_RoundTripsThroughXml()
        {
            var settings = new ProgramSettings();
            var item = settings.Items!.Add("P001", "客戶維護");
            item.BusinessObject = "MyErp.Business.CustomerBo, MyErp.Business";

            var xml = XmlCodec.Serialize(settings);
            var restored = XmlCodec.Deserialize<ProgramSettings>(xml);

            Assert.Contains("BusinessObject=\"MyErp.Business.CustomerBo, MyErp.Business\"", xml);
            Assert.NotNull(restored);
            var restoredItem = restored!.Items!["P001"];
            Assert.Equal("MyErp.Business.CustomerBo, MyErp.Business", restoredItem.BusinessObject);
        }

        [Fact]
        [DisplayName("ProgramItem parameterized constructor sets ProgId and DisplayName")]
        public void ProgramItem_ParameterizedConstructor_SetsProperties()
        {
            var item = new ProgramItem("P001", "客戶維護");

            Assert.Equal("P001", item.ProgId);
            Assert.Equal("客戶維護", item.DisplayName);
            Assert.Equal("P001", item.Key);
        }

        [Fact]
        [DisplayName("ProgramItem.ToString returns 'ProgId - DisplayName'")]
        public void ProgramItem_ToString_ReturnsFormatted()
        {
            var item = new ProgramItem("P001", "客戶維護");

            Assert.Equal("P001 - 客戶維護", item.ToString());
        }

        [Fact]
        [DisplayName("ProgramItemCollection Add(progId, displayName) adds and returns the item")]
        public void ProgramItemCollection_Add_AddsAndReturnsItem()
        {
            var settings = new ProgramSettings();
            var collection = settings.Items!;

            var item = collection.Add("P001", "客戶維護");

            Assert.Single(collection);
            Assert.Equal("P001", item.ProgId);
            Assert.Equal("客戶維護", item.DisplayName);
        }

        [Fact]
        [DisplayName("In the flattened registry, registering the same progId twice is rejected by the collection at load time")]
        public void ProgramItemCollection_DuplicateProgId_Throws()
        {
            var settings = new ProgramSettings();
            settings.Items!.Add("P001", "客戶維護");

            Assert.Throws<ArgumentException>(() => settings.Items!.Add("P001", "另一支程式"));
        }

        [Fact]
        [DisplayName("ProgramSettings has a non-null Items by default")]
        public void ProgramSettings_Default_HasItems()
        {
            var settings = new ProgramSettings();

            Assert.NotNull(settings.Items);
            Assert.Equal(string.Empty, settings.ObjectFilePath);
        }

        [Fact]
        [DisplayName("ProgramSettings.Items is not serialized while it is empty, whether or not it was read")]
        public void ProgramSettings_Items_EmptyCollection_IsNotSerialized()
        {
            var settings = new ProgramSettings();

            Assert.False(settings.ItemsSpecified);
            Assert.Empty(settings.Items!);
            Assert.False(settings.ItemsSpecified);
        }

        [Fact]
        [DisplayName("ProgramSettings.SetObjectFilePath updates the file path")]
        public void ProgramSettings_SetObjectFilePath_UpdatesPath()
        {
            var settings = new ProgramSettings();

            settings.SetObjectFilePath("/tmp/programs.xml");

            Assert.Equal("/tmp/programs.xml", settings.ObjectFilePath);
        }
    }
}
