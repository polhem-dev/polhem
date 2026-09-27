using System.ComponentModel;
using Polhem.Definition.Collections;

namespace Polhem.Definition.UnitTests.Collections
{
    /// <summary>
    /// Tests for ListItem and ListItemCollection.
    /// </summary>
    public class ListItemCollectionTests
    {
        [Fact]
        [DisplayName("ListItem constructor sets Value and Text")]
        public void ListItem_Constructor_SetsValueAndText()
        {
            // Act
            var item = new ListItem("01", "項目一");

            // Assert
            Assert.Equal("01", item.Value);
            Assert.Equal("項目一", item.Text);
        }

        [Fact]
        [DisplayName("ListItem ToString returns Text")]
        public void ListItem_ToString_ReturnsText()
        {
            // Arrange
            var item = new ListItem("01", "項目一");

            // Act & Assert
            Assert.Equal("項目一", item.ToString());
        }

        [Fact]
        [DisplayName("ListItemCollection Add(value, text) adds the item and returns it")]
        public void Add_ValueAndText_AddsAndReturnsItem()
        {
            // Arrange
            var collection = new ListItemCollection();

            // Act
            var added = collection.Add("A", "Alpha");

            // Assert
            Assert.Single(collection);
            Assert.Same(added, collection["A"]);
            Assert.Equal("Alpha", added.Text);
        }
    }
}
