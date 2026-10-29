using System.ComponentModel;
using Avalonia.Controls;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;
using Polhem.UI.Core.Fields;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// A relation column of the Avalonia <see cref="GridControl"/> in a read-only form: its cell offers the record it
    /// points at, with the hint, only in View mode, inside a form that handles the request and with a value; otherwise
    /// the cell is the plain text it was before.
    /// </summary>
    /// <remarks>
    /// The cells are built through the column's <c>CellTemplate</c>, the way the DataGrid builds them. No Avalonia
    /// platform runs, so the double-tap itself is not raised here; <c>FormViewRelatedRecordTests</c> covers the view's
    /// answer to the request.
    /// </remarks>
    public class GridControlRelatedRecordTests
    {
        private static readonly Guid s_boltRowId = new("eeeeeeee-0000-0000-0000-000000000001");

        private static FormSchema BuildOrderSchema()
        {
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var master = schema.Tables!.Add("Order", "Order");
            master.Fields!.Add(new FormField(SysFields.RowId, "Row", FieldDbType.Guid));
            var detail = schema.Tables!.Add("OrderLine", "Lines");
            detail.Fields!.Add(new FormField(SysFields.RowId, "Row", FieldDbType.Guid));
            detail.Fields!.Add(new FormField(SysFields.MasterRowId, "Order", FieldDbType.Guid));
            var product = new FormField("product_rowid", "Product", FieldDbType.Guid) { RelationProgId = "Product" };
            product.RelationFieldMappings!.Add(SysFields.Name, "ref_product_name");
            detail.Fields!.Add(product);
            detail.Fields!.Add(new FormField("ref_product_name", "Product name", FieldDbType.String, FieldType.RelationField));
            return schema;
        }

        // Binds a detail grid under a parent that stands in for the view: it carries the form mode and, when asked,
        // the flag that says the request is handled. Returns the product cell of the only line.
        private static Control BuildProductCell(SingleFormMode formMode, bool handled, Guid productRowId,
            GridEditMode editMode = GridEditMode.EditForm)
        {
            var schema = BuildOrderSchema();
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            var lines = dataObject.DataSet.Tables["OrderLine"]!;
            var line = lines.NewRow();
            line["product_rowid"] = productRowId;
            line["ref_product_name"] = "Bolt";
            lines.Rows.Add(line);

            var grid = new GridControl { EditMode = editMode };
            var parent = new Border { Child = grid };
            FormScope.SetFormMode(parent, formMode);
            if (handled) RelationOpening.SetIsEnabled(parent, true);
            grid.Bind(dataObject, FormLayoutGenerator.Generate(schema, "default").Details![0]);

            var column = grid.InnerGrid.Columns.OfType<DataGridTemplateColumn>().First(c => Equals(c.Header, "Product"));
            return column.CellTemplate!.Build(lines.DefaultView[0])!;
        }

        [Fact]
        [DisplayName("In View mode under a form that handles it, a relation cell with a value is a host with the open hint")]
        public void ViewMode_RelationCell_OffersRecordWithHint()
        {
            var cell = BuildProductCell(SingleFormMode.View, handled: true, s_boltRowId);

            var host = Assert.IsType<Border>(cell);
            Assert.NotNull(host.Background);
            Assert.Equal("Bolt", Assert.IsType<TextBlock>(host.Child).Text);
            Assert.Equal(UIText.Get(PolhemUIText.OpenRelatedRecord), ToolTip.GetTip(host));
        }

        [Fact]
        [DisplayName("The grid resolves the related record from the row's relation field")]
        public void OffersRelatedRecord_ViewMode_ReturnsTarget()
        {
            var schema = BuildOrderSchema();
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            var lines = dataObject.DataSet.Tables["OrderLine"]!;
            var line = lines.NewRow();
            line["product_rowid"] = s_boltRowId;
            lines.Rows.Add(line);
            var grid = new GridControl();
            var parent = new Border { Child = grid };
            FormScope.SetFormMode(parent, SingleFormMode.View);
            RelationOpening.SetIsEnabled(parent, true);
            grid.Bind(dataObject, FormLayoutGenerator.Generate(schema, "default").Details![0]);

            Assert.True(grid.OffersRelatedRecord(lines.DefaultView[0],
                dataObject.GetFormField("OrderLine", "product_rowid")!, out var target));
            Assert.Equal(new RelationTarget("Product", s_boltRowId), target);
        }

        [Theory]
        [InlineData(SingleFormMode.Edit, true, true)]
        [InlineData(SingleFormMode.View, false, true)]
        [InlineData(SingleFormMode.View, true, false)]
        [DisplayName("While edited in an edit form, outside a form that handles it, or without a value, a relation cell is plain text")]
        public void NotOffering_RelationCell_IsPlainText(SingleFormMode formMode, bool handled, bool hasValue)
        {
            var cell = BuildProductCell(formMode, handled, hasValue ? s_boltRowId : Guid.Empty);

            Assert.Equal("Bolt", Assert.IsType<TextBlock>(cell).Text);
        }
    }
}
