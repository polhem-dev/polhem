using System.ComponentModel;
using Bunit;
using Microsoft.AspNetCore.Components;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.UI.Core.Fields;
using Polhem.Web.Blazor.Server.Controls;
using Polhem.Web.Blazor.Server.Controls.Editors;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.UnitTests.Controls
{
    /// <summary>
    /// A relation column of the Blazor <see cref="GridControl"/> on a read-only form: its cell offers the record it
    /// points at and passes it to the scope's <see cref="FormScope.OpenRelation"/> on a double-click, and offers
    /// nothing while the form is edited, without a handler or without a value.
    /// </summary>
    public class GridControlRelatedRecordTests : BunitContext
    {
        private const string OpenCell = "td.polhem-grid__cell--opens-record";
        private static readonly Guid s_boltRowId = new("eeeeeeee-0000-0000-0000-000000000001");

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Order", "Order");
            var master = schema.Tables!.Add("Order", "Order").Fields!;
            master.Add(SysFields.RowId, "Row", FieldDbType.Guid);
            var lines = schema.Tables.Add("OrderLine", "Lines").Fields!;
            lines.Add(new FormField(SysFields.RowId, "Row", FieldDbType.Guid) { Visible = false });
            lines.Add(new FormField(SysFields.MasterRowId, "Order", FieldDbType.Guid) { Visible = false });
            var product = new FormField("product_rowid", "Product", FieldDbType.Guid) { RelationProgId = "Product" };
            product.RelationFieldMappings!.Add(SysFields.Name, "ref_product_name");
            lines.Add(product);
            lines.Add(new FormField("ref_product_name", "Product name", FieldDbType.String, FieldType.RelationField) { Visible = false });
            return schema;
        }

        private static FormDataObject BuildOrder(FormSchema schema, Guid productRowId)
        {
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            var lines = dataObject.DataSet.Tables["OrderLine"]!;
            var row = lines.NewRow();
            row["product_rowid"] = productRowId;
            row["ref_product_name"] = productRowId == Guid.Empty ? string.Empty : "Bolt";
            lines.Rows.Add(row);
            return dataObject;
        }

        private IRenderedComponent<FormScope> RenderGrid(SingleFormMode formMode, Guid productRowId,
            List<RelationTarget>? opened, GridEditMode editMode = GridEditMode.InCell)
        {
            var schema = BuildSchema();
            var layout = FormLayoutGenerator.Generate(schema, "default").Details!.Single();
            return Render<FormScope>(p =>
            {
                p.Add(c => c.DataObject, BuildOrder(schema, productRowId))
                    .Add(c => c.FormMode, formMode)
                    .AddChildContent<GridControl>(g => g.Add(c => c.Layout, layout).Add(c => c.EditMode, editMode));
                if (opened is not null)
                    p.Add(c => c.OpenRelation, EventCallback.Factory.Create<RelationTarget>(this, opened.Add));
            });
        }

        [Fact]
        [DisplayName("On a read-only form a relation cell with a value shows the hint and passes its record on a double-click")]
        public void ViewMode_DoubleClickRelationCell_PassesTarget()
        {
            var opened = new List<RelationTarget>();
            var cut = RenderGrid(SingleFormMode.View, s_boltRowId, opened);

            var cell = cut.Find(OpenCell);
            Assert.Equal(PolhemUIText.DefaultTexts[PolhemUIText.OpenRelatedRecord], cell.GetAttribute("title"));
            Assert.Equal("Bolt", cell.TextContent.Trim());
            cell.DoubleClick();

            Assert.Equal([new RelationTarget("Product", s_boltRowId)], opened);
            Assert.Empty(cut.FindAll("div.polhem-row-edit-dialog"));
        }

        [Theory]
        [InlineData(GridEditMode.InCell)]
        [InlineData(GridEditMode.EditForm)]
        [DisplayName("While the form is edited a relation cell offers nothing")]
        public void EditMode_RelationCell_OffersNothing(GridEditMode editMode)
        {
            var cut = RenderGrid(SingleFormMode.Edit, s_boltRowId, [], editMode);

            Assert.Empty(cut.FindAll(OpenCell));
        }

        [Fact]
        [DisplayName("Without a handler in the scope a relation cell offers nothing")]
        public void NoHandler_RelationCell_OffersNothing()
        {
            var cut = RenderGrid(SingleFormMode.View, s_boltRowId, opened: null);

            Assert.Empty(cut.FindAll(OpenCell));
        }

        [Fact]
        [DisplayName("An empty relation cell offers nothing")]
        public void EmptyValue_RelationCell_OffersNothing()
        {
            var cut = RenderGrid(SingleFormMode.View, Guid.Empty, []);

            Assert.Empty(cut.FindAll(OpenCell));
        }
    }
}
