using System.ComponentModel;
using Polhem.Definition.Collections;

namespace Polhem.Definition.UnitTests.Collections
{
    /// <summary>
    /// Tests for Property and PropertyCollection.
    /// </summary>
    public class PropertyCollectionTests
    {
        [Fact]
        [DisplayName("Property constructor sets Name and Value")]
        public void Property_Constructor_SetsNameAndValue()
        {
            // Act
            var prop = new Property("Key", "Value");

            // Assert
            Assert.Equal("Key", prop.Name);
            Assert.Equal("Value", prop.Value);
        }

        [Fact]
        [DisplayName("Property ToString returns the Name=Value format")]
        public void Property_ToString_ReturnsNameEqualsValue()
        {
            // Arrange
            var prop = new Property("Key", "Value");

            // Act & Assert
            Assert.Equal("Key=Value", prop.ToString());
        }

        [Fact]
        [DisplayName("PropertyCollection.Add(name, value) adds an item")]
        public void Add_StringValue_AddsItem()
        {
            // Arrange
            var collection = new PropertyCollection
            {
                // Act
                { "Theme", "Dark" }
            };

            // Assert
            Assert.Single(collection);
            Assert.Equal("Dark", collection["Theme"].Value);
        }

        [Fact]
        [DisplayName("String GetValue returns the property value when it exists, otherwise the default")]
        public void GetValue_String_ReturnsValueOrDefault()
        {
            // Arrange
            var collection = new PropertyCollection
            {
                { "A", "1" }
            };

            // Act & Assert
            Assert.Equal("1", collection.GetValue("A", "default"));
            Assert.Equal("default", collection.GetValue("Missing", "default"));
        }

        [Fact]
        [DisplayName("Bool GetValue converts the value to a boolean when it exists, otherwise returns the default")]
        public void GetValue_Bool_ReturnsConvertedOrDefault()
        {
            // Arrange
            var collection = new PropertyCollection
            {
                { "Enabled", "true" }
            };

            // Act & Assert
            Assert.True(collection.GetValue("Enabled", false));
            Assert.False(collection.GetValue("Missing", false));
        }

        [Fact]
        [DisplayName("Int GetValue converts the value to an integer when it exists, otherwise returns the default")]
        public void GetValue_Int_ReturnsConvertedOrDefault()
        {
            // Arrange
            var collection = new PropertyCollection
            {
                { "Count", "42" }
            };

            // Act & Assert
            Assert.Equal(42, collection.GetValue("Count", 0));
            Assert.Equal(99, collection.GetValue("Missing", 99));
        }
    }
}
