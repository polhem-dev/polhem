using System.ComponentModel;
using System.Data;
using Polhem.Base.Data;

namespace Polhem.Base.UnitTests
{
    /// <summary>
    /// Tests of <see cref="Polhem.Base.Data.DataRowExtensions.RewriteVersions"/>, which rewrites both versions of a
    /// modified row.
    /// </summary>
    public class DataRowRewriteVersionsTests
    {
        private static DataRow NewModifiedRow(DataTable table, string name, int qty, string newName, int newQty)
        {
            var row = table.NewRow();
            row["name"] = name;
            row["qty"] = qty;
            table.Rows.Add(row);
            row.AcceptChanges();
            row["name"] = newName;
            row["qty"] = newQty;
            return row;
        }

        private static DataTable NewTable()
        {
            var table = new DataTable("t");
            table.Columns.Add("name", typeof(string));
            table.Columns.Add("qty", typeof(int));
            return table;
        }

        [Fact]
        [DisplayName("RewriteVersions rewrites both versions of the chosen column and keeps the edits of the other columns")]
        public void RewriteVersions_OneColumn_KeepsOtherEdits()
        {
            var row = NewModifiedRow(NewTable(), "old", 1, "new", 2);

            row.RewriteVersions((column, version, value) =>
                column.ColumnName == "qty" ? (int)value * 10 : value);

            Assert.Equal(DataRowState.Modified, row.RowState);
            Assert.Equal("old", row["name", DataRowVersion.Original]);
            Assert.Equal("new", row["name", DataRowVersion.Current]);
            Assert.Equal(10, row["qty", DataRowVersion.Original]);
            Assert.Equal(20, row["qty", DataRowVersion.Current]);
        }

        [Fact]
        [DisplayName("RewriteVersions leaves a row Modified even when both versions end up equal")]
        public void RewriteVersions_VersionsBecomeEqual_StaysModified()
        {
            var row = NewModifiedRow(NewTable(), "same", 1, "same", 2);

            row.RewriteVersions((column, _, value) => column.ColumnName == "qty" ? 5 : value);

            Assert.Equal(DataRowState.Modified, row.RowState);
            Assert.Equal(5, row["qty", DataRowVersion.Original]);
            Assert.Equal(5, row["qty", DataRowVersion.Current]);
        }

        [Fact]
        [DisplayName("RewriteVersions does not write to an expression column")]
        public void RewriteVersions_ExpressionColumn_IsSkipped()
        {
            var table = NewTable();
            table.Columns.Add("double_qty", typeof(int), "qty * 2");
            var row = NewModifiedRow(table, "a", 1, "b", 3);

            var exception = Record.Exception(() => row.RewriteVersions((_, _, value) => value));

            Assert.Null(exception);
            Assert.Equal(6, row["double_qty"]);
            Assert.Equal(DataRowState.Modified, row.RowState);
        }
    }
}
