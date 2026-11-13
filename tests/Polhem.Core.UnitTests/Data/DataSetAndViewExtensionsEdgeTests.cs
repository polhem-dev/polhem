using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;

namespace Polhem.Core.UnitTests.Data
{
    /// <summary>
    /// Edge cases of <see cref="DataSetExtensions"/> and <see cref="DataViewExtensions"/>: null inputs, a data set
    /// without a usable name, and a view that has no table.
    /// </summary>
    public class DataSetAndViewExtensionsEdgeTests
    {
        [Fact]
        [DisplayName("GetMasterTable returns null for a null data set")]
        public void GetMasterTable_NullDataSet_ReturnsNull()
        {
            Assert.Null(((DataSet)null!).GetMasterTable());
        }

        [Fact]
        [DisplayName("GetMasterTable returns null when no table carries the data set's name")]
        public void GetMasterTable_NoTableNamedLikeDataSet_ReturnsNull()
        {
            var dataSet = new DataSet("Order");
            dataSet.Tables.Add("OrderLine");

            Assert.Null(dataSet.GetMasterTable());
        }

        [Fact]
        [DisplayName("IsEmpty treats a null data set as empty")]
        public void IsEmpty_NullDataSet_ReturnsTrue()
        {
            Assert.True(((DataSet)null!).IsEmpty());
        }

        [Fact]
        [DisplayName("IsEmpty treats a data set without a master table as empty")]
        public void IsEmpty_NoMasterTable_ReturnsTrue()
        {
            var dataSet = new DataSet("Order");
            dataSet.Tables.Add("OrderLine").Rows.Add();

            Assert.True(dataSet.IsEmpty());
        }

        [Fact]
        [DisplayName("IsEmpty is false when the master table has a row")]
        public void IsEmpty_MasterTableWithRow_ReturnsFalse()
        {
            var dataSet = new DataSet("Order");
            var master = dataSet.Tables.Add("Order");
            master.Columns.Add("id", typeof(int));
            master.Rows.Add(1);

            Assert.False(dataSet.IsEmpty());
        }

        [Fact]
        [DisplayName("A view without a table has no fields and counts as empty")]
        public void DataView_WithoutTable_HasNoFieldAndIsEmpty()
        {
            var view = new DataView();

            Assert.False(view.HasField("id"));
            Assert.True(view.IsEmpty());
        }

        [Fact]
        [DisplayName("IsEmpty treats a null view as empty")]
        public void DataViewIsEmpty_NullView_ReturnsTrue()
        {
            Assert.True(((DataView)null!).IsEmpty());
        }

        [Fact]
        [DisplayName("DeleteRows without accepting changes leaves the rows marked deleted")]
        public void DeleteRows_WithoutAcceptChanges_LeavesRowsDeleted()
        {
            var table = new DataTable("T");
            table.Columns.Add("id", typeof(int));
            table.Rows.Add(1);
            table.AcceptChanges();

            table.DefaultView.DeleteRows(acceptChanges: false);

            Assert.Equal(DataRowState.Deleted, table.Rows[0].RowState);
        }
    }
}
