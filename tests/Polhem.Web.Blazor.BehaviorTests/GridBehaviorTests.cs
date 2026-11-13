using System.ComponentModel;
using System.Data;
using System.Reflection;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Sorting;
using Polhem.UI.Core.Fields;
using Polhem.Web.Blazor.Controls;
using Polhem.Web.Blazor.Controls.Editors;
using Polhem.UI.Core.DataObjects;
using Polhem.UI.Core.Editing;

namespace Polhem.Web.Blazor.BehaviorTests
{
    /// <summary>
    /// What every UI package's grid must do. A UI package derives from this class and supplies how to find and operate
    /// its grid.
    /// </summary>
    public abstract class GridBehaviorTests : BunitContext, IAsyncLifetime
    {
        /// <inheritdoc />
        Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

        // xUnit only calls the synchronous Dispose otherwise, and a UI framework's services can be async-only.
        /// <inheritdoc />
        async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync().ConfigureAwait(true);

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

        /// <summary>Determines whether a column's header offers sorting.</summary>
        /// <param name="cut">The rendered grid.</param>
        /// <param name="column">The 0-based column.</param>
        /// <returns>Whether it offers sorting.</returns>
        protected abstract bool OffersSort(IRenderedComponent<ContainerFragment> cut, int column);

        /// <summary>Clicks a column's header to sort.</summary>
        /// <param name="cut">The rendered grid.</param>
        /// <param name="column">The 0-based column.</param>
        protected abstract void ClickSort(IRenderedComponent<ContainerFragment> cut, int column);

        /// <summary>Double-clicks a row.</summary>
        /// <param name="cut">The rendered grid.</param>
        /// <param name="row">The 0-based row.</param>
        protected abstract void DoubleClickRow(IRenderedComponent<ContainerFragment> cut, int row);

        /// <summary>Determines whether a column's header shows the required cue.</summary>
        /// <param name="cut">The rendered grid.</param>
        /// <param name="column">The 0-based column.</param>
        /// <returns>Whether it shows the cue.</returns>
        protected abstract bool ShowsRequiredCue(IRenderedComponent<ContainerFragment> cut, int column);

