using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.UnitTests.Layouts
{
    /// <summary>
    /// Edge cases of <see cref="FormLayoutCaptionApplier"/>: a schema without a master table, and layout entries that
    /// name no field or table.
    /// </summary>
    public class FormLayoutCaptionApplierEdgeTests
    {
        [Fact]
        [DisplayName("A schema without a master table leaves the section captions as the layout file gives them")]
        public void Apply_NoMasterTable_KeepsSectionCaptions()
        {
            var layout = new FormLayout { LayoutId = "Order" };
            var section = new LayoutSection { Name = "Main", Caption = "Main (layout file)" };
            section.Fields!.Add(new LayoutField { FieldName = "order_no", Caption = "No (layout file)" });
            layout.Sections!.Add(section);
            var schema = new FormSchema("Order", "Orders");
            schema.Tables!.Add("OrderLine", "Lines");

            FormLayoutCaptionApplier.Apply(layout, schema);

            Assert.Equal("Orders", layout.Caption);
            Assert.Equal("Main (layout file)", section.Caption);
            Assert.Equal("No (layout file)", section.Fields![0].Caption);
        }

        [Fact]
        [DisplayName("A section field without a field name keeps its layout caption")]
        public void Apply_FieldWithoutName_KeepsLayoutCaption()
        {
            var schema = BuildSchema();
            var layout = new FormLayout { LayoutId = "Order" };
            var section = new LayoutSection { Name = "Main" };
            section.Fields!.Add(new LayoutField { FieldName = " ", Caption = "Spacer" });
            section.Fields!.Add(new LayoutField { FieldName = "order_no", Caption = "No (layout file)" });
            layout.Sections!.Add(section);

            FormLayoutCaptionApplier.Apply(layout, schema);

            Assert.Equal("Spacer", section.Fields![0].Caption);
            Assert.Equal("Order no", section.Fields![1].Caption);
        }

        [Fact]
        [DisplayName("A grid without a table name, and a grid column without a field name, keep their layout captions")]
        public void Apply_GridAndColumnWithoutName_KeepLayoutCaptions()
        {
            var schema = BuildSchema();
            var layout = new FormLayout { LayoutId = "Order" };
            var unnamed = new LayoutGrid { TableName = "", Caption = "Unnamed (layout file)" };
            layout.Details!.Add(unnamed);
            var lines = new LayoutGrid { TableName = "OrderLine", Caption = "Lines (layout file)" };
            lines.Columns!.Add(new LayoutColumn { FieldName = "", Caption = "Blank" });
            lines.Columns!.Add(new LayoutColumn { FieldName = "qty", Caption = "Qty (layout file)" });
            lines.Columns!.Add(new LayoutColumn { FieldName = "gone", Caption = "Gone (layout file)" });
            layout.Details!.Add(lines);

            FormLayoutCaptionApplier.Apply(layout, schema);

            Assert.Equal("Unnamed (layout file)", unnamed.Caption);
            Assert.Equal("Lines", lines.Caption);
            Assert.Equal(["Blank", "Quantity", "Gone (layout file)"], lines.Columns!.Select(c => c.Caption));
        }

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Order", "Orders");
            schema.Tables!.Add("Order", "Orders").Fields!.Add("order_no", "Order no", FieldDbType.String);
            schema.Tables!.Add("OrderLine", "Lines").Fields!.Add("qty", "Quantity", FieldDbType.Decimal);
            return schema;
        }
    }
}
