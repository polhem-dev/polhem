using Polhem.Definition.Collections;
using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Server.DataObjects;
using Bunit;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// 補強 DynamicForm.razor switch 中 YearMonthEdit / MemoEdit / DropDownEdit 分支的渲染覆蓋率。
    /// </summary>
    public class DynamicFormControlTypeRenderTests : BunitContext
    {
        private static void SetFirstFieldControlType(FormLayout layout, ControlType controlType)
        {
            layout.Sections![0].Fields![0].ControlType = controlType;
        }

        [Fact]
        [DisplayName("DynamicForm YearMonthEdit 欄位應渲染 input[type=month] 元素")]
        public void DynamicForm_YearMonthEditField_RendersMonthInput()
        {
            var schema = new FormSchema("T", "T");
            schema.Tables!.Add("T", "T").Fields!.Add("report_month", "Month", FieldDbType.String);
            var layout = FormLayoutGenerator.Generate(schema, "default");
            SetFirstFieldControlType(layout, ControlType.YearMonthEdit);
            var dataObject = new FormDataObject(schema);

            var cut = Render<DynamicForm>(p => p
                .Add(c => c.Layout, layout)
                .Add(c => c.DataObject, dataObject));

            Assert.NotNull(cut.Find("input[type='month']"));
        }

        [Fact]
        [DisplayName("DynamicForm MemoEdit 欄位應渲染 textarea 元素")]
        public void DynamicForm_MemoEditField_RendersTextarea()
        {
            var schema = new FormSchema("T", "T");
            schema.Tables!.Add("T", "T").Fields!.Add("remark", "Remark", FieldDbType.String);
            var layout = FormLayoutGenerator.Generate(schema, "default");
            SetFirstFieldControlType(layout, ControlType.MemoEdit);
            var dataObject = new FormDataObject(schema);

            var cut = Render<DynamicForm>(p => p
                .Add(c => c.Layout, layout)
                .Add(c => c.DataObject, dataObject));

            Assert.NotNull(cut.Find("textarea.polhem-dynamic-form__input--memo"));
        }

        [Fact]
        [DisplayName("DynamicForm DropDownEdit 欄位應渲染 select 元素並包含選項")]
        public void DynamicForm_DropDownEditField_RendersSelectWithOptions()
        {
            var schema = new FormSchema("T", "T");
            var master = schema.Tables!.Add("T", "T");
            var schemaField = master.Fields!.Add("status", "Status", FieldDbType.String);
            schemaField.ListItems!.Add("A", "Active");
            schemaField.ListItems.Add("I", "Inactive");
            var layout = FormLayoutGenerator.Generate(schema, "default");
            SetFirstFieldControlType(layout, ControlType.DropDownEdit);
            var dataObject = new FormDataObject(schema);

            var cut = Render<DynamicForm>(p => p
                .Add(c => c.Layout, layout)
                .Add(c => c.DataObject, dataObject));

            Assert.NotNull(cut.Find("select.polhem-dynamic-form__input--select"));
            Assert.Equal(2, cut.FindAll("option").Count);
        }
    }
}
