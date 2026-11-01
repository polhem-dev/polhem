using System.ComponentModel;
using System.Data;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Controls;
using Polhem.Web.Blazor.Controls.Editors;
using Polhem.Web.Blazor.DataObjects;

namespace Polhem.Web.Blazor.BehaviorTests
{
    /// <summary>
    /// What every UI package's grid must do. A UI package derives from this class and supplies how to find and operate
    /// its grid.
    /// </summary>
    public abstract class GridBehaviorTests : BunitContext
    {
        /// <summary>Gets the grid component, which derives from <see cref="GridControlBase"/>.</summary>
        protected abstract Type GridType { get; }

        /// <summary>Counts the rows on screen.</summary>
        /// <param name="cut">The rendered grid.</param>
        /// <returns>The row count.</returns>
        protected abstract int RowCount(IRenderedComponent<ContainerFragment> cut);

        /// <summary>Counts the cell editors of a row.</summary>
        /// <param name="cut">The rendered grid.</param>
        /// <param name="row">The 0-based row.</param>
        /// <returns>The editor count.</returns>
        protected abstract int EditorCount(IRenderedComponent<ContainerFragment> cut, int row);

        /// <summary>Enters text in the first editor of a row.</summary>
        /// <param name="cut">The rendered grid.</param>
        /// <param name="row">The 0-based row.</param>
        /// <param name="text">The text.</param>
        protected abstract void EnterInFirstEditor(IRenderedComponent<ContainerFragment> cut, int row, string text);

        /// <summary>Determines whether the toolbar shows.</summary>
        /// <param name="cut">The rendered grid.</param>
        /// <returns>Whether it shows.</returns>
        protected abstract bool ShowsToolbar(IRenderedComponent<ContainerFragment> cut);

        /// <summary>Clicks the toolbar's Add.</summary>
        /// <param name="cut">The rendered grid.</param>
        protected abstract void ClickAdd(IRenderedComponent<ContainerFragment> cut);

        /// <summary>Clicks the toolbar's Delete.</summary>
        /// <param name="cut">The rendered grid.</param>
        protected abstract void ClickDelete(IRenderedComponent<ContainerFragment> cut);

        /// <summary>Selects a row.</summary>
        /// <param name="cut">The rendered grid.</param>
        /// <param name="row">The 0-based row.</param>
        protected abstract void ClickRow(IRenderedComponent<ContainerFragment> cut, int row);

        /// <summary>Determines whether a row shows as selected.</summary>
        /// <param name="cut">The rendered grid.</param>
        /// <param name="row">The 0-based row.</param>
        /// <returns>Whether it shows as selected.</returns>
        protected abstract bool IsSelected(IRenderedComponent<ContainerFragment> cut, int row);

        /// <summary>Determines whether the edit form is open.</summary>
        /// <param name="cut">The rendered grid.</param>
        /// <returns>Whether it is open.</returns>
        protected abstract bool IsEditFormOpen(IRenderedComponent<ContainerFragment> cut);

        /// <summary>Cancels the edit form.</summary>
        /// <param name="cut">The rendered grid.</param>
        protected abstract void CancelEditForm(IRenderedComponent<ContainerFragment> cut);

        /// <summary>Reads a column's header text.</summary>
        /// <param name="cut">The rendered grid.</param>
        /// <param name="column">The 0-based column.</param>
        /// <returns>The caption shown.</returns>
        protected abstract string HeaderText(IRenderedComponent<ContainerFragment> cut, int column);

        private static FormDataObject BuildOrder()
        {
            var schema = new FormSchema("Order", "Order");
            schema.Tables!.Add("Order", "Order");
            var lines = schema.Tables.Add("Line", "Line");
            lines.Fields!.Add(SysFields.RowId, "Row", FieldDbType.Guid);
            lines.Fields.Add("item", "Item", FieldDbType.String);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            foreach (var item in new[] { "Bolt", "Nut" })
            {
                var row = dataObject.DataSet.Tables["Line"]!.NewRow();
                row[SysFields.RowId] = Guid.NewGuid();
                row["item"] = item;
                dataObject.DataSet.Tables["Line"]!.Rows.Add(row);
            }
            dataObject.DataSet.AcceptChanges();
            return dataObject;
        }

        private static LayoutGrid BuildLayout(GridControlAllowActions actions = GridControlAllowActions.All)
        {
            var layout = new LayoutGrid { TableName = "Line", AllowActions = actions };
            layout.Columns!.Add(new LayoutColumn("item", "Item", ControlType.TextEdit));
            return layout;
        }

