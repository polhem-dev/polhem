using System.ComponentModel;
using System.Data;
using Polhem.Base.Data;

namespace Polhem.Base.UnitTests
{
    public class DataTableComparerTests
    {
        private static DataTable BuildTable(string name = "T")
        {
            var table = new DataTable(name);
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));
            table.Rows.Add(1, "a");
            table.Rows.Add(2, "b");
            table.AcceptChanges();
            return table;
        }

        [Fact]
        [DisplayName("IsEqual returns true for tables with the same schema and data")]
        public void IsEqual_IdenticalTables_ReturnsTrue()
        {
            Assert.True(DataTableComparer.IsEqual(BuildTable(), BuildTable()));
        }

        [Fact]
        [DisplayName("IsEqual returns false when either table is null")]
        public void IsEqual_NullTable_ReturnsFalse()
        {
            Assert.False(DataTableComparer.IsEqual(null!, BuildTable()));
            Assert.False(DataTableComparer.IsEqual(BuildTable(), null!));
        }

        [Fact]
        [DisplayName("IsEqual returns false when the table names differ")]
        public void IsEqual_DifferentTableName_ReturnsFalse()
        {
            Assert.False(DataTableComparer.IsEqual(BuildTable("A"), BuildTable("B")));
        }

        [Fact]
        [DisplayName("IsEqual returns false when the column count or a column name differs")]
        public void IsEqual_DifferentSchema_ReturnsFalse()
        {
            var a = BuildTable();
            var b = BuildTable();
            b.Columns.Add("Extra", typeof(string));
            Assert.False(DataTableComparer.IsEqual(a, b));

            var c = BuildTable();
            c.Columns["Name"]!.ColumnName = "Title";
            Assert.False(DataTableComparer.IsEqual(BuildTable(), c));
        }

        [Fact]
        [DisplayName("IsEqual returns false when the row counts differ")]
        public void IsEqual_DifferentRowCount_ReturnsFalse()
        {
            var a = BuildTable();
            var b = BuildTable();
            b.Rows.Add(3, "c");
            b.AcceptChanges();
            Assert.False(DataTableComparer.IsEqual(a, b));
        }

        [Fact]
        [DisplayName("IsEqual compares both Current and Original values of Modified rows")]
        public void IsEqual_ModifiedState_ComparesCurrentAndOriginal()
        {
            var a = BuildTable();
            var b = BuildTable();

            a.Rows[0]["Name"] = "changed";
            b.Rows[0]["Name"] = "changed";

            Assert.True(DataTableComparer.IsEqual(a, b));

            var c = BuildTable();
            c.Rows[0]["Name"] = "different";
            Assert.False(DataTableComparer.IsEqual(a, c));
        }

        [Fact]
        [DisplayName("IsEqual compares the Original values of Deleted rows")]
        public void IsEqual_DeletedState_ComparesOriginalValues()
        {
            var a = BuildTable();
            var b = BuildTable();
            a.Rows[0].Delete();
            b.Rows[0].Delete();

            Assert.True(DataTableComparer.IsEqual(a, b));
        }
    }
}
