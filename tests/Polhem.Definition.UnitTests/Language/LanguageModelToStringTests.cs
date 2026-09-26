using System.ComponentModel;
using Polhem.Definition.Language;

namespace Polhem.Definition.UnitTests.Language
{
    /// <summary>
    /// Adds coverage for the ToString() methods of the language model types and the parameterless constructors of their collections.
    /// Covers LanguageEnum, LanguageEnumEntry, LanguageItem and LanguageResource,
    /// and the parameterless constructor paths of LanguageEnumCollection, LanguageItemCollection and
    /// LanguageEnumEntryCollection.
    /// </summary>
    public class LanguageModelToStringTests
    {
        [Fact]
        [DisplayName("LanguageEnum.ToString returns a string with the name and the number of Entries")]
        public void LanguageEnum_ToString_ContainsNameAndEntryCount()
        {
            var langEnum = new LanguageEnum { Name = "Gender" };
            langEnum.Entries.Add("M", "男");
            langEnum.Entries.Add("F", "女");

            Assert.Equal("Gender (2 entries)", langEnum.ToString());
        }

        [Fact]
        [DisplayName("LanguageEnumEntry.ToString returns the Code = Text format")]
        public void LanguageEnumEntry_ToString_FormatsCodeAndText()
        {
            var entry = new LanguageEnumEntry { Code = "M", Text = "男" };

            Assert.Equal("M = 男", entry.ToString());
        }

        [Fact]
        [DisplayName("LanguageItem.ToString returns the Key = Value format")]
        public void LanguageItem_ToString_FormatsKeyAndValue()
        {
            var item = new LanguageItem { Key = "OK", Value = "確定" };

            Assert.Equal("OK = 確定", item.ToString());
        }

        [Fact]
        [DisplayName("LanguageResource.ToString contains the Namespace, Lang, number of Items and number of Enums")]
        public void LanguageResource_ToString_ContainsAllParts()
        {
            var resource = new LanguageResource { Namespace = "Common", Lang = "zh-TW" };
            resource.Items.Add("OK", "確定");

            Assert.Equal("Common [zh-TW] (1 items, 0 enums)", resource.ToString());
        }

        [Fact]
        [DisplayName("LanguageEnumCollection parameterless constructor creates a non-null empty collection")]
        public void LanguageEnumCollection_ParameterlessConstructor_CreatesEmptyCollection()
        {
            var collection = new LanguageEnumCollection();

            Assert.NotNull(collection);
            Assert.Empty(collection);
        }

        [Fact]
        [DisplayName("LanguageItemCollection parameterless constructor creates a non-null empty collection")]
        public void LanguageItemCollection_ParameterlessConstructor_CreatesEmptyCollection()
        {
            var collection = new LanguageItemCollection();

            Assert.NotNull(collection);
            Assert.Empty(collection);
        }

        [Fact]
        [DisplayName("LanguageEnumEntryCollection parameterless constructor creates a non-null empty collection")]
        public void LanguageEnumEntryCollection_ParameterlessConstructor_CreatesEmptyCollection()
        {
            var collection = new LanguageEnumEntryCollection();

            Assert.NotNull(collection);
            Assert.Empty(collection);
        }
    }
}
