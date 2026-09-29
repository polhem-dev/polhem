using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Server.DataObjects;
using Bunit;
using Polhem.Definition.Layouts;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// Render tests: they run BuildRenderTree of DynamicForm.razor to cover the lines of the .razor template.
    /// </summary>
    public class DynamicFormRenderTests : BunitContext
    {
        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add("emp_id", "ID", FieldDbType.String);
            master.Fields.Add("is_active", "Active", FieldDbType.Boolean);
            master.Fields.Add("created_at", "Created", FieldDbType.DateTime);
            return schema;
        }

        [Fact]
        [DisplayName("DynamicForm renders the empty-state div when Layout is null")]
        public void DynamicForm_NullLayout_RendersEmptyDiv()
        {
            var cut = Render<DynamicForm>();
            Assert.NotNull(cut.Find("div.polhem-dynamic-form--empty"));
        }

        [Fact]
        [DisplayName("DynamicForm renders the form container when given Layout and DataObject")]
        public void DynamicForm_WithLayoutAndDataObject_RendersFormContainer()
        {
            var schema = BuildSchema();
            var layout = FormLayoutGenerator.Generate(schema, "default");
            var dataObject = new FormDataObject(schema);

            var cut = Render<DynamicForm>(p => p
                .Add(c => c.Layout, layout)
                .Add(c => c.DataObject, dataObject));

            Assert.NotNull(cut.Find("div.polhem-dynamic-form"));
        }

        [Fact]
        [DisplayName("DynamicForm does not render a legend element when the Section's ShowCaption is false")]
        public void DynamicForm_SectionShowCaptionFalse_DoesNotRenderLegend()
        {
            var schema = BuildSchema();
            var layout = FormLayoutGenerator.Generate(schema, "default");
            foreach (var section in layout.Sections!)
                section.ShowCaption = false;
            var dataObject = new FormDataObject(schema);

            var cut = Render<DynamicForm>(p => p
                .Add(c => c.Layout, layout)
                .Add(c => c.DataObject, dataObject));

            Assert.Empty(cut.FindAll("legend.polhem-dynamic-form__section-caption"));
        }
    }
}
