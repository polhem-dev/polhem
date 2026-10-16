using System.Data;

namespace Polhem.Api.Core.UnitTests.AuditLog
{
    /// <summary>
    /// The change data set the audit-log wire tests send and check. Compiled into both
    /// <c>Polhem.Api.Core.UnitTests</c> (the codec round trip) and <c>Polhem.Api.Server.UnitTests</c> (the dispatch round
    /// trip) through a linked file, so the two assert the same rows.
    /// </summary>
    internal static class AuditLogChangeData
    {
        /// <summary>One Modified row (Alice → Alice Wang) and one Unchanged row.</summary>
        public static DataSet NewChangeDataSet()
        {
            var dataSet = new DataSet("Employee");
            var table = dataSet.Tables.Add("Employee");
            table.Columns.Add("sys_rowid", typeof(string));
            table.Columns.Add("name", typeof(string));
            var modified = table.Rows.Add("R-1", "Alice");
            table.Rows.Add("R-2", "Bob");
            dataSet.AcceptChanges();
            modified["name"] = "Alice Wang";
            return dataSet;
        }

        public static void AssertChangeDataSet(DataSet dataSet)
        {
            var rows = dataSet.Tables["Employee"]!.Rows;
            Assert.Equal(2, rows.Count);
            Assert.Equal(DataRowState.Modified, rows[0].RowState);
            Assert.Equal("Alice", rows[0]["name", DataRowVersion.Original]);
            Assert.Equal("Alice Wang", rows[0]["name", DataRowVersion.Current]);
            Assert.Equal(DataRowState.Unchanged, rows[1].RowState);
            Assert.Equal("Bob", rows[1]["name"]);
        }
    }
}
