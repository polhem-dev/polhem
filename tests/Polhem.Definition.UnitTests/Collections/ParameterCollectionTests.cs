using System.ComponentModel;
using Polhem.Definition.Collections;

namespace Polhem.Definition.UnitTests.Collections
{
    /// <summary>
    /// Tests for Parameter and ParameterCollection.
    /// </summary>
    public class ParameterCollectionTests
    {
        [Fact]
        [DisplayName("Parameter constructor sets Name and Value")]
        public void Parameter_Constructor_SetsNameAndValue()
        {
            // Act
            var p = new Parameter("Count", 42);

            // Assert
            Assert.Equal("Count", p.Name);
            Assert.Equal(42, p.Value);
        }

        [Fact]
        [DisplayName("Parameter ToString returns the Name=Value format")]
        public void Parameter_ToString_ReturnsFormattedString()
        {
            // Arrange
            var p = new Parameter("Count", 42);

            // Act
            var text = p.ToString();

            // Assert
            Assert.Contains("Count", text);
            Assert.Contains("42", text);
        }

        [Fact]
        [DisplayName("ParameterCollection Add(name, value) adds an item that can be looked up by name")]
        public void Add_NameValue_AddsItem()
        {
            // Arrange
            var collection = new ParameterCollection
            {
                // Act
                { "X", 100 }
            };

            // Assert
            Assert.Single(collection);
            Assert.Equal(100, collection["X"].Value);
        }

        [Fact]
        [DisplayName("ParameterCollection Add with an existing name overwrites the value")]
        public void Add_DuplicateName_ReplacesValue()
        {
            // Arrange
            var collection = new ParameterCollection
            {
                { "X", 100 },

                // Act
                { "X", 200 }
            };

            // Assert
            Assert.Single(collection);
            Assert.Equal(200, collection["X"].Value);
        }

        [Fact]
        [DisplayName("GetValue<T> returns the converted value when the key exists")]
        public void GetValueT_Existing_ReturnsTypedValue()
        {
            // Arrange
            var collection = new ParameterCollection
            {
                { "Age", 30 }
            };

            // Act
            var value = collection.GetValue<int>("Age");

            // Assert
            Assert.Equal(30, value);
        }

        [Fact]
        [DisplayName("GetValue<T> throws KeyNotFoundException when the key does not exist")]
        public void GetValueT_Missing_ThrowsKeyNotFoundException()
        {
            // Arrange
            var collection = new ParameterCollection();

            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() => collection.GetValue<int>("Missing"));
        }

        [Fact]
        [DisplayName("GetValue<T> with a default value returns the default when the key does not exist")]
        public void GetValueT_WithDefault_ReturnsDefaultWhenMissing()
        {
            // Arrange
            var collection = new ParameterCollection();

            // Act
            var value = collection.GetValue<int>("Missing", 99);

            // Assert
            Assert.Equal(99, value);
        }
    }
}
