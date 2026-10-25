using Polhem.Definition.Collections;
using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Server.Controls;
using Polhem.Web.Blazor.Server.DataObjects;
using Bunit;

namespace Polhem.Web.Blazor.Server.UnitTests.Controls
{
    /// <summary>
    /// Checks that FormLayoutPresenter renders the editor <see cref="Polhem.Web.Blazor.Server.Controls.Editors.FieldEditorFactory"/> picks for a YearMonthEdit, MemoEdit and DropDownEdit field.
    /// </summary>
    public class FormLayoutPresenterControlTypeRenderTests : BunitContext
    {
        private static void SetFirstFieldControlType(FormLayout layout, ControlType controlType)
        {
            layout.Sections![0].Fields![0].ControlType = controlType;
        }

        [Fact]
        [DisplayName("FormLayoutPresenter renders an input[type=month] element for a YearMonthEdit field")]
        public void FormLayoutPresenter_YearMonthEditField_RendersMonthInput()
        {
            var schema = new FormSchema("T", "T");
            schema.Tables!.Add("T", "T").Fields!.Add("report_month", "Month", FieldDbType.String);
            var layout = FormLayoutGenerator.Generate(schema, "default");
            SetFirstFieldControlType(layout, ControlType.YearMonthEdit);
            var dataObject = new FormDataObject(schema);

            var cut = Render<FormLayoutPresenter>(p => p
                .Add(c => c.Layout, layout)
                .Add(c => c.DataObject, dataObject));

            Assert.Single(cut.FindAll("input[type='month']"));
        }

        [Fact]
        [DisplayName("FormLayoutPresenter renders a textarea element for a MemoEdit field")]
        public void FormLayoutPresenter_MemoEditField_RendersTextarea()
        {
            var schema = new FormSchema("T", "T");
            schema.Tables!.Add("T", "T").Fields!.Add("remark", "Remark", FieldDbType.String);
            var layout = FormLayoutGenerator.Generate(schema, "default");
            SetFirstFieldControlType(layout, ControlType.MemoEdit);
            var dataObject = new FormDataObject(schema);

            var cut = Render<FormLayoutPresenter>(p => p
                .Add(c => c.Layout, layout)
                .Add(c => c.DataObject, dataObject));

            Assert.Single(cut.FindAll("textarea.polhem-field-editor--memo"));
        }

        [Fact]
        [DisplayName("FormLayoutPresenter renders a select element with the options for a DropDownEdit field")]
        public void FormLayoutPresenter_DropDownEditField_RendersSelectWithOptions()
        {
            var schema = new FormSchema("T", "T");
            var master = schema.Tables!.Add("T", "T");
            var schemaField = master.Fields!.Add("status", "Status", FieldDbType.String);
            schemaField.ListItems!.Add("A", "Active");
            schemaField.ListItems.Add("I", "Inactive");
            var layout = FormLayoutGenerator.Generate(schema, "default");
            SetFirstFieldControlType(layout, ControlType.DropDownEdit);
            var dataObject = new FormDataObject(schema);

            var cut = Render<FormLayoutPresenter>(p => p
                .Add(c => c.Layout, layout)
                .Add(c => c.DataObject, dataObject));

            Assert.NotNull(cut.Find("select.polhem-field-editor--select"));
            // The two items, plus the empty option shown while the stored value matches neither.
            Assert.Equal(3, cut.FindAll("option").Count);
        }
    }
}
