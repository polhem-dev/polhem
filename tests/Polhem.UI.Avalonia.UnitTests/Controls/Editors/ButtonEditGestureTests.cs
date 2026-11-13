using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Core.DataObjects;
using Polhem.UI.Core.Fields;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// <see cref="ButtonEdit"/> driven through the routed events input raises: its search button, the keys it handles
    /// or passes on, and the double tap that opens a related record.
    /// </summary>
    /// <remarks>
    /// No services are registered in this test assembly, so opening the lookup dialog fails before it shows anything;
    /// that is the failure path the editor reports.
    /// </remarks>
    public class ButtonEditGestureTests
    {
        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Order", "Order");
            var table = schema.Tables!.Add("Order", "Order");
            table.Fields!.Add(new FormField(SysFields.RowId, "Row", FieldDbType.Guid));
            table.Fields.Add(new FormField(SysFields.Id, "Number", FieldDbType.String));
            var customer = new FormField("customer_rowid", "Customer", FieldDbType.Guid) { RelationProgId = "Customer" };
            customer.RelationFieldMappings!.Add(SysFields.Name, "ref_customer_name");
            table.Fields.Add(customer);
            table.Fields.Add(new FormField("ref_customer_name", "Customer name", FieldDbType.String, FieldType.RelationField));
            return schema;
        }

        private static (ButtonEdit Editor, FormDataObject DataObject) BindLookup(SingleFormMode formMode, bool handled = false,
            Guid? customerRowId = null)
        {
            var schema = BuildSchema();
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            if (customerRowId is { } rowId)
                dataObject.SetField("customer_rowid", rowId.ToString());
            var layoutField = FormLayoutGenerator.Generate(schema, "default")
                .Sections![0].Fields!.First(f => f.FieldName == "customer_rowid");
            var editor = new ButtonEdit();
            var parent = new Border { Child = editor };
            FormScope.SetFormMode(parent, formMode);
            if (handled) RelationOpening.SetIsEnabled(parent, true);
            editor.Bind(dataObject, layoutField);
            return (editor, dataObject);
        }

        private static void ClickSearch(ButtonEdit editor)
            => Assert.IsType<Button>(editor.InnerRightContent).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        private static KeyEventArgs PressKey(Control target, Key key)
        {
            var args = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key };
            target.RaiseEvent(args);
            return args;
        }

        private static void DoubleTap(Control target)
        {
            var press = new PointerPressedEventArgs(target, new Pointer(1, PointerType.Mouse, isPrimary: true), target, default, 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
                KeyModifiers.None, clickCount: 2);
            target.RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, press));
        }

        [Fact]
        [DisplayName("A lookup that cannot open its dialog raises LookupFailed with the reason")]
        public void SearchButton_LookupCannotOpen_RaisesLookupFailed()
        {
            var (editor, _) = BindLookup(SingleFormMode.Edit);
            Exception? failure = null;
            editor.LookupFailed += (_, ex) => failure = ex;

            ClickSearch(editor);

            var error = Assert.IsType<InvalidOperationException>(failure);
            Assert.Contains("Customer", error.Message, StringComparison.Ordinal);
            Assert.Null(ToolTip.GetTip(editor));
        }

        [Fact]
        [DisplayName("Without a LookupFailed handler the failure is shown as the editor's tooltip")]
        public void SearchButton_LookupCannotOpenWithoutHandler_ShowsTooltip()
        {
            var (editor, _) = BindLookup(SingleFormMode.Edit);

            ClickSearch(editor);

            Assert.Contains("Customer", Assert.IsType<string>(ToolTip.GetTip(editor)), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The search button of a field without a lookup and without a ButtonClick handler does nothing")]
        public void SearchButton_NoLookupNoHandler_DoesNothing()
        {
            var dataObject = new FormDataObject(BuildSchema());
            dataObject.InitializeNewMaster();
            var editor = new ButtonEdit();
            editor.Bind(dataObject, SysFields.Id);

            var exception = Record.Exception(() => ClickSearch(editor));

            Assert.Null(exception);
            Assert.Null(ToolTip.GetTip(editor));
        }

        [Fact]
        [DisplayName("A key other than Delete or Backspace is passed on to the text box in lookup mode")]
        public void KeyDown_OtherKeyInLookupMode_IsNotHandledAsClear()
        {
            var customerRowId = Guid.NewGuid();
            var (editor, dataObject) = BindLookup(SingleFormMode.Edit, customerRowId: customerRowId);

            PressKey(editor, Key.A);

            Assert.Equal(customerRowId, dataObject.MasterRow!["customer_rowid"]);
        }

        [Fact]
        [DisplayName("Delete in View mode does not clear the lookup selection")]
        public void KeyDown_DeleteInViewMode_KeepsSelection()
        {
            var customerRowId = Guid.NewGuid();
            var (editor, dataObject) = BindLookup(SingleFormMode.View, customerRowId: customerRowId);

            PressKey(editor, Key.Delete);

            Assert.Equal(customerRowId, dataObject.MasterRow!["customer_rowid"]);
        }

        [Fact]
        [DisplayName("A double tap on a relation field that offers its record raises the open request")]
        public void DoubleTap_OffersRecord_RaisesOpenRequest()
        {
            var customerRowId = Guid.NewGuid();
            var (editor, _) = BindLookup(SingleFormMode.View, handled: true, customerRowId);
            RelationTarget? requested = null;
            editor.AddHandler(RelationOpening.RequestedEvent, (_, e) => requested = e.Target);

            DoubleTap(editor);

            Assert.Equal(new RelationTarget("Customer", customerRowId), requested);
        }

        [Fact]
        [DisplayName("A double tap on a relation field that offers nothing raises no request")]
        public void DoubleTap_OffersNothing_RaisesNoRequest()
        {
            var (editor, _) = BindLookup(SingleFormMode.Edit, handled: true, Guid.NewGuid());
            var raised = false;
            editor.AddHandler(RelationOpening.RequestedEvent, (_, _) => raised = true);

            DoubleTap(editor);

            Assert.False(raised);
        }

        [Fact]
        [DisplayName("A field without a lookup writes the typed text back to its column")]
        public void Text_NoLookup_WritesBack()
        {
            var dataObject = new FormDataObject(BuildSchema());
            dataObject.InitializeNewMaster();
            var editor = new ButtonEdit();
            editor.Bind(dataObject, SysFields.Id);
            editor.SetControlState(SingleFormMode.Edit);

            editor.Text = "SO-001";
            PressKey(editor, Key.Enter);

            Assert.Equal("SO-001", dataObject.MasterRow![SysFields.Id]);
        }
    }
}
