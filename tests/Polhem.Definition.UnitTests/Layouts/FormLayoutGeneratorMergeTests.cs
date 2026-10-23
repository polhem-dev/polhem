using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.UnitTests.Layouts
{
    /// <summary>
    /// Tests for FormLayoutGenerator.Merge completing an existing FormLayout with the fields its FormSchema gained.
    /// </summary>
    public class FormLayoutGeneratorMergeTests
    {
        [Fact]
        [DisplayName("Merge adds nothing to a layout generated from the same schema")]
        public void Merge_UpToDateLayout_AddsNothing()
        {
            var schema = BuildSchema();
            var layout = FormLayoutGenerator.Generate(schema, "Order");

            var added = FormLayoutGenerator.Merge(layout, schema);

            Assert.Empty(added);
            Assert.Equal(["order_no", "order_date", "remark"], MainFields(layout));
            Assert.Equal(["qty", "sys_rowid", "sys_master_rowid"], GridColumns(layout, "OrderLine"));
        }

        [Fact]
        [DisplayName("Merge places a new master field right after the field that precedes it in the schema")]
        public void Merge_NewMasterField_FollowsItsSchemaNeighbour()
        {
            var schema = BuildSchema();
            var layout = FormLayoutGenerator.Generate(schema, "Order");
            schema.MasterTable!.Fields!.Insert(2, new FormField("customer_name", "Customer", FieldDbType.String));

            var added = FormLayoutGenerator.Merge(layout, schema);

            Assert.Equal(["Order.customer_name"], added);
            Assert.Equal(["order_no", "order_date", "customer_name", "remark"], MainFields(layout));
        }

        [Fact]
        [DisplayName("Merge puts a new field in the section its schema neighbour was moved to")]
        public void Merge_NeighbourInAnotherSection_GoesToThatSection()
        {
            var schema = BuildSchema();
            var layout = FormLayoutGenerator.Generate(schema, "Order");
            var main = layout.Sections![0];
            var remark = main.Fields!.Single(f => f.FieldName == "remark");
            main.Fields!.Remove(remark);
            var notes = new LayoutSection { Name = "Notes" };
            notes.Fields!.Add(remark);
            layout.Sections.Add(notes);
            schema.MasterTable!.Fields!.Add("internal_note", "Internal note", FieldDbType.String);

            FormLayoutGenerator.Merge(layout, schema);

            Assert.Equal(["order_no", "order_date"], MainFields(layout));
            Assert.Equal(["remark", "internal_note"], notes.Fields!.Select(f => f.FieldName));
        }

        [Fact]
        [DisplayName("Merge places a new first field before the field that follows it in the schema")]
        public void Merge_NewFirstField_PrecedesItsSchemaNeighbour()
        {
            var schema = BuildSchema();
            var layout = FormLayoutGenerator.Generate(schema, "Order");
            schema.MasterTable!.Fields!.Insert(0, new FormField("branch", "Branch", FieldDbType.String));

            FormLayoutGenerator.Merge(layout, schema);

            Assert.Equal(["branch", "order_no", "order_date", "remark"], MainFields(layout));
        }

        [Fact]
        [DisplayName("Merge keeps what was adjusted by hand on the fields already in the layout")]
        public void Merge_ExistingFields_StayAsAdjusted()
        {
            var schema = BuildSchema();
            var layout = FormLayoutGenerator.Generate(schema, "Order");
            var orderDate = layout.Sections![0].Fields!.Single(f => f.FieldName == "order_date");
            orderDate.Caption = "Placed on";
            orderDate.ColumnSpan = 2;
            schema.MasterTable!.Fields!.Add("internal_note", "Internal note", FieldDbType.String);

            FormLayoutGenerator.Merge(layout, schema);

            Assert.Equal("Placed on", orderDate.Caption);
            Assert.Equal(2, orderDate.ColumnSpan);
        }

        [Fact]
        [DisplayName("Merge keeps a field the schema no longer has")]
        public void Merge_FieldRemovedFromSchema_StaysInLayout()
        {
            var schema = BuildSchema();
            var layout = FormLayoutGenerator.Generate(schema, "Order");
            schema.MasterTable!.Fields!.Remove(schema.MasterTable.Fields["remark"]);

            var added = FormLayoutGenerator.Merge(layout, schema);

            Assert.Empty(added);
            Assert.Contains("remark", MainFields(layout));
        }

        [Fact]
        [DisplayName("Merge places a new detail column after its schema neighbour, ahead of the hidden identity columns")]
        public void Merge_NewDetailField_FollowsItsSchemaNeighbour()
        {
            var schema = BuildSchema();
            var layout = FormLayoutGenerator.Generate(schema, "Order");
            schema.Tables!["OrderLine"].Fields!.Add("unit_price", "Unit price", FieldDbType.Decimal);

            var added = FormLayoutGenerator.Merge(layout, schema);

            Assert.Equal(["OrderLine.unit_price"], added);
            Assert.Equal(["qty", "unit_price", "sys_rowid", "sys_master_rowid"], GridColumns(layout, "OrderLine"));
        }

        [Fact]
        [DisplayName("Merge adds a whole grid for a detail table the layout does not have")]
        public void Merge_NewDetailTable_AddsItsGrid()
        {
            var schema = BuildSchema();
            var layout = FormLayoutGenerator.Generate(schema, "Order");
            var payment = schema.Tables!.Add("OrderPayment", "Payments");
            AddIdentityFields(payment);
            payment.Fields!.Add("amount", "Amount", FieldDbType.Decimal);

            var added = FormLayoutGenerator.Merge(layout, schema);

            Assert.Equal(["OrderPayment.amount", "OrderPayment.sys_rowid", "OrderPayment.sys_master_rowid"], added);
            Assert.Equal(["OrderLine", "OrderPayment"], layout.Details!.Select(g => g.TableName));
            Assert.Equal(["amount", "sys_rowid", "sys_master_rowid"], GridColumns(layout, "OrderPayment"));
        }

        [Fact]
        [DisplayName("Merge creates the Main section when the layout has no section")]
        public void Merge_NoSection_CreatesMain()
        {
            var schema = BuildSchema();
            var layout = new FormLayout { LayoutId = "Order", ProgId = "Order" };

            FormLayoutGenerator.Merge(layout, schema);

            var section = Assert.Single(layout.Sections!);
            Assert.Equal("Main", section.Name);
            Assert.Equal(["order_no", "order_date", "remark"], MainFields(layout));
        }

        private static List<string> MainFields(FormLayout layout)
            => layout.Sections![0].Fields!.Select(f => f.FieldName).ToList();

        private static List<string> GridColumns(FormLayout layout, string tableName)
            => layout.Details!.Single(g => g.TableName == tableName).Columns!.Select(c => c.FieldName).ToList();

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Order", "Orders");
            var master = schema.Tables!.Add("Order", "Orders");
            master.Fields!.Add("order_no", "Order no", FieldDbType.String);
            master.Fields!.Add("order_date", "Order date", FieldDbType.Date);
            master.Fields!.Add("remark", "Remark", FieldDbType.String);
            var line = schema.Tables!.Add("OrderLine", "Lines");
            AddIdentityFields(line);
            line.Fields!.Add("qty", "Quantity", FieldDbType.Decimal);
            return schema;
        }

        private static void AddIdentityFields(FormTable table)
        {
            table.Fields!.Add("sys_rowid", "Row id", FieldDbType.Guid).Visible = false;
            table.Fields!.Add("sys_master_rowid", "Master row id", FieldDbType.Guid).Visible = false;
        }
    }
}
