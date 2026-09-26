using System.ComponentModel;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.UnitTests.Layouts
{
    /// <summary>
    /// Unit tests for LayoutColumnCollection.
    /// </summary>
    public class LayoutColumnCollectionTests
    {
        [Fact]
        [DisplayName("Add creates and returns a LayoutColumn with the given properties")]
        public void Add_ValidParams_ReturnsColumnWithCorrectProperties()
        {
            var collection = new LayoutColumnCollection();

            var column = collection.Add("Amount", "金額", ControlType.TextEdit);

            Assert.Equal("Amount", column.FieldName);
            Assert.Equal("金額", column.Caption);
            Assert.Equal(ControlType.TextEdit, column.ControlType);
        }

        [Fact]
        [DisplayName("Add puts the column in the collection, where it can be retrieved by index")]
        public void Add_ValidParams_ColumnIsAddedToCollection()
        {
            var collection = new LayoutColumnCollection();

            var column = collection.Add("Name", "姓名", ControlType.TextEdit);

            Assert.Single(collection);
            Assert.Same(column, collection[0]);
        }

        [Fact]
        [DisplayName("Calling Add several times adds every column in order")]
        public void Add_MultipleCalls_AddsAllColumns()
        {
            var collection = new LayoutColumnCollection();

            var col1 = collection.Add("Field1", "欄位1", ControlType.TextEdit);
            var col2 = collection.Add("Field2", "欄位2", ControlType.CheckEdit);

            Assert.Equal(2, collection.Count);
            Assert.Same(col1, collection[0]);
            Assert.Same(col2, collection[1]);
        }
    }
}
