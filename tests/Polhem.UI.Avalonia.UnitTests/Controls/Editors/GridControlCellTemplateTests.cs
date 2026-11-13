using System.ComponentModel;
using System.Data;
using Avalonia.Controls;
using Avalonia.Input;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Collections;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Core.DataObjects;
using Polhem.UI.Core.Editing;
using Polhem.UI.Core.Fields;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// The cells of an editable <see cref="GridControl"/> as its <see cref="DataGrid"/> builds them, through each column's
    /// <c>CellTemplate</c> and <c>CellEditingTemplate</c>: the always-on check box, the click-to-edit swap cells of the
    /// popup editors, the text editors and the lookup cell.
    /// </summary>
    /// <remarks>
    /// No Avalonia platform runs, so the gestures are raised as routed events on the cells, the way input would reach
    /// them.
    /// </remarks>
    public class GridControlCellTemplateTests
    {
        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Order", "Order");
            var master = schema.Tables!.Add("Order", "Order");
            master.Fields!.Add(new FormField(SysFields.RowId, "Row", FieldDbType.Guid));
            var detail = schema.Tables.Add("Line", "Lines");
            detail.Fields!.Add(new FormField(SysFields.RowId, "Row", FieldDbType.Guid));
            detail.Fields.Add(new FormField(SysFields.MasterRowId, "Order", FieldDbType.Guid));
            detail.Fields.Add(new FormField("done", "Done", FieldDbType.Boolean));
            var status = detail.Fields.Add("status", "Status", FieldDbType.String);
            status.ListItems!.Add("O", "Open");
            status.ListItems.Add("C", "Closed");
            detail.Fields.Add(new FormField("ship_date", "Ship date", FieldDbType.Date));
            detail.Fields.Add(new FormField("stamp", "Stamp", FieldDbType.DateTime));
            detail.Fields.Add(new FormField("qty", "Quantity", FieldDbType.Integer));
            detail.Fields.Add(new FormField("note", "Note", FieldDbType.String));
            var product = new FormField("product_rowid", "Product", FieldDbType.Guid) { RelationProgId = "Product" };
            product.RelationFieldMappings!.Add(SysFields.Name, "ref_product_name");
            detail.Fields.Add(product);
            detail.Fields.Add(new FormField("ref_product_name", "Product name", FieldDbType.String, FieldType.RelationField));
            return schema;
        }

        private static LayoutGrid BuildLayout()
        {
            var layout = new LayoutGrid("Line", "Lines");
            layout.Columns!.Add(new LayoutColumn("done", "Done", ControlType.CheckEdit));
            layout.Columns.Add(new LayoutColumn("status", "Status", ControlType.DropDownEdit));
            layout.Columns.Add(new LayoutColumn("ship_date", "Ship date", ControlType.DateEdit));
            layout.Columns.Add(new LayoutColumn("stamp", "Stamp", ControlType.DateTimeEdit));
            layout.Columns.Add(new LayoutColumn("qty", "Quantity", ControlType.NumericEdit));
            layout.Columns.Add(new LayoutColumn("note", "Note", ControlType.TextEdit) { Required = true });
            layout.Columns.Add(new LayoutColumn("product_rowid", "Product", ControlType.ButtonEdit));
            return layout;
        }

        private sealed record Fixture(GridControl Grid, DataTable Lines, DataRowView Row);

        private static Fixture Bind(bool allowEdit = true, Action<LayoutGrid>? adjust = null)
        {
            var dataObject = new FormDataObject(BuildSchema());
            dataObject.InitializeNewMaster();
            var lines = dataObject.DataSet.Tables["Line"]!;
            var row = lines.NewRow();
            row[SysFields.RowId] = Guid.NewGuid();
            row["done"] = false;
            row["status"] = "O";
            row["qty"] = 5;
            row["note"] = "first";
            row["stamp"] = DBNull.Value;
            lines.Rows.Add(row);

            var layout = BuildLayout();
            adjust?.Invoke(layout);
            var grid = new GridControl { EditMode = GridEditMode.InCell };
            grid.Bind(dataObject, layout);
            // Binding takes the editing state from the ambient form mode, so the switch is set afterwards.
            grid.AllowEdit = allowEdit;
            return new Fixture(grid, lines, lines.DefaultView[0]);
        }

        private static DataGridTemplateColumn Column(GridControl grid, string caption)
            => grid.InnerGrid.Columns.OfType<DataGridTemplateColumn>()
                .First(c => Equals(c.Header, caption) || (c.Header as TextBlock)?.Text?.StartsWith(caption, StringComparison.Ordinal) == true);

        private static Control Cell(Fixture fixture, string caption)
            => Column(fixture.Grid, caption).CellTemplate!.Build(fixture.Row)!;

        private static Control EditingCell(Fixture fixture, string caption)
            => Column(fixture.Grid, caption).CellEditingTemplate!.Build(fixture.Row)!;

        private static void Press(Control target)
        {
            var args = new PointerPressedEventArgs(target, new Pointer(1, PointerType.Mouse, isPrimary: true), target, default, 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
                KeyModifiers.None);
            target.RaiseEvent(args);
        }

        private static void LoseFocus(Control target) => target.RaiseEvent(new FocusChangedEventArgs(InputElement.LostFocusEvent));

        private static void PressKey(Control target, Key key)
            => target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });

        [Fact]
        [DisplayName("An editable check-box cell is an enabled, centred check box that writes its state to the row")]
        public void CheckCell_Editable_WritesToRow()
        {
            var fixture = Bind();

            var checkBox = Assert.IsType<CheckBox>(Cell(fixture, "Done"));
            checkBox.IsChecked = true;

            Assert.True(checkBox.IsEnabled);
            Assert.Equal(global::Avalonia.Layout.HorizontalAlignment.Center, checkBox.HorizontalAlignment);
            Assert.Equal(true, fixture.Lines.Rows[0]["done"]);
        }

        [Fact]
        [DisplayName("A check-box cell of a grid that does not edit is a disabled check box")]
        public void CheckCell_NotEditable_IsDisabled()
        {
            var fixture = Bind(allowEdit: false);

            var checkBox = Assert.IsType<CheckBox>(Cell(fixture, "Done"));

            Assert.False(checkBox.IsEnabled);
            Assert.Equal(false, checkBox.IsChecked);
        }

        [Fact]
        [DisplayName("A drop-down cell of a grid that does not edit is plain text")]
        public void DropDownCell_NotEditable_IsText()
        {
            var fixture = Bind(allowEdit: false);

            Assert.Equal("Open", Assert.IsType<TextBlock>(Cell(fixture, "Status")).Text);
        }

        [Fact]
        [DisplayName("A press on a drop-down cell swaps in a combo box, and closing it after a pick writes and shows the value")]
        public void DropDownCell_PressPickAndClose_WritesAndSwapsBack()
        {
            var fixture = Bind();
            var host = Assert.IsType<ContentControl>(Cell(fixture, "Status"));
            Assert.Equal("Open", Assert.IsType<TextBlock>(host.Content).Text);

            Press(host);
            var combo = Assert.IsType<ComboBox>(host.Content);
            combo.SelectedIndex = 1;
            combo.IsDropDownOpen = true;
            combo.IsDropDownOpen = false;

            Assert.Equal("C", fixture.Lines.Rows[0]["status"]);
            Assert.Equal("Closed", Assert.IsType<TextBlock>(host.Content).Text);
        }

        [Fact]
        [DisplayName("A drop-down that closes without having opened keeps its editor")]
        public void DropDownCell_CloseWithoutOpen_KeepsEditor()
        {
            var fixture = Bind();
            var host = Assert.IsType<ContentControl>(Cell(fixture, "Status"));

            Press(host);
            var combo = Assert.IsType<ComboBox>(host.Content);
            combo.IsDropDownOpen = false;
            combo.MaxDropDownHeight = 100;

            Assert.Same(combo, host.Content);
        }

        [Fact]
        [DisplayName("A second press on a cell that already shows its editor leaves that editor in place")]
        public void SwapCell_PressWhileEditing_KeepsEditor()
        {
            var fixture = Bind();
            var host = Assert.IsType<ContentControl>(Cell(fixture, "Status"));
            Press(host);
            var editor = host.Content;

            Press(host);

            Assert.Same(editor, host.Content);
        }

        [Fact]
        [DisplayName("Starting an edit in one cell closes the editor still open in another")]
        public void SwapCell_PressAnotherCell_EndsFirstEdit()
        {
            var fixture = Bind();
            var status = Assert.IsType<ContentControl>(Cell(fixture, "Status"));
            var shipDate = Assert.IsType<ContentControl>(Cell(fixture, "Ship date"));
            Press(status);
            Assert.IsType<ComboBox>(status.Content);

            Press(shipDate);

            Assert.IsType<TextBlock>(status.Content);
            Assert.IsType<DatePicker>(shipDate.Content);
        }

        [Fact]
        [DisplayName("Picking a date in a date cell writes the date and swaps the cell back to text")]
        public void DateCell_PickDate_WritesAndSwapsBack()
        {
            var fixture = Bind();
            var host = Assert.IsType<ContentControl>(Cell(fixture, "Ship date"));

            Press(host);
            var picker = Assert.IsType<DatePicker>(host.Content);
            picker.SelectedDate = new DateTimeOffset(2026, 3, 4, 0, 0, 0, TimeSpan.Zero);

            Assert.Equal(new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Unspecified),
                ((DateTime)fixture.Lines.Rows[0]["ship_date"]).Date);
            Assert.IsType<TextBlock>(host.Content);
        }

        [Fact]
        [DisplayName("EndEdit closes an open swap-cell editor")]
        public void EndEdit_WithOpenSwapEditor_SwapsBack()
        {
            var fixture = Bind();
            var host = Assert.IsType<ContentControl>(Cell(fixture, "Ship date"));
            Press(host);

            fixture.Grid.EndEdit();

            Assert.IsType<TextBlock>(host.Content);
        }

        [Fact]
        [DisplayName("A numeric text cell's editor is right-aligned and writes its value when it loses focus")]
        public void NumericEditor_LostFocus_WritesValue()
        {
            var fixture = Bind();

            var editor = Assert.IsType<TextBox>(EditingCell(fixture, "Quantity"));
            editor.Text = "9";
            LoseFocus(editor);

            Assert.Equal(global::Avalonia.Media.TextAlignment.Right, editor.TextAlignment);
            Assert.Equal(9, fixture.Lines.Rows[0]["qty"]);
        }

        [Fact]
        [DisplayName("A text editor left with an unchanged value writes nothing")]
        public void TextEditor_UnchangedValue_LeavesRowUnchanged()
        {
            var fixture = Bind();
            fixture.Lines.AcceptChanges();

            var editor = Assert.IsType<TextBox>(EditingCell(fixture, "Note"));
            LoseFocus(editor);
            PressKey(editor, Key.Tab);

            Assert.Equal(DataRowState.Unchanged, fixture.Lines.Rows[0].RowState);
        }

        [Fact]
        [DisplayName("A date-time cell with no value starts empty and writes the date-time typed into it on Enter")]
        public void DateTimeEditor_EmptyValueThenEnter_WritesValue()
        {
            var fixture = Bind();

            var editor = Assert.IsType<TextBox>(EditingCell(fixture, "Stamp"));
            Assert.Equal(string.Empty, editor.Text);
            editor.Text = "2026-03-04 10:30";
            PressKey(editor, Key.Tab);
            Assert.Equal(DBNull.Value, fixture.Lines.Rows[0]["stamp"]);
            PressKey(editor, Key.Enter);

            Assert.IsType<DateTime>(fixture.Lines.Rows[0]["stamp"]);
        }

        [Fact]
        [DisplayName("A required column's header is a styled text block rather than a plain caption")]
        public void Header_RequiredColumn_IsStyledTextBlock()
        {
            var fixture = Bind();

            var header = Assert.IsType<TextBlock>(Column(fixture.Grid, "Note").Header);

            Assert.StartsWith("Note", header.Text, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A press on an editable lookup cell that cannot open its dialog shows the failure as the grid's tooltip")]
        public void LookupCell_PressWithoutServices_ShowsFailureTip()
        {
            var fixture = Bind();
            var cell = Assert.IsType<Border>(Cell(fixture, "Product"));

            Press(cell);

            var tip = Assert.IsType<string>(ToolTip.GetTip(fixture.Grid));
            Assert.Contains("Product", tip, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A double tap on a read-only relation cell that offers its record raises the open request")]
        public void RelationCell_DoubleTap_RaisesOpenRequest()
        {
            var rowId = Guid.NewGuid();
            var dataObject = new FormDataObject(BuildSchema());
            dataObject.InitializeNewMaster();
            var lines = dataObject.DataSet.Tables["Line"]!;
            var row = lines.NewRow();
            row["product_rowid"] = rowId;
            row["ref_product_name"] = "Bolt";
            lines.Rows.Add(row);
            var grid = new GridControl();
            var parent = new Border { Child = grid };
            FormScope.SetFormMode(parent, SingleFormMode.View);
            RelationOpening.SetIsEnabled(parent, true);
            grid.Bind(dataObject, BuildLayout());
            var cell = Assert.IsType<Border>(Column(grid, "Product").CellTemplate!.Build(lines.DefaultView[0])!);
            // A built cell has no parent yet, so the request is caught on the cell itself rather than bubbling up.
            RelationTarget? requested = null;
            cell.AddHandler(RelationOpening.RequestedEvent, (_, e) => requested = e.Target);

            var press = new PointerPressedEventArgs(cell, new Pointer(1, PointerType.Mouse, isPrimary: true), cell, default, 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
                KeyModifiers.None, clickCount: 2);
            cell.RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, press));

            Assert.Equal(new RelationTarget("Product", rowId), requested);
        }
    }
}
