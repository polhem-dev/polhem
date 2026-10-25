using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Web.Blazor.Server.Controls;
using Polhem.Web.Blazor.Server.DataObjects;
using Bunit;
using Polhem.Definition.Layouts;

namespace Polhem.Web.Blazor.Server.UnitTests.Controls
{
    /// <summary>
    /// Render tests: they run BuildRenderTree of FormLayoutPresenter.razor to cover the lines of the .razor template.
    /// </summary>
    public class FormLayoutPresenterRenderTests : BunitContext
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
        [DisplayName("FormLayoutPresenter renders the empty-state div when Layout is null")]
        public void FormLayoutPresenter_NullLayout_RendersEmptyDiv()
        {
            var cut = Render<FormLayoutPresenter>();
            Assert.NotNull(cut.Find("div.polhem-form-layout--empty"));
        }

        [Fact]
        [DisplayName("FormLayoutPresenter renders the form container when given Layout and DataObject")]
        public void FormLayoutPresenter_WithLayoutAndDataObject_RendersFormContainer()
        {
            var schema = BuildSchema();
            var layout = FormLayoutGenerator.Generate(schema, "default");
            var dataObject = new FormDataObject(schema);

            var cut = Render<FormLayoutPresenter>(p => p
                .Add(c => c.Layout, layout)
                .Add(c => c.DataObject, dataObject));

            Assert.NotNull(cut.Find("div.polhem-form-layout"));
        }

        [Fact]
        [DisplayName("FormLayoutPresenter does not render a legend element when the Section's ShowCaption is false")]
        public void FormLayoutPresenter_SectionShowCaptionFalse_DoesNotRenderLegend()
        {
            var schema = BuildSchema();
            var layout = FormLayoutGenerator.Generate(schema, "default");
            foreach (var section in layout.Sections!)
                section.ShowCaption = false;
            var dataObject = new FormDataObject(schema);

            var cut = Render<FormLayoutPresenter>(p => p
                .Add(c => c.Layout, layout)
                .Add(c => c.DataObject, dataObject));

            Assert.Empty(cut.FindAll("legend.polhem-form-layout__section-caption"));
        }
    }
}
