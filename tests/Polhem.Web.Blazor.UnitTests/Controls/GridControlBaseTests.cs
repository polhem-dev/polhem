using System.ComponentModel;
using System.Data;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Sorting;
using Polhem.UI.Core.DataObjects;
using Polhem.UI.Core.Editing;
using Polhem.UI.Core.Lists;
using Polhem.Web.Blazor.Controls;
using Polhem.Web.Blazor.Controls.Editors;

namespace Polhem.Web.Blazor.UnitTests.Controls
{
    /// <summary>
    /// The row editing, selection and column state of <see cref="GridControlBase"/>, exercised through a grid that renders
    /// plain markup, so they hold whichever UI framework draws the grid.
    /// </summary>
    public class GridControlBaseTests : BunitContext
    {
        private const string TableName = "Line";
        private const string Row = "div.row";
        private const string Header = "div.header";
        private const string Toolbar = "span.toolbar";
        private static readonly Guid s_firstId = new("aaaaaaaa-0000-0000-0000-000000000001");

        private static LayoutGrid BuildLayout(GridControlAllowActions actions = GridControlAllowActions.All)
        {
            var layout = new LayoutGrid(TableName, "Lines") { AllowActions = actions };
            layout.Columns!.Add("item", "Item", ControlType.TextEdit);
            layout.Columns.Add("qty", "Qty", ControlType.NumericEdit);
            layout.Columns.Add("note", "Note", ControlType.MemoEdit);
            return layout;
        }

