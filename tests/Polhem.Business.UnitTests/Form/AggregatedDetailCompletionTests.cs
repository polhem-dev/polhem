using System.ComponentModel;
using System.Data;
using Polhem.Business.Form;
using Polhem.Definition;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// Tests for <see cref="AggregatedDetailCompletion"/>: which stored detail rows are added to a save payload.
    /// </summary>
    public class AggregatedDetailCompletionTests
    {
        private static DataTable NewLines(string name = "Lines")
        {
            var table = new DataTable(name);
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            table.Columns.Add("amount", typeof(decimal));
            return table;
        }

        private static DataSet Stored(params (Guid RowId, decimal Amount)[] rows)
        {
            var dataSet = new DataSet();
            var lines = NewLines();
            dataSet.Tables.Add(lines);
            foreach (var (rowId, amount) in rows) { lines.Rows.Add(rowId, amount); }
            dataSet.AcceptChanges();
            return dataSet;
        }

        [Fact]
        [DisplayName("Stored rows the payload does not carry are added unchanged; carried and deleted rows are not")]
        public void ImportMissingRows_AddsOnlyUncarriedRows()
        {
            Guid edited = Guid.NewGuid(), deleted = Guid.NewGuid(), missing = Guid.NewGuid();
            using var stored = Stored((edited, 1m), (deleted, 2m), (missing, 3m));
            using var payload = Stored((edited, 1m), (deleted, 2m));
            var lines = payload.Tables["Lines"]!;
            lines.Rows[0]["amount"] = 10m;
            lines.Rows[1].Delete();

            AggregatedDetailCompletion.ImportMissingRows(payload, stored, ["Lines"]);

            Assert.Equal(3, lines.Rows.Count);
            var added = lines.Rows[2];
            Assert.Equal(missing, added[SysFields.RowId]);
            Assert.Equal(DataRowState.Unchanged, added.RowState);
            Assert.Equal(10m, lines.Rows[0]["amount"]);
        }

        [Fact]
        [DisplayName("A detail table the payload leaves out is added with every stored row")]
        public void ImportMissingRows_TableMissing_AddsTable()
        {
            using var stored = Stored((Guid.NewGuid(), 1m), (Guid.NewGuid(), 2m));
            using var payload = new DataSet();

            AggregatedDetailCompletion.ImportMissingRows(payload, stored, ["Lines"]);

            Assert.Equal(2, payload.Tables["Lines"]!.Rows.Count);
        }

        [Fact]
        [DisplayName("Rows of a payload table without a row id column are not completed, so none is counted twice")]
        public void ImportMissingRows_PayloadWithoutRowId_Skipped()
        {
            using var stored = Stored((Guid.NewGuid(), 1m));
            using var payload = new DataSet();
            var lines = new DataTable("Lines");
            lines.Columns.Add("amount", typeof(decimal));
            lines.Rows.Add(1m);
            payload.Tables.Add(lines);

            AggregatedDetailCompletion.ImportMissingRows(payload, stored, ["Lines"]);

            Assert.Single(lines.Rows);
        }
    }
}
