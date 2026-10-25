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

        [Fact]
        [DisplayName("FormLayoutPresenter renders a detail grid after the sections, in edit-form mode when compact")]
        public void FormLayoutPresenter_Details_RenderGrid()
        {
            var schema = OrderTestData.BuildSchema();
            var layout = FormLayoutGenerator.Generate(schema, "default");
            var dataObject = OrderTestData.BuildOrder(schema);

            var cut = Render<FormLayoutPresenter>(p => p
                .Add(c => c.Layout, layout)
                .Add(c => c.DataObject, dataObject)
                .Add(c => c.IsCompact, true));

            Assert.Equal("Lines", cut.Find("legend.polhem-form-layout__detail-caption").TextContent);
            Assert.Equal(2, cut.FindAll("tr.polhem-grid__row").Count);
            Assert.Empty(cut.FindAll("td.polhem-grid__cell--editor"));
            Assert.Single(cut.FindAll(".polhem-grid__button--edit"));
        }

        [Fact]
        [DisplayName("FormLayoutPresenter lays the master fields out in one column when compact, clamping wider spans")]
        public void FormLayoutPresenter_Compact_RendersSingleColumn()
        {
            var schema = BuildSchema();
            var layout = FormLayoutGenerator.Generate(schema, "default");
            layout.ColumnCount = 3;
            layout.Sections![0].Fields![0].ColumnSpan = 2;

            var cut = Render<FormLayoutPresenter>(p => p
                .Add(c => c.Layout, layout)
                .Add(c => c.IsCompact, true));

            Assert.NotNull(cut.Find("div.polhem-form-layout.polhem-form-layout--compact"));
            Assert.Contains("repeat(1,", cut.Find("div.polhem-form-layout__grid").GetAttribute("style"), StringComparison.Ordinal);
            Assert.Contains("grid-column:span 1", cut.FindAll("div.polhem-form-layout__field")[0].GetAttribute("style"), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("FormLayoutPresenter keeps the layout's column count and spans when not compact")]
        public void FormLayoutPresenter_NotCompact_RendersLayoutColumns()
        {
            var schema = BuildSchema();
            var layout = FormLayoutGenerator.Generate(schema, "default");
            layout.ColumnCount = 3;
            layout.Sections![0].Fields![0].ColumnSpan = 2;

            var cut = Render<FormLayoutPresenter>(p => p.Add(c => c.Layout, layout));

            Assert.Empty(cut.FindAll("div.polhem-form-layout--compact"));
            Assert.Contains("repeat(3,", cut.Find("div.polhem-form-layout__grid").GetAttribute("style"), StringComparison.Ordinal);
            Assert.Contains("grid-column:span 2", cut.FindAll("div.polhem-form-layout__field")[0].GetAttribute("style"), StringComparison.Ordinal);
        }
    }
}
