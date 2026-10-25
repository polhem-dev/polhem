using System.ComponentModel;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Contracts.Form;
using Polhem.UI.Core.Definitions;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Server.Controls.Editors;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.UnitTests.Controls.Editors
{
    /// <summary>
    /// Behavior tests for <see cref="ButtonEdit"/>, aligned with the Avalonia head's <c>ButtonEdit</c>: the
    /// display fields, the lookup flow, clearing, and the plain-button mode.
    /// </summary>
    public class ButtonEditTests : BunitContext
    {
        private const string InputSelector = "input.polhem-field-editor--button";
        private const string ButtonSelector = "button.polhem-button-edit__button";

        private IRenderedComponent<ButtonEdit> RenderEditor(FormDataObject dataObject, Action<Exception>? failed = null)
            => Render<ButtonEdit>(p =>
            {
                p.Add(c => c.FieldName, LookupTestData.LookupField).Add(c => c.DataObject, dataObject);
                if (failed is not null) p.Add(c => c.LookupFailed, failed);
            });

        private readonly FakeLookupSource _source = new();

        public ButtonEditTests()
        {
            // The dialog the editor opens resolves its schema and connector from these, as in a host
            // without a Polhem backend.
            Services.AddScoped<IFormDefinitionSource>(_ => _source);
            Services.AddScoped<IFormApiConnectorFactory>(_ => _source);
        }

        [Fact]
        [DisplayName("A lookup editor shows its display fields and is read-only")]
        public void Render_Lookup_ShowsDisplayFieldsReadOnly()
        {
            var dataObject = LookupTestData.BuildEmployee();
            dataObject.SetField("ref_dept_id", "D01");
            dataObject.SetField("ref_dept_name", "Engineering");

            var cut = RenderEditor(dataObject);

            var input = cut.Find(InputSelector);
            Assert.Equal("D01 - Engineering", input.GetAttribute("value"));
            Assert.True(input.HasAttribute("readonly"));
            Assert.True(cut.Instance.HasLookup);
        }

        [Fact]
        [DisplayName("Choosing a row in the lookup writes the row id and the mapped fields")]
        public void Lookup_Commit_WritesSelection()
        {
            var dataObject = LookupTestData.BuildEmployee();
            var cut = RenderEditor(dataObject);

            cut.Find(ButtonSelector).Click();
            cut.FindAll("tr.polhem-dynamic-grid__row")[1].DoubleClick();

            Assert.Equal(LookupTestData.SalesId, (Guid)dataObject.MasterRow![LookupTestData.LookupField]);
            Assert.Equal("Sales", dataObject.GetField("ref_dept_name"));
            Assert.Equal("D02 - Sales", cut.Find(InputSelector).GetAttribute("value"));
            Assert.Empty(cut.FindComponents<LookupDialog>());
        }

        [Fact]
        [DisplayName("Delete clears the selection and its display fields")]
        public void Delete_ClearsSelection()
        {
            var dataObject = LookupTestData.BuildEmployee();
            dataObject.SetField(LookupTestData.LookupField, LookupTestData.EngineeringId.ToString());
            dataObject.SetField("ref_dept_name", "Engineering");
            var cut = RenderEditor(dataObject);

            cut.Find(InputSelector).KeyDown("Delete");

            Assert.Equal(string.Empty, dataObject.GetField("ref_dept_name"));
            Assert.Equal(string.Empty, cut.Find(InputSelector).GetAttribute("value"));
        }

        [Fact]
        [DisplayName("The button is hidden and Delete does nothing in view mode")]
        public void ViewMode_HidesButtonAndIgnoresDelete()
        {
            var dataObject = LookupTestData.BuildEmployee();
            dataObject.SetField("ref_dept_name", "Engineering");

            var cut = Render<FormScope>(p => p
                .Add(c => c.DataObject, dataObject)
                .Add(c => c.FormMode, SingleFormMode.View)
                .AddChildContent<ButtonEdit>(e => e.Add(c => c.FieldName, LookupTestData.LookupField)));
            cut.Find(InputSelector).KeyDown("Delete");

            Assert.Empty(cut.FindAll(ButtonSelector));
            Assert.Equal("Engineering", dataObject.GetField("ref_dept_name"));
        }

        [Fact]
        [DisplayName("A selection whose mapping is outside the lookup result reports LookupFailed")]
        public void Lookup_MissingMappedField_ReportsFailure()
        {
            var dataObject = LookupTestData.BuildEmployee();
            dataObject.GetFormField(LookupTestData.LookupField)!.RelationFieldMappings!.Add("sys_missing", "ref_dept_name");
            Exception? failure = null;
            var cut = RenderEditor(dataObject, ex => failure = ex);

            cut.Find(ButtonSelector).Click();
            cut.FindAll("tr.polhem-dynamic-grid__row")[0].DoubleClick();

            Assert.IsType<InvalidOperationException>(failure);
        }

        [Fact]
        [DisplayName("Without relation metadata the editor is a text input whose button raises ButtonClick")]
        public void NoLookup_ButtonRaisesButtonClick()
        {
            var schema = new FormSchema("Note", "Note");
            schema.Tables!.Add("Note", "Note").Fields!.Add("code", "Code", FieldDbType.String);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            var clicks = 0;
            var cut = Render<ButtonEdit>(p => p
                .Add(c => c.FieldName, "code")
                .Add(c => c.DataObject, dataObject)
                .Add(c => c.ButtonClick, () => clicks++));

            cut.Find(InputSelector).Change("N-1");
            cut.Find(ButtonSelector).Click();

            Assert.False(cut.Instance.HasLookup);
            Assert.Equal("N-1", dataObject.GetField("code"));
            Assert.Equal(1, clicks);
            Assert.Empty(cut.FindComponents<LookupDialog>());
        }
    }
}