        private static FormDataObject BuildOrder()
        {
            var schema = new FormSchema("Order", "Order");
            schema.Tables!.Add("Order", "Order").Fields!.Add(SysFields.RowId, "Row", FieldDbType.Guid);
            var lines = schema.Tables.Add(TableName, "Line").Fields!;
            lines.Add(SysFields.RowId, "Row", FieldDbType.Guid);
            lines.Add("item", "Item", FieldDbType.String).Required = true;
            lines.Add("qty", "Qty", FieldDbType.Decimal);
            lines.Add("note", "Note", FieldDbType.Text);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        private static DataTable BuildListTable()
        {
            var table = new DataTable(TableName);
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            table.Columns.Add(new DataColumn("item", typeof(string)) { AllowDBNull = false });
            table.Columns.Add(new DataColumn("qty", typeof(decimal)) { AllowDBNull = false });
            table.Columns.Add(new DataColumn("note", typeof(string)) { AllowDBNull = false, DefaultValue = "none" });
            table.Rows.Add(s_firstId, "Bolt", 2m, "first");
            return table;
        }

        private IRenderedComponent<ProbeGrid> RenderGrid(Action<ComponentParameterCollectionBuilder<ProbeGrid>> configure)
            => Render(configure);

        [Fact]
        [DisplayName("AddRow on a list-mode table fills each non-null column without a default with an empty value")]
        public async Task AddRow_ListMode_FillsNonNullColumns()
        {
            var table = BuildListTable();
            var cut = RenderGrid(p => p.Add(c => c.DataTable, table).Add(c => c.Layout, BuildLayout()));

            var row = await cut.InvokeAsync(() => cut.Instance.AddRow());

            Assert.NotNull(row);
            Assert.Equal(string.Empty, row["item"]);
            Assert.Equal(0m, row["qty"]);
            Assert.Equal("none", row["note"]);
            Assert.Same(row, cut.Instance.SelectedRow);
        }

        [Fact]
        [DisplayName("AddRow without a table adds nothing")]
        public void AddRow_NoTable_ReturnsNull()
        {
            var cut = RenderGrid(p => p.Add(c => c.Layout, BuildLayout()));

            Assert.Null(cut.Instance.AddRow());
        }

        [Fact]
        [DisplayName("A grid bound to a data object without its table shows no rows")]
        public void Table_UnknownTableName_ShowsNoRows()
        {
            var cut = RenderGrid(p => p.Add(c => c.DataObject, BuildOrder()).Add(c => c.TableName, "Missing"));

            Assert.Empty(cut.FindAll(Row));
            Assert.Null(cut.Instance.AddRow());
        }

        [Fact]
        [DisplayName("EndEdit leaves the rows as they are")]
        public void EndEdit_ChangesNothing()
        {
            var table = BuildListTable();
            var cut = RenderGrid(p => p.Add(c => c.DataTable, table));

            cut.Instance.EndEdit();

            Assert.Single(cut.FindAll(Row));
            Assert.Equal(DataRowState.Added, table.Rows[0].RowState);
        }

        [Fact]
        [DisplayName("A deleted selected row is no longer the selected row")]
        public void SelectedRow_Deleted_IsNull()
        {
            var table = BuildListTable();
            var cut = RenderGrid(p => p.Add(c => c.DataTable, table));
            cut.Find(Row).Click();
            Assert.Same(table.Rows[0], cut.Instance.SelectedRow);

            table.Rows[0].AcceptChanges();
            table.Rows[0].Delete();

            Assert.Null(cut.Instance.SelectedRow);
        }

        [Fact]
        [DisplayName("A click reports the row id to RowSelected, and a double-click to RowActivated")]
        public void Click_ReportsTheRowId()
        {
            Guid? selected = null;
            Guid? activated = null;
            var cut = RenderGrid(p => p
                .Add(c => c.DataTable, BuildListTable())
                .Add(c => c.RowSelected, id => selected = id)
                .Add(c => c.RowActivated, id => activated = id));

            cut.Find(Row).Click();
            cut.Find(Row).DoubleClick();

            Assert.Equal(s_firstId, selected);
            Assert.Equal(s_firstId, activated);
            Assert.Contains("polhem-grid__row--selected", cut.Find(Row).ClassName, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A host's selected row id marks only the row it names")]
        public void SelectedRowId_MarksOnlyThatRow()
        {
            var cut = RenderGrid(p => p.Add(c => c.DataTable, BuildListTable()).Add(c => c.SelectedRowId, Guid.NewGuid()));

            Assert.DoesNotContain("polhem-grid__row--selected", cut.Find(Row).ClassName, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Add on a grid without a data object does nothing")]
        public void AddClicked_WithoutDataObject_DoesNothing()
        {
            var table = BuildListTable();
            var cut = RenderGrid(p => p.Add(c => c.DataTable, table).Add(c => c.Layout, BuildLayout()));

            cut.Find("button.add").Click();

            Assert.Single(table.Rows);
        }

        [Fact]
        [DisplayName("Add in in-cell mode appends a row without opening the edit form")]
        public void AddClicked_InCell_AddsWithoutForm()
        {
            var dataObject = BuildOrder();
            var cut = RenderGrid(p => p.Add(c => c.DataObject, dataObject).Add(c => c.Layout, BuildLayout()));

            cut.Find("button.add").Click();

            Assert.Single(dataObject.DataSet.Tables[TableName]!.Rows);
            Assert.Equal("closed", cut.Find(".form").TextContent);
        }

        [Fact]
        [DisplayName("Edit opens the selected row in the edit form, and does nothing without a selection")]
        public void EditSelected_OpensTheSelectedRow()
        {
            var dataObject = BuildOrder();
            var cut = RenderGrid(p => p
                .Add(c => c.DataObject, dataObject)
                .Add(c => c.Layout, BuildLayout())
                .Add(c => c.EditMode, GridEditMode.EditForm));
            cut.Find("button.edit").Click();
            Assert.Equal("closed", cut.Find(".form").TextContent);
            cut.InvokeAsync(() => cut.Instance.AddRow());

            cut.Find("button.edit").Click();

            Assert.Equal("open", cut.Find(".form").TextContent);
        }

        [Fact]
        [DisplayName("Cancelling the edit form of an existing row keeps the row")]
        public void EditCancelled_ExistingRow_KeepsTheRow()
        {
            var dataObject = BuildOrder();
            var cut = RenderGrid(p => p
                .Add(c => c.DataObject, dataObject)
                .Add(c => c.Layout, BuildLayout())
                .Add(c => c.EditMode, GridEditMode.EditForm));
            cut.Find("button.add").Click();
            cut.Find("button.commit").Click();
            cut.Find(Row).DoubleClick();
            Assert.Equal("open", cut.Find(".form").TextContent);

            cut.Find("button.cancel").Click();

            Assert.Equal("closed", cut.Find(".form").TextContent);
            Assert.Single(dataObject.DataSet.Tables[TableName]!.Rows);
        }

        [Fact]
        [DisplayName("Without the Edit action a double-click opens no edit form")]
        public void Activate_WithoutEditAction_OpensNoForm()
        {
            var dataObject = BuildOrder();
            var cut = RenderGrid(p => p
                .Add(c => c.DataObject, dataObject)
                .Add(c => c.Layout, BuildLayout(GridControlAllowActions.Add))
                .Add(c => c.EditMode, GridEditMode.EditForm));
            cut.InvokeAsync(() => cut.Instance.AddRow());

            cut.Find(Row).DoubleClick();

            Assert.Equal("closed", cut.Find(".form").TextContent);
        }

        [Fact]
        [DisplayName("A layout with no data shows the toolbar the layout allows, and a View state hides it")]
        public void Toolbar_LayoutOnly_FollowsTheState()
        {
            var cut = RenderGrid(p => p.Add(c => c.Layout, BuildLayout()));
            Assert.Equal("shown", cut.Find(Toolbar).TextContent);

            cut.InvokeAsync(() => cut.Instance.SetControlState(SingleFormMode.View));

            Assert.Equal("hidden", cut.Find(Toolbar).TextContent);
        }

        [Fact]
        [DisplayName("A grid without a layout shows no toolbar, no columns and an empty identity")]
        public void NoLayout_HasNothingToShow()
        {
            var cut = RenderGrid(p => p.Add(c => c.DataTable, BuildListTable()));

            Assert.Equal("hidden", cut.Find(Toolbar).TextContent);
            Assert.Empty(cut.FindAll(Header));
            Assert.Equal(string.Empty, cut.Instance.Identity);
        }

        [Fact]
        [DisplayName("The layout identity names the table and the column fields")]
        public void LayoutIdentity_NamesTableAndColumns()
        {
            var cut = RenderGrid(p => p.Add(c => c.Layout, BuildLayout()));

            Assert.Equal("Line|item,qty,note", cut.Instance.Identity);
        }

        [Theory]
        [InlineData(SortDirection.Asc, "ascending")]
        [InlineData(SortDirection.Desc, "descending")]
        [DisplayName("The sorted column's header carries its aria-sort, and other columns none")]
        public void AriaSort_FollowsTheSortedColumn(SortDirection direction, string expected)
        {
            var cut = RenderGrid(p => p
                .Add(c => c.DataTable, BuildListTable())
                .Add(c => c.Layout, BuildLayout())
                .Add(c => c.SortFieldName, "QTY")
                .Add(c => c.SortDirection, direction));

            var headers = cut.FindAll(Header);
            Assert.Equal(expected, headers[1].GetAttribute("aria-sort"));
            Assert.Null(headers[0].GetAttribute("aria-sort"));
        }

        [Fact]
        [DisplayName("A bound grid sorts only the columns its schema can sort, and shows the required cue from the schema")]
        public void Columns_Bound_FollowTheSchema()
        {
            var cut = RenderGrid(p => p
                .Add(c => c.DataObject, BuildOrder())
                .Add(c => c.Layout, BuildLayout())
                .Add(c => c.OnSortRequested, _ => { })
                .Add(c => c.Filters, new ListFilterState())
                .Add(c => c.OnFilterChanged, _ => { }));

            var headers = cut.FindAll(Header);
            Assert.Equal("true", headers[0].GetAttribute("data-sortable"));
            Assert.Equal("false", headers[2].GetAttribute("data-sortable"));
            Assert.Contains("polhem-grid__header--required", headers[0].ClassName, StringComparison.Ordinal);
            Assert.Equal(nameof(ColumnFilterKind.Number), headers[1].GetAttribute("data-filter"));
        }

        [Fact]
        [DisplayName("A list-mode grid resolves its columns through the FormTable, and offers no filter without a handler")]
        public void Columns_ListMode_UseTheFormTable()
        {
            var formTable = new FormTable(TableName, "Line");
            formTable.Fields!.Add("qty", "Qty", FieldDbType.Decimal);
            var cut = RenderGrid(p => p
                .Add(c => c.DataTable, BuildListTable())
                .Add(c => c.FormTable, formTable)
                .Add(c => c.Layout, BuildLayout())
                .Add(c => c.OnSortRequested, _ => { })
                .Add(c => c.Filters, new ListFilterState()));

            var headers = cut.FindAll(Header);
            Assert.Equal("false", headers[0].GetAttribute("data-sortable"));
            Assert.Equal("true", headers[1].GetAttribute("data-sortable"));
            Assert.Equal(nameof(ColumnFilterKind.None), headers[1].GetAttribute("data-filter"));
        }

        [Fact]
        [DisplayName("A numeric column's editor gets the currency and unit settings, and a text column's does not")]
        public void EditorParameters_NumericColumn_CarryTheSettings()
        {
            var currencies = new CurrencySettings();
            var table = BuildListTable();
            var layout = BuildLayout();
            var cut = RenderGrid(p => p
                .Add(c => c.DataTable, table)
                .Add(c => c.Layout, layout)
                .Add(c => c.CurrencySettings, currencies)
                .Add(c => c.DefaultCurrencyCode, "TWD"));

            var numeric = cut.Instance.EditorParameters(layout.Columns![1], table.Rows[0]);
            var text = cut.Instance.EditorParameters(layout.Columns[0], table.Rows[0]);

            Assert.Same(currencies, numeric[nameof(NumericEditorBase.CurrencySettings)]);
            Assert.Equal("TWD", numeric[nameof(NumericEditorBase.DefaultCurrencyCode)]);
            Assert.False(text.ContainsKey(nameof(NumericEditorBase.CurrencySettings)));
        }

        [Fact]
        [DisplayName("A cell offers no record to open outside a scope that opens one")]
        public async Task OpenTarget_WithoutScope_IsNull()
        {
            var table = BuildListTable();
            var layout = BuildLayout();
            var cut = RenderGrid(p => p.Add(c => c.DataTable, table).Add(c => c.Layout, layout));

            Assert.Null(cut.Instance.Target(table.Rows[0], layout.Columns![0]));
            await cut.Instance.OpenAsync(new Polhem.UI.Core.Fields.RelationTarget("Dept", Guid.NewGuid()));
        }

        [Fact]
        [DisplayName("RefreshRows shows a row added to a list-mode table in place")]
        public void RefreshRows_ShowsRowsAddedInPlace()
        {
            var table = BuildListTable();
            var cut = RenderGrid(p => p.Add(c => c.DataTable, table));

            table.Rows.Add(Guid.NewGuid(), "Nut", 1m, "second");
            cut.InvokeAsync(cut.Instance.RefreshRows);

            cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(Row).Count));
        }

        [Fact]
        [DisplayName("Disposing a grid twice is harmless")]
        public async Task Dispose_Twice_IsHarmless()
        {
            var cut = RenderGrid(p => p.Add(c => c.DataObject, BuildOrder()).Add(c => c.TableName, TableName));

            cut.Instance.Dispose();
            var exception = await Record.ExceptionAsync(() => cut.Instance.DisposeAsync().AsTask());

            Assert.Null(exception);
        }

        // A grid that draws plain markup: a header per column, a row per visible row, and the toolbar and edit form
        // commands as buttons.
        private sealed class ProbeGrid : GridControlBase
        {
            public string Identity => LayoutIdentity;

            public Dictionary<string, object?> EditorParameters(LayoutColumn column, DataRow row) => BuildEditorParameters(column, row);

            public Polhem.UI.Core.Fields.RelationTarget? Target(DataRow row, LayoutColumn column) => OpenTarget(row, column);

            public Task OpenAsync(Polhem.UI.Core.Fields.RelationTarget target) => OpenRelationAsync(target);

            protected override void BuildRenderTree(RenderTreeBuilder builder)
            {
                builder.OpenElement(0, "span");
                builder.AddAttribute(1, "class", "toolbar");
                builder.AddContent(2, ShowToolbar ? "shown" : "hidden");
                builder.CloseElement();
                AddButton(builder, 3, "add", OnAddClicked);
                AddButton(builder, 10, "edit", EditSelectedRow);
                AddButton(builder, 20, "commit", OnEditCommitted);
                AddButton(builder, 30, "cancel", OnEditCancelled);
                builder.OpenElement(40, "span");
                builder.AddAttribute(41, "class", "form");
                builder.AddContent(42, EditingRow is null ? "closed" : "open");
                builder.CloseElement();
                foreach (var column in VisibleColumns)
                {
                    builder.OpenElement(50, "div");
                    builder.AddAttribute(51, "class", "header " + HeaderClass(column));
                    builder.AddAttribute(52, "aria-sort", AriaSort(column));
                    builder.AddAttribute(53, "data-sortable", IsSortable(column) ? "true" : "false");
                    builder.AddAttribute(54, "data-filter", FilterKind(column).ToString());
                    builder.CloseElement();
                }
                foreach (var row in VisibleRows)
                {
                    builder.OpenElement(60, "div");
                    builder.AddAttribute(61, "class", "row " + RowClass(row));
                    builder.AddAttribute(62, "onclick", EventCallback.Factory.Create(this, () => SelectRowAsync(row)));
                    builder.AddAttribute(63, "ondblclick", EventCallback.Factory.Create(this, () => ActivateRowAsync(row)));
                    builder.CloseElement();
                }
            }

            private void AddButton(RenderTreeBuilder builder, int sequence, string name, Action handler)
            {
                builder.OpenElement(sequence, "button");
                builder.AddAttribute(sequence + 1, "class", name);
                builder.AddAttribute(sequence + 2, "onclick", EventCallback.Factory.Create(this, handler));
                builder.CloseElement();
            }
        }
    }
}
