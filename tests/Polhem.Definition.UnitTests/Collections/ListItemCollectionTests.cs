using System.ComponentModel;
using System.Data;
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

        [Fact]
        [DisplayName("FromTable fills Value and Text from the specified columns")]
        public void FromTable_PopulatesItemsFromDataTable()
        {
            // Arrange
            var table = new DataTable();
            table.Columns.Add("ValueCol", typeof(string));
            table.Columns.Add("TextCol", typeof(string));
            table.Rows.Add("01", "一");
            table.Rows.Add("02", "二");

            var collection = new ListItemCollection();

            // Act
            collection.FromTable(table, "ValueCol", "TextCol");

            // Assert
            Assert.Equal(2, collection.Count);
            Assert.Equal("一", collection["01"].Text);
            Assert.Equal("二", collection["02"].Text);
        }
    }
}