        private IRenderedComponent<ContainerFragment> RenderGrid(FormDataObject dataObject, SingleFormMode mode = SingleFormMode.Edit,
            GridEditMode editMode = GridEditMode.InCell, GridControlAllowActions actions = GridControlAllowActions.All)
        {
            var layout = BuildLayout(actions);
            var type = GridType;
            RenderFragment grid = b =>
            {
                b.OpenComponent(0, type);
                b.AddAttribute(1, nameof(GridControlBase.Layout), layout);
                b.AddAttribute(2, nameof(GridControlBase.EditMode), editMode);
                b.CloseComponent();
            };
            return Render(b =>
            {
                b.OpenComponent<FormScope>(0);
                b.AddAttribute(1, nameof(FormScope.DataObject), dataObject);
                b.AddAttribute(2, nameof(FormScope.FormMode), mode);
                b.AddAttribute(3, nameof(FormScope.ChildContent), grid);
                b.CloseComponent();
            });
        }

        [Fact]
        [DisplayName("The grid lists the rows of its table and the layout's captions")]
        public void Render_ListsRowsAndCaptions()
        {
            var cut = RenderGrid(BuildOrder(), SingleFormMode.View);

            Assert.Equal(2, RowCount(cut));
            Assert.Equal("Item", HeaderText(cut, 0));
        }

        [Theory]
        [InlineData(SingleFormMode.Edit, GridControlAllowActions.All, true)]
        [InlineData(SingleFormMode.View, GridControlAllowActions.All, false)]
        [InlineData(SingleFormMode.Edit, GridControlAllowActions.None, false)]
        [DisplayName("The toolbar shows only when the form mode allows editing and the layout grants an action")]
        public void Toolbar_FollowsModeAndActions(SingleFormMode mode, GridControlAllowActions actions, bool visible)
        {
            Assert.Equal(visible, ShowsToolbar(RenderGrid(BuildOrder(), mode, actions: actions)));
        }

        [Fact]
        [DisplayName("In-cell editing writes the row")]
        public void InCell_Edit_WritesTheRow()
        {
            var dataObject = BuildOrder();
            var cut = RenderGrid(dataObject);

            EnterInFirstEditor(cut, 0, "Washer");

            Assert.Equal("Washer", dataObject.GetField(dataObject.DataSet.Tables["Line"]!.Rows[0], "item"));
        }

        [Fact]
        [DisplayName("A read-only form shows no editors in its cells")]
        public void View_ShowsNoEditors()
        {
            Assert.Equal(0, EditorCount(RenderGrid(BuildOrder(), SingleFormMode.View), 0));
        }

        [Fact]
        [DisplayName("Add appends a row, and Delete marks the selected row and hides it")]
        public void AddAndDelete_ChangeTheRows()
        {
            var dataObject = BuildOrder();
            var cut = RenderGrid(dataObject);

            ClickAdd(cut);
            Assert.Equal(3, RowCount(cut));

            ClickRow(cut, 0);
            Assert.True(IsSelected(cut, 0));
            ClickDelete(cut);

            Assert.Equal(2, RowCount(cut));
            Assert.Equal(DataRowState.Deleted, dataObject.DataSet.Tables["Line"]!.Rows[0].RowState);
        }

        [Fact]
        [DisplayName("Delete with nothing selected deletes nothing")]
        public void Delete_NothingSelected_KeepsTheRows()
        {
            var cut = RenderGrid(BuildOrder());

            ClickDelete(cut);

            Assert.Equal(2, RowCount(cut));
        }

        [Fact]
        [DisplayName("Cancelling the edit form of a new row removes the blank row again")]
        public void EditForm_CancelledAdd_RemovesTheRow()
        {
            var cut = RenderGrid(BuildOrder(), editMode: GridEditMode.EditForm);

            ClickAdd(cut);
            Assert.True(IsEditFormOpen(cut));
            Assert.Equal(3, RowCount(cut));

            CancelEditForm(cut);

            Assert.False(IsEditFormOpen(cut));
            Assert.Equal(2, RowCount(cut));
        }

        [Fact]
        [DisplayName("A row other code adds to the table reaches the grid")]
        public void RowAddedByCode_RefreshesTheRows()
        {
            var dataObject = BuildOrder();
            var cut = RenderGrid(dataObject);

            cut.InvokeAsync(() => dataObject.DataSet.Tables["Line"]!.Rows.Add(dataObject.DataSet.Tables["Line"]!.NewRow()));

            cut.WaitForAssertion(() => Assert.Equal(3, RowCount(cut)));
        }
    }
}
