using System.ComponentModel;
using System.Text.Json;
using Polhem.Base.Serialization;
using Polhem.Definition.Language;

namespace Polhem.Definition.UnitTests.Language
{
    /// <summary>
    /// Core serialization and lookup tests for <see cref="LanguageResource"/>.
    /// </summary>
    public class LanguageResourceTests
    {
        [Fact]
        [DisplayName("LanguageResource XML round-trip keeps Namespace / Lang / Items / Enums")]
        public void XmlRoundTrip_PreservesAllFields()
        {
            var original = CreateSample();

            var xml = XmlCodec.Serialize(original);
            var restored = XmlCodec.Deserialize<LanguageResource>(xml);

            Assert.NotNull(restored);
            Assert.Equal("Customer", restored!.Namespace);
            Assert.Equal("zh-TW", restored.Lang);
            Assert.Equal(2, restored.Items.Count);
            Assert.Equal("客戶名稱", restored.Items["Field.Name.Caption"].Value);
            var singleEnum = Assert.Single(restored.Enums);
            Assert.Equal("Gender", singleEnum.Name);
            Assert.Equal(2, singleEnum.Entries.Count);
            Assert.Equal("男", singleEnum.Entries["M"].Text);
        }

        [Fact]
        [DisplayName("LanguageResource writes Namespace / Lang as XML attributes")]
        public void XmlSerialization_NamespaceAndLang_AsXmlAttributes()
        {
            var resource = CreateSample();

            var xml = XmlCodec.Serialize(resource);

            Assert.Contains("Namespace=\"Customer\"", xml);
            Assert.Contains("Lang=\"zh-TW\"", xml);
        }

        [Fact]
        [DisplayName("LanguageResource JSON serialization includes the key properties and nested structure (for JS consumers)")]
        public void JsonSerialization_ContainsKeyStructure()
        {
            var resource = CreateSample();

            var json = JsonCodec.Serialize(resource);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("Customer", root.GetProperty("namespace").GetString());
            Assert.Equal("zh-TW", root.GetProperty("lang").GetString());

            var items = root.GetProperty("items");
            Assert.Equal(2, items.GetArrayLength());
            Assert.Equal("Field.Name.Caption", items[0].GetProperty("key").GetString());
            Assert.Equal("客戶名稱", items[0].GetProperty("value").GetString());

            var enums = root.GetProperty("enums");
            Assert.Equal(1, enums.GetArrayLength());
            var gender = enums[0];
            Assert.Equal("Gender", gender.GetProperty("name").GetString());
            var entries = gender.GetProperty("entries");
            Assert.Equal(2, entries.GetArrayLength());
            Assert.Equal("M", entries[0].GetProperty("code").GetString());
            Assert.Equal("男", entries[0].GetProperty("text").GetString());
        }

        [Fact]
        [DisplayName("LanguageResource JSON writes property names in camelCase")]
        public void JsonSerialization_PropertyNames_AreCamelCase()
        {
            var resource = CreateSample();

            var json = JsonCodec.Serialize(resource);

            Assert.Contains("\"namespace\"", json);
            Assert.Contains("\"lang\"", json);
            Assert.Contains("\"items\"", json);
            Assert.Contains("\"enums\"", json);
            Assert.DoesNotContain("\"Namespace\"", json);
        }

        [Fact]
        [DisplayName("GetText returns the value on a hit")]
        public void GetText_ExistingKey_ReturnsValue()
        {
            var resource = CreateSample();

            Assert.Equal("客戶名稱", resource.GetText("Field.Name.Caption"));
            Assert.Equal("儲存失敗", resource.GetText("SaveFailed"));
        }

        [Fact]
        [DisplayName("GetText returns null when the key is not found")]
        public void GetText_MissingKey_ReturnsNull()
        {
            var resource = CreateSample();

            Assert.Null(resource.GetText("Nonexistent.Key"));
        }

        [Fact]
        [DisplayName("GetEnum returns the matching LanguageEnum on a hit")]
        public void GetEnum_ExistingName_ReturnsEnum()
        {
            var resource = CreateSample();

            var gender = resource.GetEnum("Gender");

            Assert.NotNull(gender);
            Assert.Equal(2, gender!.Entries.Count);
            Assert.Equal("男", gender.GetText("M"));
            Assert.Equal("女", gender.GetText("F"));
        }

        [Fact]
        [DisplayName("GetEnum returns null when the name is not found")]
        public void GetEnum_MissingName_ReturnsNull()
        {
            var resource = CreateSample();

            Assert.Null(resource.GetEnum("Nonexistent"));
        }

        [Fact]
        [DisplayName("LanguageEnum.GetText returns null when the code is not found")]
        public void LanguageEnum_GetText_MissingCode_ReturnsNull()
        {
            var resource = CreateSample();
            var gender = resource.GetEnum("Gender")!;

            Assert.Null(gender.GetText("X"));
        }

        [Fact]
        [DisplayName("An empty LanguageResource keeps its empty collections through a serialization round-trip")]
        public void XmlRoundTrip_EmptyResource_PreservesEmptyCollections()
        {
            var original = new LanguageResource
            {
                Namespace = "Empty",
                Lang = "en-US"
            };

            var xml = XmlCodec.Serialize(original);
            var restored = XmlCodec.Deserialize<LanguageResource>(xml);

            Assert.NotNull(restored);
            Assert.Equal("Empty", restored!.Namespace);
            Assert.Equal("en-US", restored.Lang);
            Assert.NotNull(restored.Items);
            Assert.Empty(restored.Items);
            Assert.NotNull(restored.Enums);
            Assert.Empty(restored.Enums);
        }

        [Fact]
        [DisplayName("LanguageItemCollection Add throws for a duplicate key")]
        public void LanguageItemCollection_DuplicateKey_Throws()
        {
            var resource = new LanguageResource { Namespace = "Test", Lang = "en-US" };
            resource.Items.Add("OK", "OK");

            Assert.Throws<ArgumentException>(() => resource.Items.Add("OK", "Confirm"));
        }

        [Fact]
        [DisplayName("LanguageItemCollection gets an item through the key indexer")]
        public void LanguageItemCollection_KeyIndexer_RetrievesItem()
        {
            var resource = CreateSample();

            Assert.Equal("儲存失敗", resource.Items["SaveFailed"].Value);
        }

        private static LanguageResource CreateSample()
        {
            var resource = new LanguageResource
            {
                Namespace = "Customer",
                Lang = "zh-TW"
            };
            resource.Items.Add("Field.Name.Caption", "客戶名稱");
            resource.Items.Add("SaveFailed", "儲存失敗");
            var gender = new LanguageEnum { Name = "Gender" };
            gender.Entries.Add("M", "男");
            gender.Entries.Add("F", "女");
            resource.Enums.Add(gender);
            return resource;
        }
    }
}
