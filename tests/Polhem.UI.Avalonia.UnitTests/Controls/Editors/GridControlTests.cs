using Polhem.Api.Client;
using Polhem.Definition.Collections;
using System.ComponentModel;
using System.Data;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Polhem.Base.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.DataObjects;
using Polhem.Tests.Shared;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// Structural + behaviour checks for <see cref="GridControl"/>: layout-driven
    /// column generation, the two explicit bind paths, row selection and the
    /// formatting helpers carried over from the retired <c>DynamicGrid</c>.
    /// </summary>
    public class GridControlTests
    {
        private static LayoutGrid BuildEmployeeListLayout()
        {
            var layout = new LayoutGrid("Employee", "Employees");
            layout.Columns!.Add(new LayoutColumn { FieldName = "sys_id", Caption = "Employee ID", Visible = true });
            layout.Columns.Add(new LayoutColumn { FieldName = "sys_name", Caption = "Name", Visible = true, Width = 120 });
            layout.Columns.Add(new LayoutColumn { FieldName = "hire_date", Caption = "Hire Date", Visible = true });
            layout.Columns.Add(new LayoutColumn { FieldName = "internal_notes", Caption = "Notes", Visible = false });
            return layout;
        }

        private static DataTable BuildEmployeeRows()
        {
            var table = new DataTable("Employee");
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            table.Columns.Add("sys_id", typeof(string));
            table.Columns.Add("sys_name", typeof(string));
            table.Columns.Add("hire_date", typeof(DateTime));
            table.Rows.Add(Guid.NewGuid(), "E001", "Alice Chen", new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Unspecified));
            table.Rows.Add(Guid.NewGuid(), "E002", "Bob Liu", new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Unspecified));
            return table;
        }

        private static FormDataObject BuildDataObjectWithDetail()
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add("emp_name", "Name", FieldDbType.String);
            var detail = schema.Tables.Add("EmployeePhone", "Phones");
            detail.Fields!.Add("phone", "Phone", FieldDbType.String);
            var phoneType = detail.Fields.Add("type", "Type", FieldDbType.String);
            phoneType.ListItems!.Add("OF", "Office");
            phoneType.ListItems.Add("MB", "Mobile");

            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        private static void InvokeSelectionChangedHandler(GridControl grid)
        {
            var method = typeof(GridControl).GetMethod(
                "OnSelectionChangedCore", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            method!.Invoke(grid, new object?[] { null, null });
        }

        private static StackPanel GetToolbar(GridControl grid)
            => Assert.IsType<StackPanel>(Assert.IsType<DockPanel>(grid.Content).Children[0]);

        [Fact]
        [DisplayName("GridControl is a ContentControl composite containing a toolbar and a DataGrid")]
        public void Type_IsContentControlCompositeWithBaseStyleKey()
        {
            var grid = new GridControl();

            Assert.IsType<ContentControl>(grid, exactMatch: false);
            var styleKey = typeof(global::Avalonia.StyledElement)
                .GetProperty("StyleKeyOverride", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(grid);
            Assert.Equal(typeof(ContentControl), styleKey);

            var host = Assert.IsType<DockPanel>(grid.Content);
            Assert.IsType<StackPanel>(host.Children[0]);
            Assert.Same(grid.InnerGrid, host.Children[1]);
        }

        [Fact]
        [DisplayName("Defaults are read-only, single selection, no auto-generated columns and a hidden toolbar")]
        public void Defaults_AreReadOnlySingleSelection()
        {
            var grid = new GridControl();

            Assert.True(grid.InnerGrid.IsReadOnly);
            Assert.False(grid.InnerGrid.AutoGenerateColumns);
            Assert.Equal(DataGridSelectionMode.Single, grid.InnerGrid.SelectionMode);
            Assert.False(grid.AllowEdit);
            Assert.False(GetToolbar(grid).IsVisible);
        }

        [Fact]
        [DisplayName("Bind(layout, rows) builds only the visible columns and attaches the data")]
        public void Bind_LayoutAndRows_BuildsVisibleColumns()
        {
            var grid = new GridControl();

            grid.Bind(BuildEmployeeListLayout(), BuildEmployeeRows());

            // Three visible columns (sys_id, sys_name, hire_date); internal_notes hidden.
            Assert.Equal(3, grid.InnerGrid.Columns.Count);
            Assert.Equal("Employee ID", grid.InnerGrid.Columns[0].Header);
            Assert.Equal("Name", grid.InnerGrid.Columns[1].Header);
            Assert.Equal("Hire Date", grid.InnerGrid.Columns[2].Header);
            Assert.NotNull(grid.InnerGrid.ItemsSource);
            Assert.Equal("Employee", grid.TableName);
        }

        [Fact]
        [DisplayName("LayoutColumn.Width greater than 0 becomes a fixed column width")]
        public void Bind_ColumnWithWidth_AppliesPixelWidth()
        {
            var grid = new GridControl();

            grid.Bind(BuildEmployeeListLayout(), BuildEmployeeRows());

            Assert.Equal(120d, grid.InnerGrid.Columns[1].Width.Value);
            Assert.Equal(DataGridLengthUnitType.Pixel, grid.InnerGrid.Columns[1].Width.UnitType);
            Assert.Equal(DataGridLengthUnitType.Star, grid.InnerGrid.Columns[0].Width.UnitType);
        }

        [Fact]
        [DisplayName("Bind(dataObject, layout) resolves the detail table by TableName")]
        public void Bind_DataObjectDetail_ResolvesTable()
        {
            var dataObject = BuildDataObjectWithDetail();
            var layout = new LayoutGrid("EmployeePhone", "Phones");
            layout.Columns!.Add(new LayoutColumn { FieldName = "phone", Caption = "Phone", Visible = true });

            var grid = new GridControl();
            grid.Bind(dataObject, layout);

            Assert.Same(dataObject.DataSet.Tables["EmployeePhone"], grid.DataTable);
            Assert.Single(grid.InnerGrid.Columns);
        }

        [Fact]
        [DisplayName("Bind(dataObject, layout) binds empty (headers only) when the table is not found")]
        public void Bind_DataObjectMissingTable_BindsEmpty()
        {
            var dataObject = BuildDataObjectWithDetail();
            var layout = new LayoutGrid("NoSuchTable", "Missing");
            layout.Columns!.Add(new LayoutColumn { FieldName = "x", Caption = "X", Visible = true });

            var grid = new GridControl();
            grid.Bind(dataObject, layout);

            Assert.Null(grid.DataTable);
            Assert.Null(grid.InnerGrid.ItemsSource);
            Assert.Single(grid.InnerGrid.Columns);
        }

        [Fact]
        [DisplayName("Setting the DataTable property rebuilds the rows and keeps the columns of the existing layout")]
        public void DataTable_Setter_RebuildsRowsKeepsColumns()
        {
            var grid = new GridControl();
            grid.Bind(BuildEmployeeListLayout(), null);
            Assert.Null(grid.InnerGrid.ItemsSource);

            grid.DataTable = BuildEmployeeRows();

            Assert.NotNull(grid.InnerGrid.ItemsSource);
            Assert.Equal(3, grid.InnerGrid.Columns.Count);
        }

        [Fact]
        [DisplayName("The RowSelected event returns the sys_rowid Guid of the selected row")]
        public void RowSelected_InvokesHandlerWithSysRowId()
        {
            var rows = BuildEmployeeRows();
            var expectedRowId = (Guid)rows.Rows[1][SysFields.RowId];

            var grid = new GridControl();
            grid.Bind(BuildEmployeeListLayout(), rows);

            Guid? received = null;
            grid.RowSelected += (_, rowId) => received = rowId;

            grid.SelectedItem = rows.DefaultView[1];
            // Selection without a realized visual tree may not raise SelectionChanged,
            // so drive the private handler directly; a duplicate invocation is harmless
            // because the assertion compares the (idempotent) payload.
            InvokeSelectionChangedHandler(grid);

            Assert.Equal(expectedRowId, received);
        }

        [Fact]
        [DisplayName("RowSelected does not fire when the row has no sys_rowid")]
        public void RowSelected_NoSysRowIdColumn_DoesNotInvokeHandler()
        {
            var table = new DataTable("Employee");
            table.Columns.Add("sys_id", typeof(string));
            table.Rows.Add("E999");
            var layout = new LayoutGrid("Employee", "Employees");
            layout.Columns!.Add(new LayoutColumn { FieldName = "sys_id", Caption = "Employee ID", Visible = true });

            var grid = new GridControl();
            grid.Bind(layout, table);

            var invoked = false;
            grid.RowSelected += (_, _) => invoked = true;

            grid.SelectedItem = table.DefaultView[0];
            InvokeSelectionChangedHandler(grid);

            Assert.False(invoked);
        }

        [Fact]
        [DisplayName("After the DataSet is replaced, the new detail table instance is resolved again by TableName")]
        public async Task Bind_DataObjectDetail_DataSetReplaced_ReResolvesTable()
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add("emp_name", "Name", FieldDbType.String);
            var detail = schema.Tables.Add("EmployeePhone", "Phones");
            detail.Fields!.Add("phone", "Phone", FieldDbType.String);

            var refreshed = new DataSet("Employee");
            refreshed.Tables.Add(new DataTable("Employee"));
            var refreshedDetail = new DataTable("EmployeePhone");
            refreshedDetail.Columns.Add("phone", typeof(string));
            refreshedDetail.Rows.Add("02-1234-5678");
            refreshed.Tables.Add(refreshedDetail);

            var connector = new FakeFormApiConnector
            {
                GetNewDataHandler = () => new Polhem.Api.Core.Messages.Form.GetNewDataResponse { DataSet = refreshed },
            };
            var dataObject = new FormDataObject(schema, connector);

            var layout = new LayoutGrid("EmployeePhone", "Phones");
            layout.Columns!.Add(new LayoutColumn { FieldName = "phone", Caption = "Phone", Visible = true });
            var grid = new GridControl();
            grid.Bind(dataObject, layout);
            var originalTable = grid.DataTable;

            await dataObject.NewAsync();

            Assert.NotSame(originalTable, grid.DataTable);
            Assert.Same(refreshedDetail, grid.DataTable);
        }

        [Fact]
        [DisplayName("With a detail binding and AllowActions.Edit, Edit mode is editable and View mode is read-only")]
        public void SetControlState_DetailBoundEditMode_TogglesReadOnly()
        {
            var dataObject = BuildDataObjectWithDetail();
            var layout = new LayoutGrid("EmployeePhone", "Phones");
            layout.Columns!.Add(new LayoutColumn { FieldName = "phone", Caption = "Phone", Visible = true });

            var grid = new GridControl();
            grid.Bind(dataObject, layout);

            grid.SetControlState(SingleFormMode.Edit);
            Assert.False(grid.InnerGrid.IsReadOnly);

            grid.SetControlState(SingleFormMode.View);
            Assert.True(grid.InnerGrid.IsReadOnly);
        }

        [Fact]
        [DisplayName("Without Edit in AllowActions, every mode is read-only")]
        public void SetControlState_AllowActionsWithoutEdit_StaysReadOnly()
        {
            var dataObject = BuildDataObjectWithDetail();
            var layout = new LayoutGrid("EmployeePhone", "Phones")
            {
                AllowActions = GridControlAllowActions.Add | GridControlAllowActions.Delete,
            };
            layout.Columns!.Add(new LayoutColumn { FieldName = "phone", Caption = "Phone", Visible = true });

            var grid = new GridControl();
            grid.Bind(dataObject, layout);
            grid.SetControlState(SingleFormMode.Edit);

            Assert.True(grid.InnerGrid.IsReadOnly);
        }

        [Fact]
        [DisplayName("List mode (no FormDataObject) stays read-only")]
        public void SetControlState_ListMode_StaysReadOnly()
        {
            var grid = new GridControl();
            grid.Bind(BuildEmployeeListLayout(), BuildEmployeeRows());

            grid.SetControlState(SingleFormMode.Edit);

            Assert.True(grid.InnerGrid.IsReadOnly);
        }

        [Fact]
        [DisplayName("SetControlState maps AllowEdit: off in View, on in Add/Edit")]
        public void SetControlState_MapsFormModeToAllowEdit()
        {
            var dataObject = BuildDataObjectWithDetail();
            var layout = new LayoutGrid("EmployeePhone", "Phones");
            layout.Columns!.Add(new LayoutColumn { FieldName = "phone", Caption = "Phone", Visible = true });
            var grid = new GridControl();
            grid.Bind(dataObject, layout);

            grid.SetControlState(SingleFormMode.View);
            Assert.False(grid.AllowEdit);

            grid.SetControlState(SingleFormMode.Add);
            Assert.True(grid.AllowEdit);

            grid.SetControlState(SingleFormMode.Edit);
            Assert.True(grid.AllowEdit);
        }

        [Fact]
        [DisplayName("SetControlState narrows the editable modes by LayoutGrid.AllowEditModes")]
        public void SetControlState_AllowEditModes_NarrowsModes()
        {
            var dataObject = BuildDataObjectWithDetail();
            var layout = new LayoutGrid("EmployeePhone", "Phones") { AllowEditModes = FormEditModes.Add };
            layout.Columns!.Add(new LayoutColumn { FieldName = "phone", Caption = "Phone", Visible = true });
            var grid = new GridControl();
            grid.Bind(dataObject, layout);

            grid.SetControlState(SingleFormMode.Add);
            Assert.True(grid.AllowEdit);
            Assert.False(grid.InnerGrid.IsReadOnly);

            grid.SetControlState(SingleFormMode.Edit);
            Assert.False(grid.AllowEdit);
            Assert.True(grid.InnerGrid.IsReadOnly);

            grid.SetControlState(SingleFormMode.View);
            Assert.False(grid.AllowEdit);
        }

        [Fact]
        [DisplayName("With AllowEditModes=None no mode is editable")]
        public void SetControlState_AllowEditModesNone_NeverEditable()
        {
            var dataObject = BuildDataObjectWithDetail();
            var layout = new LayoutGrid("EmployeePhone", "Phones") { AllowEditModes = FormEditModes.None };
            layout.Columns!.Add(new LayoutColumn { FieldName = "phone", Caption = "Phone", Visible = true });
            var grid = new GridControl();
            grid.Bind(dataObject, layout);

            grid.SetControlState(SingleFormMode.Add);
            Assert.False(grid.AllowEdit);

            grid.SetControlState(SingleFormMode.Edit);
            Assert.False(grid.AllowEdit);
        }

        [Fact]
        [DisplayName("With a detail binding the toolbar shows and hides with AllowEdit")]
        public void AllowEdit_DetailBound_TogglesToolbarVisibility()
        {
            var dataObject = BuildDataObjectWithDetail();
            var layout = new LayoutGrid("EmployeePhone", "Phones");
            layout.Columns!.Add(new LayoutColumn { FieldName = "phone", Caption = "Phone", Visible = true });
            var grid = new GridControl();

            // The ambient form mode defaults to Edit, so the explicit bind leaves
            // the grid editable with the toolbar shown.
            grid.Bind(dataObject, layout);
            Assert.True(grid.AllowEdit);
            Assert.True(GetToolbar(grid).IsVisible);
            Assert.False(grid.InnerGrid.IsReadOnly);

            grid.AllowEdit = false;
            Assert.False(GetToolbar(grid).IsVisible);
            Assert.True(grid.InnerGrid.IsReadOnly);
        }

        [Fact]
        [DisplayName("A list-mode binding keeps the toolbar hidden and read-only even with AllowEdit on")]
        public void AllowEdit_ListMode_KeepsToolbarHiddenAndReadOnly()
        {
            var grid = new GridControl();
            grid.Bind(BuildEmployeeListLayout(), BuildEmployeeRows());

            grid.AllowEdit = true;

            Assert.False(GetToolbar(grid).IsVisible);
            Assert.True(grid.InnerGrid.IsReadOnly);
        }

        [Fact]
        [DisplayName("Toolbar buttons show according to AllowActions and EditMode")]
        public void Toolbar_ButtonVisibility_FollowsAllowActionsAndEditMode()
        {
            var dataObject = BuildDataObjectWithDetail();
            var layout = new LayoutGrid("EmployeePhone", "Phones");
            layout.Columns!.Add(new LayoutColumn { FieldName = "phone", Caption = "Phone", Visible = true });
            var grid = new GridControl();
            grid.Bind(dataObject, layout);

            var toolbar = GetToolbar(grid);
            var addButton = Assert.IsType<Button>(toolbar.Children[0]);
            var editButton = Assert.IsType<Button>(toolbar.Children[1]);
            var deleteButton = Assert.IsType<Button>(toolbar.Children[2]);

            // In-cell editing needs no Edit button (cells edit in place).
            Assert.True(addButton.IsVisible);
            Assert.False(editButton.IsVisible);
            Assert.True(deleteButton.IsVisible);

            grid.EditMode = GridEditMode.EditForm;
            Assert.True(editButton.IsVisible);

            var restricted = new LayoutGrid("EmployeePhone", "Phones")
            {
                AllowActions = GridControlAllowActions.Add | GridControlAllowActions.Delete,
            };
            restricted.Columns!.Add(new LayoutColumn { FieldName = "phone", Caption = "Phone", Visible = true });
            var restrictedGrid = new GridControl { EditMode = GridEditMode.EditForm };
            restrictedGrid.Bind(dataObject, restricted);

            var restrictedToolbar = GetToolbar(restrictedGrid);
            Assert.True(restrictedToolbar.IsVisible);
            Assert.True(Assert.IsType<Button>(restrictedToolbar.Children[0]).IsVisible);
            Assert.False(Assert.IsType<Button>(restrictedToolbar.Children[1]).IsVisible);
            Assert.True(Assert.IsType<Button>(restrictedToolbar.Children[2]).IsVisible);
        }

        [Fact]
        [DisplayName("The toolbar does not show when AllowActions=None")]
        public void Toolbar_NoAllowedActions_StaysHidden()
        {
            var dataObject = BuildDataObjectWithDetail();
            var layout = new LayoutGrid("EmployeePhone", "Phones")
            {
                AllowActions = GridControlAllowActions.None,
            };
            layout.Columns!.Add(new LayoutColumn { FieldName = "phone", Caption = "Phone", Visible = true });
            var grid = new GridControl();
            grid.Bind(dataObject, layout);

            Assert.True(grid.AllowEdit);
            Assert.False(GetToolbar(grid).IsVisible);
            Assert.True(grid.InnerGrid.IsReadOnly);
        }

        [Fact]
        [DisplayName("AddRow adds a row, fills NOT NULL defaults and marks dirty")]
        public void AddRow_AppendsRowAndMarksDirty()
        {
            var dataObject = BuildDataObjectWithDetail();
            var layout = new LayoutGrid("EmployeePhone", "Phones");
            layout.Columns!.Add(new LayoutColumn { FieldName = "phone", Caption = "Phone", Visible = true });
            var grid = new GridControl();
            grid.Bind(dataObject, layout);

            grid.AddRow();

            Assert.Equal(1, grid.DataTable!.Rows.Count);
            Assert.True(dataObject.IsDirty);
        }

        [Fact]
        [DisplayName("AddRow fills typed empty values for a wire-shaped table (NOT NULL columns without DefaultValue)")]
        public void AddRow_WireShapedTable_SeedsNonNullDefaults()
        {
            var table = new DataTable("Items");
            table.Columns.Add(new DataColumn("name", typeof(string)) { AllowDBNull = false });
            table.Columns.Add(new DataColumn("qty", typeof(int)) { AllowDBNull = false });
            var layout = new LayoutGrid("Items", "Items");
            layout.Columns!.Add(new LayoutColumn { FieldName = "name", Caption = "Name", Visible = true });

            var grid = new GridControl();
            grid.Bind(layout, table);
            grid.AddRow();

            var row = table.Rows[0];
            Assert.Equal(string.Empty, row["name"]);
            Assert.Equal(0, row["qty"]);
        }

        [Fact]
        [DisplayName("DeleteSelectedRow marks the selected row Deleted and marks dirty")]
        public void DeleteSelectedRow_MarksRowDeletedAndDirty()
        {
            var dataObject = BuildDataObjectWithDetail();
            var layout = new LayoutGrid("EmployeePhone", "Phones");
            layout.Columns!.Add(new LayoutColumn { FieldName = "phone", Caption = "Phone", Visible = true });
            var grid = new GridControl();
            grid.Bind(dataObject, layout);
            grid.AddRow();
            grid.DataTable!.AcceptChanges();

            grid.SelectedItem = grid.DataTable.DefaultView[0];
            grid.DeleteSelectedRow();

            Assert.Equal(DataRowState.Deleted, grid.DataTable.Rows[0].RowState);
            Assert.True(dataObject.IsDirty);
        }

        [Fact]
        [DisplayName("The text cell editor writes back to the DataRow and keeps the original value on invalid input")]
        public void BuildCellEditor_TextEditor_WritesBackAndIgnoresInvalid()
        {
            var table = new DataTable("Items");
            table.Columns.Add("name", typeof(string));
            table.Columns.Add("qty", typeof(int));
            table.Rows.Add("Widget", 5);
            var grid = new GridControl();
            grid.Bind(new LayoutGrid("Items", "Items"), table);

            var method = typeof(GridControl).GetMethod(
                "BuildCellEditor", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var rowView = table.DefaultView[0];

            var nameEditor = Assert.IsType<TextBox>(method!.Invoke(
                grid, new object?[] { rowView, new LayoutColumn { FieldName = "name" } }));
            // In-cell text commits on Enter / leaving the cell, not per keystroke.
            nameEditor.Text = "Gadget";
            Assert.NotEqual("Gadget", table.Rows[0]["name"]);
            nameEditor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
            Assert.Equal("Gadget", table.Rows[0]["name"]);

            var qtyEditor = Assert.IsType<TextBox>(method.Invoke(
                grid, new object?[] { rowView, new LayoutColumn { FieldName = "qty" } }));
            qtyEditor.Text = "abc";
            qtyEditor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
            Assert.Equal(5, table.Rows[0]["qty"]);
            qtyEditor.Text = "12";
            qtyEditor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
            Assert.Equal(12, table.Rows[0]["qty"]);
        }

        [Fact]
        [DisplayName("The time cell editor normalizes to fixed-width HH:mm on write-back and keeps the original value on invalid input")]
        public void BuildCellEditor_TimeColumn_NormalizesAndIgnoresInvalid()
        {
            var table = new DataTable("Shifts");
            table.AddColumn("work_start", FieldDbType.Time);
            table.Rows.Add("08:00");
            var grid = new GridControl();
            grid.Bind(new LayoutGrid("Shifts", "Shifts"), table);

            var method = typeof(GridControl).GetMethod(
                "BuildCellEditor", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var editor = Assert.IsType<TextBox>(method!.Invoke(
                grid, new object?[] { table.DefaultView[0], new LayoutColumn("work_start", "Start", ControlType.TimeEdit) }));

            // The grid has no dedicated time editor, so fixed width must come from the shared write path.
            editor.Text = "8:30";
            editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
            Assert.Equal("08:30", table.Rows[0]["work_start"]);

            editor.Text = "25:00";
            editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
            Assert.Equal("08:30", table.Rows[0]["work_start"]);
        }

        [Fact]
        [DisplayName("The CheckEdit cell editor writes a boolean back through a CheckBox")]
        public void BuildCellEditor_CheckEditor_WritesBoolean()
        {
            var table = new DataTable("Items");
            table.Columns.Add("ok", typeof(bool));
            table.Rows.Add(false);
            var grid = new GridControl();
            grid.Bind(new LayoutGrid("Items", "Items"), table);

            var method = typeof(GridControl).GetMethod(
                "BuildCellEditor", BindingFlags.NonPublic | BindingFlags.Instance);
            var editor = Assert.IsType<CheckBox>(method!.Invoke(
                grid, new object?[] { table.DefaultView[0], new LayoutColumn { FieldName = "ok", ControlType = ControlType.CheckEdit } }));

            editor.IsChecked = true;

            Assert.True((bool)table.Rows[0]["ok"]);
        }

        [Fact]
        [DisplayName("LayoutColumn.ReadOnly makes the DataGrid column read-only (checked on an editable grid)")]
        public void Bind_ReadOnlyColumn_SetsColumnReadOnly()
        {
            // The DataGridColumn.IsReadOnly getter coerces with the owning grid's
            // IsReadOnly, so the per-column flag is only observable on an editable
            // (detail-bound, Edit-mode) grid.
            var dataObject = BuildDataObjectWithDetail();
            var layout = new LayoutGrid("EmployeePhone", "Phones");
            layout.Columns!.Add(new LayoutColumn { FieldName = "phone", Caption = "Phone", Visible = true, ReadOnly = true });
            layout.Columns.Add(new LayoutColumn { FieldName = "type", Caption = "Type", Visible = true });

            var grid = new GridControl();
            grid.Bind(dataObject, layout);
            grid.SetControlState(SingleFormMode.Edit);

            Assert.False(grid.InnerGrid.IsReadOnly);
            Assert.True(grid.InnerGrid.Columns[0].IsReadOnly);
            Assert.False(grid.InnerGrid.Columns[1].IsReadOnly);
        }

        [Fact]
        [DisplayName("A read-only column header is wrapped in parentheses and an editable one stays as is")]
        public void Bind_ReadOnlyColumn_HeaderParenthesised()
        {
            var dataObject = BuildDataObjectWithDetail();
            var layout = new LayoutGrid("EmployeePhone", "Phones");
            layout.Columns!.Add(new LayoutColumn { FieldName = "phone", Caption = "Phone", Visible = true, ReadOnly = true });
            layout.Columns.Add(new LayoutColumn { FieldName = "type", Caption = "Type", Visible = true });

            var grid = new GridControl();
            grid.Bind(dataObject, layout);

            Assert.Equal("(Phone)", grid.InnerGrid.Columns[0].Header);
            Assert.Equal("Type", grid.InnerGrid.Columns[1].Header);
        }

        [Fact]
        [DisplayName("Popup-type columns (Check/DropDown/Date/YearMonth) bypass the edit pipeline and use a resident editor")]
        public void BuildColumn_PopupEditorTypes_BypassEditPipeline()
        {
            var dataObject = BuildDataObjectWithDetail();
            var layout = new LayoutGrid("EmployeePhone", "Phones");
            layout.Columns!.Add(new LayoutColumn("phone", "Phone", ControlType.TextEdit));
            layout.Columns.Add(new LayoutColumn("type", "Type", ControlType.DropDownEdit));
            layout.Columns.Add(new LayoutColumn("ok", "OK", ControlType.CheckEdit));

            var grid = new GridControl();
            grid.Bind(dataObject, layout);

            var textColumn = Assert.IsType<DataGridTemplateColumn>(grid.InnerGrid.Columns[0]);
            Assert.NotNull(textColumn.CellEditingTemplate);

            var dropDownColumn = Assert.IsType<DataGridTemplateColumn>(grid.InnerGrid.Columns[1]);
            Assert.Null(dropDownColumn.CellEditingTemplate);
            Assert.True(dropDownColumn.IsReadOnly);

            var checkColumn = Assert.IsType<DataGridTemplateColumn>(grid.InnerGrid.Columns[2]);
            Assert.Null(checkColumn.CellEditingTemplate);
            Assert.True(checkColumn.IsReadOnly);
        }

        [Fact]
        [DisplayName("Interactive cells: a boolean is a centered check box; a read-only popup type is text and an editable one is a click-to-replace host")]
        public void BuildInteractiveCell_StateVariants_BuildExpectedControls()
        {
            var table = new DataTable("Items");
            table.Columns.Add("ok", typeof(bool));
            table.Rows.Add(true);
            var rowView = table.DefaultView[0];
            var column = new LayoutColumn("ok", "OK", ControlType.CheckEdit);

            var method = typeof(GridControl).GetMethod(
                "BuildInteractiveCell", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            // Boolean cells render a centred checkbox in every state: disabled on a
            // read-only (list-mode) grid, interactive on an editable one.
            var readOnlyGrid = new GridControl();
            readOnlyGrid.Bind(new LayoutGrid("Items", "Items"), table);
            var readOnlyCell = Assert.IsType<CheckBox>(method!.Invoke(readOnlyGrid, new object?[] { rowView, column }));
            Assert.False(readOnlyCell.IsEnabled);
            Assert.True(readOnlyCell.IsChecked);
            Assert.Null(readOnlyCell.Content);
            Assert.Equal(global::Avalonia.Layout.VerticalAlignment.Center, readOnlyCell.VerticalAlignment);

            var dataObject = BuildDataObjectWithDetail();
            var editableLayout = new LayoutGrid("EmployeePhone", "Phones");
            editableLayout.Columns!.Add(column);
            var editableGrid = new GridControl();
            editableGrid.Bind(dataObject, editableLayout);
            Assert.False(editableGrid.InnerGrid.IsReadOnly);
            var editableCell = Assert.IsType<CheckBox>(method.Invoke(editableGrid, new object?[] { rowView, column }));
            Assert.True(editableCell.IsEnabled);

            // Popup columns: text on a read-only grid, click-to-edit host on an
            // editable one (resting content is still the plain text).
            var dateColumn = new LayoutColumn("ok", "OK", ControlType.DateEdit);
            Assert.IsType<TextBlock>(method.Invoke(readOnlyGrid, new object?[] { rowView, dateColumn }));
            var swapHost = Assert.IsType<ContentControl>(
                method.Invoke(editableGrid, new object?[] { rowView, dateColumn }), exactMatch: false);
            Assert.IsType<TextBlock>(swapHost.Content);
        }

        [Fact]
        [DisplayName("Date cell editors use the segmented DatePicker and write back by format")]
        public void BuildCellEditor_DateEditor_UsesSegmentedDatePicker()
        {
            var table = new DataTable("Items");
            table.Columns.Add("d", typeof(DateTime));
            table.Columns.Add("ym", typeof(string));
            table.Rows.Add(new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Unspecified), "2026-06");
            var rowView = table.DefaultView[0];
            var grid = new GridControl();
            grid.Bind(new LayoutGrid("Items", "Items"), table);

            var method = typeof(GridControl).GetMethod(
                "BuildCellEditor", BindingFlags.NonPublic | BindingFlags.Instance);

            var datePicker = Assert.IsType<DatePicker>(method!.Invoke(
                grid, new object?[] { rowView, new LayoutColumn("d", "D", ControlType.DateEdit) }));
            Assert.True(datePicker.DayVisible);
            Assert.Equal(new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Unspecified), datePicker.SelectedDate!.Value.DateTime);
            datePicker.SelectedDate = new DateTimeOffset(
                new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero);
            Assert.Equal(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Unspecified), (DateTime)table.Rows[0]["d"]);

            var monthPicker = Assert.IsType<DatePicker>(method.Invoke(
                grid, new object?[] { rowView, new LayoutColumn("ym", "YM", ControlType.YearMonthEdit) }));
            Assert.False(monthPicker.DayVisible);
            monthPicker.SelectedDate = new DateTimeOffset(
                new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero);
            Assert.Equal("2026-07", table.Rows[0]["ym"]);
        }

        [Fact]
        [DisplayName("The DropDown cell editor writes ListItem.Value back to the DataRow after a selection")]
        public void BuildCellEditor_DropDownEditor_WritesBackValue()
        {
            var dataObject = BuildDataObjectWithDetail();
            var layout = new LayoutGrid("EmployeePhone", "Phones");
            layout.Columns!.Add(new LayoutColumn("type", "Type", ControlType.DropDownEdit));
            var grid = new GridControl();
            grid.Bind(dataObject, layout);

            var detailTable = grid.DataTable!;
            detailTable.Rows.Add("0912-345-678", "OF");
            var rowView = detailTable.DefaultView[0];

            var method = typeof(GridControl).GetMethod(
                "BuildCellEditor", BindingFlags.NonPublic | BindingFlags.Instance);
            var combo = Assert.IsType<ComboBox>(method!.Invoke(
                grid, new object?[] { rowView, new LayoutColumn("type", "Type", ControlType.DropDownEdit) }));

            var selected = Assert.IsType<Polhem.Definition.Collections.ListItem>(combo.SelectedItem);
            Assert.Equal("OF", selected.Value);

            combo.SelectedIndex = 1;

            Assert.Equal("MB", detailTable.Rows[0]["type"]);
        }

        [Fact]
        [DisplayName("EndEdit does not throw when nothing is being edited")]
        public void EndEdit_NoActiveEdit_DoesNotThrow()
        {
            var grid = new GridControl();
            grid.Bind(BuildEmployeeListLayout(), BuildEmployeeRows());

            var exception = Record.Exception(grid.EndEdit);

            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("TryGetRowId accepts a Guid column and a parsable Guid string, and returns false for DBNull")]
        public void TryGetRowId_VariantInputs_Behaviour()
        {
            // The grid reads the selected row's id through the helper it shares with the Blazor grid.
            var guidTable = new DataTable();
            guidTable.Columns.Add(SysFields.RowId, typeof(Guid));
            var expectedGuid = Guid.NewGuid();
            guidTable.Rows.Add(expectedGuid);
            Assert.True(FormDataGuard.TryGetRowId(guidTable.Rows[0], out var fromGuid));
            Assert.Equal(expectedGuid, fromGuid);

            var stringTable = new DataTable();
            stringTable.Columns.Add(SysFields.RowId, typeof(string));
            stringTable.Rows.Add(expectedGuid.ToString());
            Assert.True(FormDataGuard.TryGetRowId(stringTable.Rows[0], out var fromString));
            Assert.Equal(expectedGuid, fromString);

            var nullTable = new DataTable();
            nullTable.Columns.Add(SysFields.RowId, typeof(Guid));
            nullTable.Rows.Add(DBNull.Value);
            Assert.False(FormDataGuard.TryGetRowId(nullTable.Rows[0], out _));

            var noColumnTable = new DataTable();
            noColumnTable.Columns.Add("other", typeof(string));
            noColumnTable.Rows.Add("x");
            Assert.False(FormDataGuard.TryGetRowId(noColumnTable.Rows[0], out _));
        }

        [Fact]
        [DisplayName("FormatCell returns the expected string for dates, format strings, null and missing columns in the user's culture (de-DE)")]
        public void FormatCell_VariantInputs_FormatsExpectedString()
        {
            using var culture = new CultureScope("de-DE");
            var method = typeof(GridControl).GetMethod(
                "FormatCell", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            var table = new DataTable();
            table.Columns.Add("date_only", typeof(DateTime));
            table.Columns.Add("ts", typeof(DateTime));
            table.Columns.Add("amount", typeof(decimal));
            table.Columns.Add("nullable", typeof(string));
            table.Rows.Add(
                new DateTime(2026, 5, 23, 0, 0, 0, DateTimeKind.Unspecified),
                new DateTime(2026, 5, 23, 9, 30, 15, DateTimeKind.Unspecified),
                1234.56m,
                DBNull.Value);
            var row = table.DefaultView[0];

            string Format(string fieldName, string displayFormat = "", string numberFormat = "")
                => (string)method!.Invoke(null, new object?[] { row, fieldName, displayFormat, numberFormat })!;

            Assert.Equal("23.05.2026", Format("date_only"));
            Assert.Equal("23.05.2026 09:30:15", Format("ts"));
            Assert.Equal("1.234,56", Format("amount", displayFormat: "N2"));
            Assert.Equal("1234,6", Format("amount", numberFormat: "F1"));
            Assert.Equal(string.Empty, Format("nullable"));
            Assert.Equal(string.Empty, Format("not_a_column"));

            var nullRow = (string)method!.Invoke(null, new object?[] { null, "date_only", "", "" })!;
            Assert.Equal(string.Empty, nullRow);
        }

        /// <summary>
        /// Test double that bypasses the real JSON-RPC pipeline by overriding the
        /// virtual CRUD methods used here. Mirrors the fake in
        /// <c>FormDataObjectTests</c>.
        /// </summary>
        private sealed class FakeFormApiConnector : Polhem.Api.Client.Connectors.FormApiConnector
        {
            public FakeFormApiConnector() : base(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.NewGuid(), "Employee") { }

            public Func<Polhem.Api.Core.Messages.Form.GetNewDataResponse>? GetNewDataHandler { get; set; }

            public override Task<Polhem.Api.Core.Messages.Form.GetNewDataResponse> GetNewDataAsync(CancellationToken cancellationToken = default)
                => Task.FromResult((GetNewDataHandler ?? (() => new Polhem.Api.Core.Messages.Form.GetNewDataResponse()))());
        }
    }
}
