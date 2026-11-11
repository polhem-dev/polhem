using System.ComponentModel;
using System.Data;
using Polhem.UI.Core.DataObjects;

namespace Polhem.UI.Core.UnitTests.DataObjects
{
    /// <summary>
    /// Which data object changes a bound field editor reacts to, the rule both UI heads share.
    /// </summary>
    public class FieldValueChangeFilterTests
    {
        private static readonly string[] s_watched = ["total"];

        private static (DataRow Master, DataRow Line) BuildRows()
        {
            var dataSet = new DataSet();
            var master = dataSet.Tables.Add("Order");
            master.Columns.Add("note");
            var line = dataSet.Tables.Add("Line");
            line.Columns.Add("qty");
            return (master.Rows.Add("x"), line.Rows.Add("1"));
        }

        private static FieldValueChangedEventArgs Change(string table, string field, DataRow row)
            => new(table, field, string.Empty, row);

        [Fact]
        [DisplayName("A master binding reacts to its field on the master table, whatever the casing of the names")]
        public void Concerns_MasterBinding_MatchesTableAndFieldCaseInsensitively()
        {
            var (master, _) = BuildRows();

            Assert.True(FieldValueChangeFilter.Concerns(Change("ORDER", "NOTE", master), null, "Order", "note", null));
            Assert.False(FieldValueChangeFilter.Concerns(Change("Line", "note", master), null, "Order", "note", null));
            Assert.False(FieldValueChangeFilter.Concerns(Change("Order", "other", master), null, "Order", "note", null));
        }

        [Fact]
        [DisplayName("A row binding reacts only to changes on its own row")]
        public void Concerns_RowBinding_MatchesTheExactRow()
        {
            var (_, line) = BuildRows();
            var otherLine = line.Table.Rows.Add("2");

            Assert.True(FieldValueChangeFilter.Concerns(Change("Line", "qty", line), line, "Order", "qty", null));
            Assert.False(FieldValueChangeFilter.Concerns(Change("Line", "qty", otherLine), line, "Order", "qty", null));
        }

        [Fact]
        [DisplayName("A watched field's change concerns the editor as well as its own field")]
        public void Concerns_WatchedField_Matches()
        {
            var (master, _) = BuildRows();

            Assert.True(FieldValueChangeFilter.Concerns(Change("Order", "TOTAL", master), null, "Order", "note", s_watched));
            Assert.False(FieldValueChangeFilter.Concerns(Change("Order", "tax", master), null, "Order", "note", s_watched));
        }

        [Fact]
        [DisplayName("Only a master binding reloads when the dataset is replaced")]
        public void RefreshesOnDataSetReplaced_OnlyForMasterBinding()
        {
            var (_, line) = BuildRows();

            Assert.True(FieldValueChangeFilter.RefreshesOnDataSetReplaced(null));
            Assert.False(FieldValueChangeFilter.RefreshesOnDataSetReplaced(line));
        }
    }
}
