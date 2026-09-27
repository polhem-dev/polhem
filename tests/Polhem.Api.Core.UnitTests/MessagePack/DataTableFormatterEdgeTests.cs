using System.ComponentModel;
using System.Data;
using Polhem.Api.Core.MessagePack;

namespace Polhem.Api.Core.UnitTests.MessagePack
{
    /// <summary>
    /// Edge case and error path tests for the MessagePack DataTable formatter, covering column defaults, primary keys
    /// that are empty or name missing columns, and the row state and original / current values of rows after a
    /// round-trip.
    /// </summary>
    public class DataTableFormatterEdgeTests
    {
        private static DataTable RoundTrip(DataTable table)
            => MessagePackCodec.Deserialize<DataTable>(MessagePackCodec.Serialize(table))!;

        [Fact]
        [DisplayName("A column's DBNull default travels as nil and comes back as DBNull")]
        public void RoundTrip_ColumnDBNullDefault_StaysDBNull()
        {
            var dt = new DataTable("T");
            var col = new DataColumn("Name", typeof(string));
            Assert.Equal(DBNull.Value, col.DefaultValue);
            dt.Columns.Add(col);

            var restored = RoundTrip(dt);

            Assert.Single(restored.Columns);
            Assert.Equal(DBNull.Value, restored.Columns["Name"]!.DefaultValue);
        }

        [Fact]
        [DisplayName("A concrete column default is preserved")]
        public void RoundTrip_ColumnConcreteDefault_IsPreserved()
        {
            var dt = new DataTable("T");
            var col = new DataColumn("Age", typeof(int)) { DefaultValue = 42 };
            dt.Columns.Add(col);

            var restored = RoundTrip(dt);

            Assert.Equal(42, restored.Columns["Age"]!.DefaultValue);
        }

        [Fact]
        [DisplayName("No PrimaryKey is set when the primary key list is empty")]
        public void Deserialize_EmptyPrimaryKeys_NoPrimaryKeyApplied()
        {
            var bytes = DataTableWire.WriteInt32Table("T", ["Id"], []);

            var dt = MessagePackCodec.Deserialize<DataTable>(bytes)!;

            Assert.Empty(dt.PrimaryKey);
        }

        [Fact]
        [DisplayName("A primary key that names a missing column is filtered out, leaving none")]
        public void Deserialize_PrimaryKeyWithMissingColumn_IsFilteredOut()
        {
            var bytes = DataTableWire.WriteInt32Table("T", ["Id"], ["Ghost"]);

            var dt = MessagePackCodec.Deserialize<DataTable>(bytes)!;

            Assert.Empty(dt.PrimaryKey);
        }

        [Fact]
        [DisplayName("Only the existing columns are applied when the primary key mixes existing and missing columns")]
        public void Deserialize_PrimaryKeyMixedValidAndMissing_AppliesOnlyValid()
        {
            var bytes = DataTableWire.WriteInt32Table("T", ["Id"], ["Id", "Ghost"]);

            var dt = MessagePackCodec.Deserialize<DataTable>(bytes)!;

            Assert.Single(dt.PrimaryKey);
            Assert.Equal("Id", dt.PrimaryKey[0].ColumnName);
        }

        [Fact]
        [DisplayName("A round-trip preserves the Original and Current values of a Modified row")]
        public void RoundTrip_ModifiedRow_PreservesOriginalAndCurrent()
        {
            var dt = new DataTable("T");
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("Name", typeof(string));
            dt.Rows.Add(1, "原始");
            dt.AcceptChanges();
            dt.Rows[0]["Name"] = "修改後";

            var restored = RoundTrip(dt);
            var row = restored.Rows[0];
            Assert.Equal(DataRowState.Modified, row.RowState);
            Assert.Equal("原始", row["Name", DataRowVersion.Original]);
            Assert.Equal("修改後", row["Name", DataRowVersion.Current]);
        }

        [Fact]
        [DisplayName("A round-trip restores a Deleted row as Deleted and preserves its Original values")]
        public void RoundTrip_DeletedRow_PreservesOriginal()
        {
            var dt = new DataTable("T");
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("Name", typeof(string));
            dt.Rows.Add(1, "待刪除");
            dt.AcceptChanges();
            dt.Rows[0].Delete();

            var restored = RoundTrip(dt);
            var row = restored.Rows[0];
            Assert.Equal(DataRowState.Deleted, row.RowState);
            Assert.Equal(1, row["Id", DataRowVersion.Original]);
            Assert.Equal("待刪除", row["Name", DataRowVersion.Original]);
        }

        [Fact]
        [DisplayName("A round-trip keeps an Unchanged row Unchanged")]
        public void RoundTrip_UnchangedRow_PreservesState()
        {
            var dt = new DataTable("T");
            dt.Columns.Add("Id", typeof(int));
            dt.Rows.Add(1);
            dt.AcceptChanges();

            var restored = RoundTrip(dt);
            Assert.Equal(DataRowState.Unchanged, restored.Rows[0].RowState);
        }
    }
}
