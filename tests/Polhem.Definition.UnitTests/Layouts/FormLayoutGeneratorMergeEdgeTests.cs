using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.UnitTests.Layouts
{
    /// <summary>
    /// Edge cases of <see cref="FormLayoutGenerator"/>: schemas without a master table, detail grids that share no
    /// column with the schema, and fields a relation lookup already displays.
    /// </summary>
    public class FormLayoutGeneratorMergeEdgeTests
    {
        [Fact]
        [DisplayName("Merge adds nothing for a schema without a master table or detail tables")]
        public void Merge_SchemaWithoutTables_AddsNothing()
        {
            var layout = new FormLayout { LayoutId = "Empty" };

            var added = FormLayoutGenerator.Merge(layout, new FormSchema("Empty", "Empty"));

            Assert.Empty(added);
            Assert.Empty(layout.Sections!);
            Assert.Empty(layout.Details!);
        }

        [Fact]
        [DisplayName("Merge inserts a detail column that has no neighbour in the grid ahead of the trailing hidden identity columns")]
        public void Merge_DetailColumnWithoutNeighbour_GoesBeforeHiddenIdentity()
        {
            var schema = BuildSchemaWithLine(line => line.Fields!.Add("qty", "Quantity", FieldDbType.Decimal));
            var layout = new FormLayout { LayoutId = "Order" };
            var grid = new LayoutGrid { TableName = "OrderLine" };
            grid.Columns!.Add(new LayoutColumn { FieldName = "note" });
            grid.Columns!.Add(new LayoutColumn { FieldName = "sys_rowid", Visible = false });
            layout.Details!.Add(grid);

            var added = FormLayoutGenerator.Merge(layout, schema);

            Assert.Equal(["Order.order_no", "OrderLine.qty", "OrderLine.sys_master_rowid"], added);
            Assert.Equal(["note", "qty", "sys_master_rowid", "sys_rowid"], grid.Columns!.Select(c => c.FieldName));
        }

        [Fact]
        [DisplayName("A hidden column that is not an identity column ends the trailing identity block, so the new column goes after it")]
        public void Merge_TrailingHiddenNonIdentityColumn_GoesAtEnd()
        {
            var schema = BuildSchemaWithLine(line => line.Fields!.Add("qty", "Quantity", FieldDbType.Decimal));
            var layout = new FormLayout { LayoutId = "Order" };
            var grid = new LayoutGrid { TableName = "OrderLine" };
            grid.Columns!.Add(new LayoutColumn { FieldName = "note" });
            grid.Columns!.Add(new LayoutColumn { FieldName = "legacy_code", Visible = false });
            layout.Details!.Add(grid);

            FormLayoutGenerator.Merge(layout, schema);

            Assert.Equal(["note", "legacy_code", "qty", "sys_master_rowid"], grid.Columns!.Select(c => c.FieldName));
        }

        [Fact]
        [DisplayName("A grid made only of hidden identity columns takes the new column at the very start")]
        public void Merge_GridOfIdentityColumnsOnly_InsertsAtStart()
        {
            var schema = BuildSchemaWithLine(line => line.Fields!.Add("qty", "Quantity", FieldDbType.Decimal));
            var layout = new FormLayout { LayoutId = "Order" };
            var grid = new LayoutGrid { TableName = "OrderLine" };
            grid.Columns!.Add(new LayoutColumn { FieldName = "sys_rowid", Visible = false });
            layout.Details!.Add(grid);

            FormLayoutGenerator.Merge(layout, schema);

            Assert.Equal(["qty", "sys_master_rowid", "sys_rowid"], grid.Columns!.Select(c => c.FieldName));
        }

        [Fact]
        [DisplayName("Generate skips a hidden master field and a field the relation lookup already displays")]
        public void Generate_HiddenAndLookupCoveredFields_AreSkipped()
        {
            var schema = new FormSchema("Order", "Orders");
            var master = schema.Tables!.Add("Order", "Orders");
            master.Fields!.Add("order_no", "Order no", FieldDbType.String);
            master.Fields!.Add("internal_note", "Internal note", FieldDbType.String).Visible = false;
            var customer = master.Fields!.Add("customer_rowid", "Customer", FieldDbType.Guid);
            customer.RelationProgId = "Customer";
            customer.DisplayFields = "ref_customer_name";
            master.Fields!.Add("ref_customer_name", "Customer name", FieldDbType.String);

            var layout = FormLayoutGenerator.Generate(schema, "Order");

            Assert.Equal(["order_no", "customer_rowid"],
                layout.Sections![0].Fields!.Select(f => f.FieldName));
        }

        [Fact]
        [DisplayName("Generate leaves out a detail table whose fields are all hidden and not identity fields")]
        public void Generate_DetailWithNoEmittedColumns_AddsNoGrid()
        {
            var schema = new FormSchema("Order", "Orders");
            schema.Tables!.Add("Order", "Orders").Fields!.Add("order_no", "Order no", FieldDbType.String);
            schema.Tables!.Add("OrderLine", "Lines").Fields!.Add("secret", "Secret", FieldDbType.String).Visible = false;

            var layout = FormLayoutGenerator.Generate(schema, "Order");

            Assert.Empty(layout.Details!);
        }

        private static FormSchema BuildSchemaWithLine(Action<FormTable> addLineFields)
        {
            var schema = new FormSchema("Order", "Orders");
            schema.Tables!.Add("Order", "Orders").Fields!.Add("order_no", "Order no", FieldDbType.String);
            var line = schema.Tables!.Add("OrderLine", "Lines");
            line.Fields!.Add("sys_master_rowid", "Master row id", FieldDbType.Guid).Visible = false;
            addLineFields(line);
            return schema;
        }
    }
}
