using System.ComponentModel;
using System.Data;
using Bunit;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Server.Controls;
using Polhem.Web.Blazor.Server.Controls.Editors;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.UnitTests.Controls
{
    /// <summary>
    /// Behavior tests for the Blazor <see cref="GridControl"/>, aligned with the Avalonia grid's editing rules:
    /// when the toolbar shows, in-cell editing, the edit form flow, and list mode.
    /// </summary>
    public class GridControlTests : BunitContext
    {
        private IRenderedComponent<GridControl> RenderGrid(FormDataObject dataObject, LayoutGrid layout,
            GridEditMode editMode = GridEditMode.InCell, SingleFormMode formMode = SingleFormMode.Edit)
        {
            var scope = Render<FormScope>(p => p
                .Add(c => c.DataObject, dataObject)
                .Add(c => c.FormMode, formMode)
                .AddChildContent<GridControl>(g => g.Add(c => c.Layout, layout).Add(c => c.EditMode, editMode)));
            return scope.FindComponent<GridControl>();
        }

        [Fact]
        [DisplayName("The grid lists the detail rows with a parenthesised read-only header and a required header class")]
        public void Render_ListsRowsAndHeaders()
        {
            var schema = OrderTestData.BuildSchema();
            var cut = RenderGrid(OrderTestData.BuildOrder(schema), OrderTestData.LinesLayout(schema), formMode: SingleFormMode.View);

            Assert.Equal(2, cut.FindAll("tr.polhem-grid__row").Count);
            var headers = cut.FindAll("th.polhem-grid__header");
            Assert.Contains(headers, h => h.TextContent == "(Amount)");
            Assert.Contains(headers, h => h.TextContent == "Item" && h.ClassList.Contains("polhem-grid__header--required"));
        }

        [Theory]
        [InlineData(SingleFormMode.Edit, GridControlAllowActions.All, true)]
        [InlineData(SingleFormMode.View, GridControlAllowActions.All, false)]
        [InlineData(SingleFormMode.Edit, GridControlAllowActions.None, false)]
        [DisplayName("The toolbar shows only when the form mode allows editing and the layout grants an action")]
        public void Toolbar_FollowsModeAndActions(SingleFormMode mode, GridControlAllowActions actions, bool visible)
        {
            var schema = OrderTestData.BuildSchema();
            var cut = RenderGrid(OrderTestData.BuildOrder(schema), OrderTestData.LinesLayout(schema, actions), formMode: mode);

            Assert.Equal(visible, cut.FindAll(".polhem-grid__toolbar").Count == 1);
        }

        [Fact]
        [DisplayName("In-cell mode shows editors for editable columns only, and an edit writes the row")]
        public void InCell_EditorsWriteTheRow()
        {
            var schema = OrderTestData.BuildSchema();
            var dataObject = OrderTestData.BuildOrder(schema);
            var cut = RenderGrid(dataObject, OrderTestData.LinesLayout(schema));

            var firstRow = cut.FindAll("tr.polhem-grid__row")[0];
            Assert.Equal(2, firstRow.QuerySelectorAll("td.polhem-grid__cell--editor").Length);
            cut.FindAll("tr.polhem-grid__row")[0].QuerySelector("input.polhem-field-editor--text")!.Change("Washer");

            Assert.Equal("Washer", dataObject.GetField(OrderTestData.Line(dataObject, 0), "item"));
        }

        [Fact]
        [DisplayName("Add appends a seeded row and Delete removes the selected one")]
        public void AddAndDelete_ChangeRows()
        {
            var schema = OrderTestData.BuildSchema();
            var dataObject = OrderTestData.BuildOrder(schema);
            // As loaded from the server: a deleted row is then marked for the save instead of dropped.
            dataObject.DataSet.AcceptChanges();
            var cut = RenderGrid(dataObject, OrderTestData.LinesLayout(schema));

            cut.Find(".polhem-grid__button--add").Click();
            Assert.Equal(3, cut.FindAll("tr.polhem-grid__row").Count);

            cut.FindAll("tr.polhem-grid__row")[0].Click();
            cut.Find(".polhem-grid__button--delete").Click();

            Assert.Equal(2, cut.FindAll("tr.polhem-grid__row").Count);
            Assert.Equal(DataRowState.Deleted, OrderTestData.Line(dataObject, 0).RowState);
        }

        [Fact]
        [DisplayName("Edit-form mode edits a double-clicked row in the dialog, and OK commits it")]
        public void EditForm_DoubleClick_CommitsThroughDialog()
        {
            var schema = OrderTestData.BuildSchema();
            var dataObject = OrderTestData.BuildOrder(schema);
            var cut = RenderGrid(dataObject, OrderTestData.LinesLayout(schema), GridEditMode.EditForm);
            Assert.Empty(cut.FindAll("td.polhem-grid__cell--editor"));

            cut.FindAll("tr.polhem-grid__row")[1].DoubleClick();
            cut.Find("#polhem-row-edit-item").Change("Washer");
            cut.Find(".polhem-row-edit-panel__ok").Click();

            Assert.Empty(cut.FindAll(".polhem-row-edit-dialog"));
            Assert.Equal("Washer", dataObject.GetField(OrderTestData.Line(dataObject, 1), "item"));
            Assert.Contains("Washer", cut.FindAll("tr.polhem-grid__row")[1].TextContent, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A cancelled Add in edit-form mode removes the blank row again")]
        public void EditForm_AddCancelled_RemovesRow()
        {
            var schema = OrderTestData.BuildSchema();
            var dataObject = OrderTestData.BuildOrder(schema);
            var cut = RenderGrid(dataObject, OrderTestData.LinesLayout(schema), GridEditMode.EditForm);

            cut.Find(".polhem-grid__button--add").Click();
            Assert.Single(cut.FindAll(".polhem-row-edit-dialog"));
            cut.Find(".polhem-row-edit-panel__cancel").Click();

            Assert.Equal(2, cut.FindAll("tr.polhem-grid__row").Count);
            Assert.Equal(2, dataObject.DataSet.Tables["OrderLine"]!.Rows.Count);
        }

        [Fact]
        [DisplayName("A list-mode grid shows its rows and never offers editing")]
        public void ListMode_IsReadOnly()
        {
            var schema = OrderTestData.BuildSchema();
            var table = OrderTestData.BuildOrder(schema).DataSet.Tables["OrderLine"]!;

            var cut = Render<GridControl>(p => p
                .Add(c => c.Layout, OrderTestData.LinesLayout(schema))
                .Add(c => c.DataTable, table));

            Assert.Equal(2, cut.FindAll("tr.polhem-grid__row").Count);
            Assert.Empty(cut.FindAll(".polhem-grid__toolbar"));
            Assert.Empty(cut.FindAll("td.polhem-grid__cell--editor"));
        }

        [Fact]
        [DisplayName("Selecting a row raises RowSelected with its row id")]
        public void Click_RaisesRowSelected()
        {
            var schema = OrderTestData.BuildSchema();
            var dataObject = OrderTestData.BuildOrder(schema);
            Guid? selected = null;
            var cut = Render<GridControl>(p => p
                .Add(c => c.Layout, OrderTestData.LinesLayout(schema))
                .Add(c => c.DataObject, dataObject)
                .Add(c => c.AllowEdit, false)
                .Add(c => c.RowSelected, id => selected = id));

            cut.FindAll("tr.polhem-grid__row")[1].Click();

            Assert.Equal((Guid)OrderTestData.Line(dataObject, 1)[Polhem.Definition.SysFields.RowId], selected);
            Assert.Single(cut.FindAll("tr.polhem-grid__row--selected"));
        }
    }
}
