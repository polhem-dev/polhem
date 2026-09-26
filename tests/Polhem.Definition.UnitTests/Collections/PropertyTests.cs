using System.ComponentModel;
using Polhem.Definition.Collections;

namespace Polhem.Definition.UnitTests.Collections
{
    /// <summary>
    /// Unit tests for Property.
    /// </summary>
    public class PropertyTests
    {
        [Fact]
        [DisplayName("Default constructor initializes Name and Value to empty strings")]
        public void DefaultConstructor_InitializesEmpty()
        {
            var property = new Property();

            Assert.Equal(string.Empty, property.Name);
            Assert.Equal(string.Empty, property.Value);
        }

        [Fact]
        [DisplayName("Parameterized constructor sets Name and Value")]
        public void ParameterizedConstructor_SetsProperties()
        {
            var property = new Property("Color", "Red");

            Assert.Equal("Color", property.Name);
            Assert.Equal("Red", property.Value);
        }

        [Fact]
        [DisplayName("Name maps to Key")]
        public void Name_MapsToKey()
        {
            var property = new Property { Name = "Alpha" };

            Assert.Equal("Alpha", property.Key);

            property.Key = "Beta";
            Assert.Equal("Beta", property.Name);
        }

        [Fact]
        [DisplayName("ToString returns \"Name=Value\"")]
        public void ToString_ReturnsFormatted()
        {
            var property = new Property("Color", "Red");

            Assert.Equal("Color=Red", property.ToString());
        }
    }
}
