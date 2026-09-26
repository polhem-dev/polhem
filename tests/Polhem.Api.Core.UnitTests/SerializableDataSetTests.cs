using System.ComponentModel;
using System.Data;
using Polhem.Api.Core.MessagePack;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// FromDataSet/ToDataSet and DataRelation round-trip tests for SerializableDataSet.
    /// </summary>
    public class SerializableDataSetTests
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

        [Fact]
        [DisplayName("FromDataSet preserves DataSetName, Tables and Relations")]
        public void FromDataSet_PreservesNameTablesRelations()
        {
            var ds = BuildMasterDetailDataSet();

            var sds = SerializableDataSet.FromDataSet(ds);

            Assert.Equal("Orders", sds.DataSetName);
            Assert.Equal(2, sds.Tables.Count);
            Assert.Single(sds.Relations);

            var rel = sds.Relations[0];
            Assert.Equal("FK_Customer_Order", rel.RelationName);
            Assert.Equal("Customer", rel.ParentTable);
            Assert.Equal("Order", rel.ChildTable);
            Assert.Equal(s_expectedParentColumns, rel.ParentColumns);
            Assert.Equal(s_expectedChildColumns, rel.ChildColumns);
        }

        [Fact]
        [DisplayName("ToDataSet restores the DataSet and rebuilds Relations")]
        public void ToDataSet_RestoresDataSetAndRelations()
        {
            var ds = BuildMasterDetailDataSet();
            var sds = SerializableDataSet.FromDataSet(ds);

            var restored = SerializableDataSet.ToDataSet(sds);

            Assert.Equal("Orders", restored.DataSetName);
            Assert.Equal(2, restored.Tables.Count);
            Assert.Single(restored.Relations.Cast<DataRelation>());

            var rel = restored.Relations[0];
            Assert.Equal("FK_Customer_Order", rel.RelationName);
            Assert.Equal("Customer", rel.ParentTable.TableName);
            Assert.Equal("Order", rel.ChildTable.TableName);
            Assert.Equal("Id", rel.ParentColumns[0].ColumnName);
            Assert.Equal("CustomerId", rel.ChildColumns[0].ColumnName);
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
        }

        [Fact]
        [DisplayName("Converting a DataSet without Relations gives an empty Relations collection")]
        public void FromDataSet_NoRelations_ReturnsEmptyRelations()
        {
            var ds = new DataSet("Simple");
            var t = new DataTable("T");
            t.Columns.Add("X", typeof(int));
            ds.Tables.Add(t);

            var sds = SerializableDataSet.FromDataSet(ds);

            Assert.Empty(sds.Relations);
            Assert.Single(sds.Tables);
        }
    }
}
