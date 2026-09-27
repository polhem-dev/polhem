using System.ComponentModel;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Settings
{
    /// <summary>
    /// Unit tests for DbCategory.
    /// </summary>
    public class DbCategoryTests
    {
        [Fact]
        [DisplayName("The default constructor initializes empty strings")]
        public void DefaultConstructor_InitializesEmpty()
        {
            var category = new DbCategory();

            Assert.Equal(string.Empty, category.Id);
            Assert.Equal(string.Empty, category.DisplayName);
        }

        [Fact]
        [DisplayName("Id maps to Key")]
        public void Id_MapsToKey()
        {
            var category = new DbCategory { Id = "common" };

            Assert.Equal("common", category.Key);

            category.Key = "system";
            Assert.Equal("system", category.Id);
        }

        [Fact]
        [DisplayName("ToString returns 'Id - DisplayName'")]
        public void ToString_ReturnsFormatted()
        {
            var category = new DbCategory { Id = "common", DisplayName = "共用資料庫" };

            Assert.Equal("common - 共用資料庫", category.ToString());
        }

        [Fact]
        [DisplayName("Tables returns a collection instance when not serializing")]
        public void Tables_DefaultState_ReturnsCollection()
        {
            var category = new DbCategory();

            Assert.NotNull(category.Tables);
        }

        [Fact]
        [DisplayName("Tables is not serialized while it is empty, whether or not it was read")]
        public void Tables_EmptyCollection_IsNotSerialized()
        {
            var category = new DbCategory();

            Assert.False(category.TablesSpecified);
            Assert.Empty(category.Tables!);
            Assert.False(category.TablesSpecified);
        }
    }
}
