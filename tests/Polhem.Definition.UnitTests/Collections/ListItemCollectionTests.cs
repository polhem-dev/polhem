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
        [DisplayName("ListItem Text falls back to Value when no text is set")]
        public void Text_NotSet_ReturnsValue()
        {
            var item = new ListItem { Value = "B" };

            Assert.Equal("B", item.Text);
        }

        [Fact]
        [DisplayName("ListItem Text set to an empty string falls back to Value")]
        public void Text_Empty_ReturnsValue()
        {
            var item = new ListItem("B", string.Empty);

            Assert.Equal("B", item.Text);
        }

        [Fact]
        [DisplayName("ListItem read from XML without a Text attribute shows its value")]
        public void Text_MissingInXml_ReturnsValue()
        {
            var items = Polhem.Core.Serialization.XmlCodec.Deserialize<ListItemCollection>(
                "<ArrayOfListItem><ListItem Value=\"A\" /></ArrayOfListItem>");

            Assert.Equal("A", items![0].Text);
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
