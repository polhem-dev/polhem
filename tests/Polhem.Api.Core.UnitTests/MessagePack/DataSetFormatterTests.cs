using System.Buffers;
using System.ComponentModel;
using System.Data;
using MessagePack;
using Polhem.Api.Core.MessagePack;

namespace Polhem.Api.Core.UnitTests.MessagePack
{
    /// <summary>
    /// MessagePack round-trip tests for a DataSet: its name, its tables in the DataTable shape, and its relations.
    /// </summary>
    public class DataSetFormatterTests
    {
        private static readonly string[] s_expectedParentColumns = ["Id"];
        private static readonly string[] s_expectedChildColumns = ["CustomerId"];

        private static DataSet BuildMasterDetailDataSet()
        {
            var ds = new DataSet("Orders");

            var master = new DataTable("Customer");
            master.Columns.Add("Id", typeof(int));
            master.Columns.Add("Name", typeof(string));
            master.Rows.Add(1, "Alice");
            master.Rows.Add(2, "Bob");
            ds.Tables.Add(master);

            var detail = new DataTable("Order");
            detail.Columns.Add("OrderId", typeof(int));
            detail.Columns.Add("CustomerId", typeof(int));
            detail.Columns.Add("Amount", typeof(decimal));
            detail.Rows.Add(101, 1, 10.5m);
            detail.Rows.Add(102, 2, 20.5m);
            ds.Tables.Add(detail);

            ds.Relations.Add(new DataRelation(
                "FK_Customer_Order",
                master.Columns["Id"]!,
                detail.Columns["CustomerId"]!));

            return ds;
        }

        private static DataSet RoundTrip(DataSet ds)
            => MessagePackCodec.Deserialize<DataSet>(MessagePackCodec.Serialize(ds))!;

        [Fact]
        [DisplayName("A round-trip preserves DataSetName, Tables and the Relation's name, tables and columns")]
        public void RoundTrip_PreservesNameTablesRelations()
        {
            var restored = RoundTrip(BuildMasterDetailDataSet());

            Assert.Equal("Orders", restored.DataSetName);
            Assert.Equal(["Customer", "Order"], restored.Tables.Cast<DataTable>().Select(t => t.TableName));
            var rel = Assert.Single(restored.Relations.Cast<DataRelation>());
            Assert.Equal("FK_Customer_Order", rel.RelationName);
            Assert.Equal("Customer", rel.ParentTable.TableName);
            Assert.Equal("Order", rel.ChildTable.TableName);
            Assert.Equal(s_expectedParentColumns, rel.ParentColumns.Select(c => c.ColumnName));
            Assert.Equal(s_expectedChildColumns, rel.ChildColumns.Select(c => c.ColumnName));
        }

        [Fact]
        [DisplayName("A DataSet with a Relation keeps its structure through a MessagePack round-trip")]
        public void DataSet_WithRelation_MessagePackRoundTrip_PreservesStructure()
        {
            var ds = BuildMasterDetailDataSet();

            var bytes = MessagePackCodec.Serialize(ds);
            var restored = MessagePackCodec.Deserialize<DataSet>(bytes);

            Assert.NotNull(restored);
            Assert.Equal(2, restored.Tables.Count);
            Assert.Single(restored.Relations.Cast<DataRelation>());
            Assert.Equal("FK_Customer_Order", restored.Relations[0].RelationName);
            Assert.Equal(2, restored.Tables["Customer"]!.Rows.Count);
            Assert.Equal(2, restored.Tables["Order"]!.Rows.Count);
            Assert.Equal(20.5m, restored.Tables["Order"]!.Rows[1]["Amount"]);
        }

        [Fact]
        [DisplayName("A DataSet without Relations round-trips with an empty Relations collection")]
        public void RoundTrip_NoRelations_ReturnsEmptyRelations()
        {
            var ds = new DataSet("Simple");
            var t = new DataTable("T");
            t.Columns.Add("X", typeof(int));
            ds.Tables.Add(t);

            var restored = RoundTrip(ds);

            Assert.Empty(restored.Relations);
            Assert.Single(restored.Tables.Cast<DataTable>());
        }

        [Fact]
        [DisplayName("A relation over a composite key keeps its column pairs in order")]
        public void RoundTrip_CompositeKeyRelation_PreservesColumnPairs()
        {
            var ds = new DataSet("Composite");
            var master = ds.Tables.Add("Master");
            master.Columns.Add("CompanyId", typeof(string));
            master.Columns.Add("No", typeof(int));
            var detail = ds.Tables.Add("Detail");
            detail.Columns.Add("MasterNo", typeof(int));
            detail.Columns.Add("MasterCompanyId", typeof(string));
            ds.Relations.Add("FK", [master.Columns["CompanyId"]!, master.Columns["No"]!],
                [detail.Columns["MasterCompanyId"]!, detail.Columns["MasterNo"]!]);

            var rel = Assert.Single(RoundTrip(ds).Relations.Cast<DataRelation>());

            Assert.Equal(["CompanyId", "No"], rel.ParentColumns.Select(c => c.ColumnName));
            Assert.Equal(["MasterCompanyId", "MasterNo"], rel.ChildColumns.Select(c => c.ColumnName));
        }

        [Fact]
        [DisplayName("The tables of a DataSet carry their row states and original values")]
        public void RoundTrip_DetailRowStates_ArePreserved()
        {
            var ds = BuildMasterDetailDataSet();
            ds.AcceptChanges();
            var detail = ds.Tables["Order"]!;
            detail.Rows[0]["Amount"] = 11m;
            detail.Rows[1].Delete();
            detail.Rows.Add(103, 2, 5m);

            var restored = RoundTrip(ds).Tables["Order"]!;

            Assert.Equal(
                [DataRowState.Modified, DataRowState.Deleted, DataRowState.Added],
                restored.Rows.Cast<DataRow>().Select(r => r.RowState));
            Assert.Equal(10.5m, restored.Rows[0]["Amount", DataRowVersion.Original]);
            Assert.Equal(11m, restored.Rows[0]["Amount"]);
            Assert.Equal(20.5m, restored.Rows[1]["Amount", DataRowVersion.Original]);
        }

        [Fact]
        [DisplayName("A relation that names a table the DataSet does not have is rejected")]
        public void Deserialize_RelationWithMissingTable_Throws()
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            writer.WriteArrayHeader(3);
            writer.Write("ds");
            writer.WriteArrayHeader(0);
            writer.WriteArrayHeader(1);
            writer.WriteArrayHeader(5);
            writer.Write("FK");
            writer.Write("Ghost");
            writer.Write("Ghost");
            writer.WriteArrayHeader(0);
            writer.WriteArrayHeader(0);
            writer.Flush();
            var bytes = buffer.WrittenSpan.ToArray();

            var ex = Assert.ThrowsAny<MessagePackSerializationException>(() => MessagePackCodec.Deserialize<DataSet>(bytes));

            Assert.Contains("names a table the DataSet does not have", ex.ToString(), StringComparison.Ordinal);
        }
    }
}
