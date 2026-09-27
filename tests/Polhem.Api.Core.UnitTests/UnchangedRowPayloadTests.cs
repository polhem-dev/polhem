using System.ComponentModel;
using System.Data;
using Polhem.Api.Core.MessagePack;
using Polhem.Api.Core.UnitTests.MessagePack;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Verifies that an Unchanged row carries only one set of values, the Current one.
    /// </summary>
    /// <remarks>
    /// This is not a rare case: <c>DataFormRepository.GetData</c> calls <c>AcceptChanges()</c> before returning (as
    /// stated in the response contract), so **every row read from the database** is Unchanged. Unchanged and Modified
    /// used to share one case and send two identical sets of values, while the restoring side read only Current:
    /// double the payload and the serialization CPU, for a copy no reader used.
    /// </remarks>
    public class UnchangedRowPayloadTests
    {
        private static DataTable BuildTable()
        {
            var table = new DataTable("Order");
            table.Columns.Add("id", typeof(int));
            table.Columns.Add("name", typeof(string));
            table.Rows.Add(1, "alpha");
            table.Rows.Add(2, "beta");
            table.AcceptChanges();   // The actual state on the read path.
            return table;
        }

        [Fact]
        [DisplayName("An Unchanged row carries one value per column, its Current values only")]
        public void Unchanged_CarriesCurrentOnly()
        {
            var wire = DataTableWire.Read(MessagePackCodec.Serialize(BuildTable()));

            Assert.All(wire.Rows, row =>
            {
                Assert.Equal((int)DataRowState.Unchanged, row.State);
                Assert.Equal(wire.Columns.Count, row.Cells.Count);
            });
            Assert.Equal("alpha", DataTableWire.ReadString(wire.Rows[0].Cells[1]));
        }

        [Fact]
        [DisplayName("A Modified row still carries both Original and Current")]
        public void Modified_StillCarriesBoth()
        {
            var table = BuildTable();
            table.Rows[0]["name"] = "changed";

            var wire = DataTableWire.Read(MessagePackCodec.Serialize(table));
            var modified = wire.Rows.Single(r => r.State == (int)DataRowState.Modified);

            Assert.Equal(2 * wire.Columns.Count, modified.Cells.Count);
            Assert.Equal("alpha", DataTableWire.ReadString(modified.Cells[1]));
            Assert.Equal("changed", DataTableWire.ReadString(modified.Cells[wire.Columns.Count + 1]));
        }

        [Fact]
        [DisplayName("Unchanged rows without Original still round-trip completely")]
        public void Unchanged_RoundTripsUnaffected()
        {
            var original = BuildTable();

            var bytes = MessagePackCodec.Serialize(original);
            var restored = MessagePackCodec.Deserialize<DataTable>(bytes)!;

            Assert.Equal(2, restored.Rows.Count);
            Assert.All(restored.Rows.Cast<DataRow>(), r => Assert.Equal(DataRowState.Unchanged, r.RowState));
            Assert.Equal(1, restored.Rows[0]["id"]);
            Assert.Equal("alpha", restored.Rows[0]["name"]);
            Assert.Equal("beta", restored.Rows[1]["name"]);

            // The Original version is still available. `AcceptChanges` makes both versions equal, which is exactly why the second copy need not be sent.
            Assert.Equal("alpha", restored.Rows[0]["name", DataRowVersion.Original]);
        }

        [Fact]
        [DisplayName("The payload of Unchanged rows is smaller than when both versions are sent")]
        public void Unchanged_PayloadIsSmaller()
        {
            var unchanged = BuildTable();

            var modified = BuildTable();
            modified.Rows[0]["name"] = "x";
            modified.Rows[1]["name"] = "y";

            var unchangedSize = MessagePackCodec.Serialize(unchanged).Length;
            var modifiedSize = MessagePackCodec.Serialize(modified).Length;

            // Same rows and columns; the only difference is the extra Original that Modified sends.
            Assert.True(unchangedSize < modifiedSize,
                $"Unchanged payload ({unchangedSize} B) should be smaller than Modified ({modifiedSize} B).");
        }
    }
}