        /// <summary>Double-clicks the cell of a relation column that offers its record, if any.</summary>
        /// <param name="cut">The rendered grid.</param>
        /// <returns>Whether a cell offered its record.</returns>
        protected abstract bool TryDoubleClickRecordCell(IRenderedComponent<ContainerFragment> cut);

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
            GridEditMode editMode = GridEditMode.InCell, GridControlAllowActions actions = GridControlAllowActions.All,
            LayoutGrid? layout = null, Action<RelationTarget>? openRelation = null)
        {
            layout ??= BuildLayout(actions);
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
                if (openRelation is not null)
                    b.AddAttribute(3, nameof(FormScope.OpenRelation), EventCallback.Factory.Create(this, openRelation));
                b.AddAttribute(4, nameof(FormScope.ChildContent), grid);
                b.CloseComponent();
            });
        }

        [Fact]
        [DisplayName("A value committed in one cell renders that cell again and no other")]
        public async Task CommitValue_RendersOnlyTheChangedCell()
        {
            var schema = new FormSchema("Order", "Order");
            schema.Tables!.Add("Order", "Order");
            var fields = schema.Tables.Add("Line", "Line").Fields!;
            var layout = new LayoutGrid { TableName = "Line", AllowActions = GridControlAllowActions.All };
            foreach (var column in new[] { "a", "b", "c" })
            {
                fields.Add(column, column, FieldDbType.String);
                layout.Columns!.Add(new LayoutColumn(column, column, ControlType.TextEdit));
            }
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            var lines = dataObject.DataSet.Tables["Line"]!;
            foreach (var index in Enumerable.Range(0, 20))
            {
                var line = lines.NewRow();
                line["a"] = line["b"] = line["c"] = $"r{index}";
                lines.Rows.Add(line);
            }
            var cut = RenderGrid(dataObject, layout: layout);
            var cells = cut.FindComponents<GridEditorCell>();
            Assert.Equal(60, cells.Count);
            var before = cells.Select(cell => cell.RenderCount).ToArray();

            await cut.InvokeAsync(() => dataObject.SetField(lines.Rows[5], "b", "changed"));

            var rendered = cells.Where((cell, index) => cell.RenderCount != before[index]).ToArray();
            var only = Assert.Single(rendered);
            Assert.Equal("changed", only.Find(".polhem-grid-cell__text").TextContent);
        }

        [Fact]
        [DisplayName("Disposing the grid releases its data object subscriptions")]
        public async Task Dispose_ReleasesSubscriptions()
        {
            var dataObject = BuildOrder();
            RenderGrid(dataObject, SingleFormMode.View);
            Assert.NotNull(EventDelegate(dataObject, nameof(FormDataObject.RowAdded)));

            await DisposeComponentsAsync();

            Assert.Null(EventDelegate(dataObject, nameof(FormDataObject.RowAdded)));
            Assert.Null(EventDelegate(dataObject, nameof(FormDataObject.RowDeleted)));
            Assert.Null(EventDelegate(dataObject, nameof(FormDataObject.FieldValueChanged)));
            Assert.Null(EventDelegate(dataObject, nameof(FormDataObject.DataSetReplaced)));
        }

        private static Delegate? EventDelegate(FormDataObject dataObject, string eventName)
            => (Delegate?)typeof(FormDataObject)
                .GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(dataObject);

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

        private static FormTable BuildListTable()
        {
            var schema = new FormSchema("Item", "Item");
            var table = schema.Tables!.Add("Item", "Item");
            table.Fields!.Add(SysFields.RowId, "Row", FieldDbType.Guid);
            table.Fields.Add("name", "Name", FieldDbType.String);
            table.Fields.Add("note", "Note", FieldDbType.Text);
            return table;
        }

        private static DataTable BuildListRows(Guid first, Guid second)
        {
            var rows = new DataTable("Item");
            rows.Columns.Add(SysFields.RowId, typeof(Guid));
            rows.Columns.Add("name", typeof(string));
            rows.Columns.Add("note", typeof(string));
            rows.Rows.Add(first, "Bolt", "a");
            rows.Rows.Add(second, "Nut", "b");
            return rows;
        }

        private IRenderedComponent<ContainerFragment> RenderList(Action<string>? sort = null, string? sortField = null,
            SortDirection direction = SortDirection.Asc, Guid? selected = null, Action<Guid>? activated = null, Action<Guid>? rowSelected = null)
        {
            var layout = new LayoutGrid { TableName = "Item" };
            layout.Columns!.Add(new LayoutColumn("name", "Name", ControlType.TextEdit));
            layout.Columns.Add(new LayoutColumn("note", "Note", ControlType.MemoEdit));
            var table = BuildListTable();
            var rows = BuildListRows(Guid.NewGuid(), Guid.NewGuid());
            if (selected is { } id) rows.Rows[1][SysFields.RowId] = id;
            var type = GridType;
            return Render(b =>
            {
                b.OpenComponent(0, type);
                b.AddAttribute(1, nameof(GridControlBase.Layout), layout);
                b.AddAttribute(2, nameof(GridControlBase.DataTable), rows);
                b.AddAttribute(3, nameof(GridControlBase.FormTable), table);
                b.AddAttribute(4, nameof(GridControlBase.SortFieldName), sortField);
                b.AddAttribute(5, nameof(GridControlBase.SortDirection), direction);
                b.AddAttribute(6, nameof(GridControlBase.SelectedRowId), selected);
                if (sort is not null) b.AddAttribute(7, nameof(GridControlBase.OnSortRequested), EventCallback.Factory.Create(this, sort));
                if (activated is not null) b.AddAttribute(8, nameof(GridControlBase.RowActivated), EventCallback.Factory.Create(this, activated));
                if (rowSelected is not null) b.AddAttribute(9, nameof(GridControlBase.RowSelected), EventCallback.Factory.Create(this, rowSelected));
                b.CloseComponent();
            });
        }

        [Fact]
        [DisplayName("A header click asks the host to sort by the field and leaves the rows in the order they came")]
        public void Sort_HeaderClick_ReportsTheField()
        {
            var requested = new List<string>();
            var cut = RenderList(sort: requested.Add);

            ClickSort(cut, 0);

            Assert.Equal("name", Assert.Single(requested));
            Assert.Equal(2, RowCount(cut));
        }

        [Fact]
        [DisplayName("A Text column offers no sort, and without a handler no column does")]
        public void Sort_NotOffered_ForTextOrWithoutHandler()
        {
            var sortable = RenderList(sort: _ => { });
            Assert.True(OffersSort(sortable, 0));
            Assert.False(OffersSort(sortable, 1));

            Assert.False(OffersSort(RenderList(), 0));
        }

        [Theory]
        [InlineData(SortDirection.Asc, "\u25B2")]
        [InlineData(SortDirection.Desc, "\u25BC")]
        [DisplayName("The sorted column's header shows the direction")]
        public void Sort_Mark_FollowsTheDirection(SortDirection direction, string mark)
        {
            var cut = RenderList(sort: _ => { }, sortField: "NAME", direction: direction);

            Assert.EndsWith(mark, HeaderText(cut, 0), StringComparison.Ordinal);
            Assert.DoesNotContain(mark, HeaderText(cut, 1), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A host's selected row id marks that row")]
        public void SelectedRowId_MarksTheRow()
        {
            var cut = RenderList(selected: Guid.NewGuid());

            Assert.False(IsSelected(cut, 0));
            Assert.True(IsSelected(cut, 1));
        }

        [Fact]
        [DisplayName("A click reports the row id, and a double-click activates the row")]
        public void Click_ReportsTheRowId()
        {
            var selected = new List<Guid>();
            var activated = new List<Guid>();
            var id = Guid.NewGuid();
            var cut = RenderList(selected: id, rowSelected: selected.Add, activated: activated.Add);

            ClickRow(cut, 1);
            Assert.Equal(id, Assert.Single(selected));

            DoubleClickRow(cut, 1);
            Assert.Equal(id, Assert.Single(activated));
        }

        [Fact]
        [DisplayName("A read-only column's caption is parenthesised and shows no editor")]
        public void ReadOnlyColumn_IsMarkedAndNotEdited()
        {
            var dataObject = BuildOrder();
            var layout = BuildLayout();
            layout.Columns!.Add(new LayoutColumn("item", "Total", ControlType.TextEdit) { ReadOnly = true });
            var withEditable = EditorCount(RenderGrid(dataObject), 0);
            var cut = RenderGrid(dataObject, layout: layout);

            Assert.Equal("(Total)", HeaderText(cut, 1));
            Assert.Equal(withEditable, EditorCount(cut, 0));
        }

        [Fact]
        [DisplayName("A field the schema requires shows the required cue in a bound grid")]
        public void RequiredColumn_ShowsTheCue()
        {
            var dataObject = BuildOrder();
            Assert.False(ShowsRequiredCue(RenderGrid(dataObject), 0));

            dataObject.GetFormField("Line", "item")!.Required = true;

            Assert.True(ShowsRequiredCue(RenderGrid(dataObject), 0));
        }

        [Fact]
        [DisplayName("On a read-only form a relation cell opens its record, and while editing it offers nothing")]
        public void RelationCell_OpensItsRecordOnlyWhenReadOnly()
        {
            var opened = new List<RelationTarget>();
            var rowId = Guid.NewGuid();
            var schema = new FormSchema("Order", "Order");
            schema.Tables!.Add("Order", "Order");
            var lines = schema.Tables.Add("Line", "Line").Fields!;
            lines.Add("product_rowid", "Product", FieldDbType.Guid).RelationProgId = "Product";
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            var row = dataObject.DataSet.Tables["Line"]!.NewRow();
            row["product_rowid"] = rowId;
            dataObject.DataSet.Tables["Line"]!.Rows.Add(row);
            var layout = new LayoutGrid { TableName = "Line", AllowActions = GridControlAllowActions.All };
            layout.Columns!.Add(new LayoutColumn("product_rowid", "Product", ControlType.ButtonEdit));

            Assert.False(TryDoubleClickRecordCell(RenderGrid(dataObject, layout: layout, openRelation: opened.Add)));
            Assert.Empty(opened);

            Assert.True(TryDoubleClickRecordCell(RenderGrid(dataObject, SingleFormMode.View, layout: layout, openRelation: opened.Add)));
            Assert.Equal(rowId, Assert.Single(opened).RowId);
        }

        /// <summary>Clicks OK on the edit form.</summary>
        /// <param name="cut">The rendered grid.</param>
        protected abstract void ConfirmEditForm(IRenderedComponent<ContainerFragment> cut);

        /// <summary>Enters text in the edit form's input of a field.</summary>
        /// <param name="cut">The rendered grid.</param>
        /// <param name="fieldName">The field.</param>
        /// <param name="text">The text.</param>
        protected abstract void EnterInEditForm(IRenderedComponent<ContainerFragment> cut, string fieldName, string text);

        [Fact]
        [DisplayName("Edit-form mode edits a double-clicked row in the form, and OK commits it")]
        public void EditForm_DoubleClick_Commits()
        {
            var dataObject = BuildOrder();
            var cut = RenderGrid(dataObject, editMode: GridEditMode.EditForm);

            DoubleClickRow(cut, 0);
            cut.WaitForAssertion(() => Assert.True(IsEditFormOpen(cut)));
            EnterInEditForm(cut, "item", "Washer");
            ConfirmEditForm(cut);

            cut.WaitForAssertion(() => Assert.False(IsEditFormOpen(cut)));
            Assert.Equal("Washer", dataObject.GetField(dataObject.DataSet.Tables["Line"]!.Rows[0], "item"));
        }
    }
}
