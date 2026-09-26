using System.ComponentModel;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Settings
{
    /// <summary>
    /// Tests for the TableItem data class.
    /// </summary>
    public class TableItemTests
    {
        [Fact]
        [DisplayName("TableItem defaults to empty strings")]
        public void TableItem_Default_HasEmptyProperties()
        {
            var item = new TableItem();

            Assert.Equal(string.Empty, item.TableName);
            Assert.Equal(string.Empty, item.DisplayName);
        }

        [Fact]
        [DisplayName("TableItem.TableName maps to Key")]
        public void TableItem_TableName_MapsToKey()
        {
            var item = new TableItem { TableName = "st_user" };

            Assert.Equal("st_user", item.Key);
            Assert.Equal("st_user", item.TableName);
        }

        [Fact]
        [DisplayName("TableItem.ToString returns 'TableName - DisplayName'")]
        public void TableItem_ToString_ReturnsFormatted()
        {
            var item = new TableItem
            {
                TableName = "st_user",
                DisplayName = "使用者"
            };

            Assert.Equal("st_user - 使用者", item.ToString());
        }
    }
}
