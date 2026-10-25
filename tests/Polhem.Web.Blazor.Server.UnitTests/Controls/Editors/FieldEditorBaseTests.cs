using System.ComponentModel;
using System.Reflection;
using Bunit;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Server.Controls.Editors;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.UnitTests.Controls.Editors
{
    /// <summary>
    /// The binding behavior every field editor inherits from <see cref="FieldEditorBase"/>, exercised through
    /// <see cref="TextEdit"/>: where the binding comes from, the form mode, and refreshing on changes the
    /// editor did not make. These mirror the Avalonia head's <c>FieldEditorBinder</c> rules.
    /// </summary>
    public class FieldEditorBaseTests : BunitContext
    {
        private const string FieldName = "emp_name";
        private const string InputSelector = "input.polhem-field-editor--text";

        private static FormSchema BuildSchema(int maxLength = 0)
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add(FieldName, "Name", FieldDbType.String).MaxLength = maxLength;
            var detail = schema.Tables.Add("Skill", "Skill");
            detail.Fields!.Add("skill_name", "Skill", FieldDbType.String);
            return schema;
        }

        private static FormDataObject BuildDataObject(string value = "Ada", FormSchema? schema = null)
        {
            var dataObject = new FormDataObject(schema ?? BuildSchema());
            dataObject.InitializeNewMaster();
            dataObject.SetField(FieldName, value);
            return dataObject;
        }

        [Fact]
        [DisplayName("An editor inside a FormScope binds to the scope's data object")]
        public void Render_InsideFormScope_BindsToScopeDataObject()
        {
            var dataObject = BuildDataObject();

            var cut = Render<FormScope>(p => p
                .Add(c => c.DataObject, dataObject)
                .AddChildContent<TextEdit>(e => e.Add(c => c.FieldName, FieldName)));

            Assert.Equal("Ada", cut.Find(InputSelector).GetAttribute("value"));
        }

        [Fact]
        [DisplayName("The DataObject parameter wins over the enclosing FormScope")]
        public void Render_DataObjectParameter_WinsOverScope()
        {
            var scopeObject = BuildDataObject("Scope");
            var ownObject = BuildDataObject("Own");

            var cut = Render<FormScope>(p => p
                .Add(c => c.DataObject, scopeObject)
                .AddChildContent<TextEdit>(e => e
                    .Add(c => c.FieldName, FieldName)
                    .Add(c => c.DataObject, ownObject)));

            Assert.Equal("Own", cut.Find(InputSelector).GetAttribute("value"));
        }

        [Fact]
        [DisplayName("An explicit Bind wins over the parameters until Unbind")]
        public void Bind_Explicit_WinsUntilUnbind()
        {
            var parameterObject = BuildDataObject("Parameter");
            var explicitObject = BuildDataObject("Explicit");
            var cut = Render<TextEdit>(p => p
                .Add(c => c.FieldName, FieldName)
                .Add(c => c.DataObject, parameterObject));

            cut.InvokeAsync(() => cut.Instance.Bind(explicitObject, FieldName));
            Assert.Equal("Explicit", cut.Find(InputSelector).GetAttribute("value"));

            cut.InvokeAsync(() => cut.Instance.Unbind());
            Assert.Equal("Parameter", cut.Find(InputSelector).GetAttribute("value"));
        }

        [Fact]
        [DisplayName("A committed change writes the value back to the data object")]
        public void Change_WritesBack()
        {
            var dataObject = BuildDataObject();
            var cut = Render<TextEdit>(p => p.Add(c => c.FieldName, FieldName).Add(c => c.DataObject, dataObject));

            cut.Find(InputSelector).Change("Grace");

            Assert.Equal("Grace", dataObject.GetField(FieldName));
        }

        [Fact]
        [DisplayName("A value written by someone else refreshes the editor")]
        public void SetField_External_RefreshesEditor()
        {
            var dataObject = BuildDataObject();
            var cut = Render<TextEdit>(p => p.Add(c => c.FieldName, FieldName).Add(c => c.DataObject, dataObject));

            cut.InvokeAsync(() => dataObject.SetField(FieldName, "Linus"));

            Assert.Equal("Linus", cut.Find(InputSelector).GetAttribute("value"));
        }

        [Fact]
        [DisplayName("Resetting the data object refreshes the editor")]
        public void InitializeNewMaster_RefreshesEditor()
        {
            var dataObject = BuildDataObject();
            var cut = Render<TextEdit>(p => p.Add(c => c.FieldName, FieldName).Add(c => c.DataObject, dataObject));

            cut.InvokeAsync(dataObject.InitializeNewMaster);

            Assert.Equal(string.Empty, cut.Find(InputSelector).GetAttribute("value"));
        }

        [Theory]
        [InlineData(SingleFormMode.View, FormEditModes.All, true)]
        [InlineData(SingleFormMode.Edit, FormEditModes.All, false)]
        [InlineData(SingleFormMode.Add, FormEditModes.Edit, true)]
        [InlineData(SingleFormMode.Edit, FormEditModes.Edit, false)]
        [DisplayName("The scope's form mode and the field's AllowEditModes decide whether the editor is read-only")]
        public void Render_FormModeAndAllowEditModes_DecideReadOnly(SingleFormMode mode, FormEditModes allowed, bool readOnly)
        {
            var dataObject = BuildDataObject();
            var layoutField = new LayoutField { FieldName = FieldName, AllowEditModes = allowed };

            var cut = Render<FormScope>(p => p
                .Add(c => c.DataObject, dataObject)
                .Add(c => c.FormMode, mode)
                .AddChildContent<TextEdit>(e => e.Add(c => c.LayoutField, layoutField)));

            var input = cut.Find(InputSelector);
            Assert.Equal(readOnly, input.HasAttribute("readonly"));
            Assert.Equal(readOnly, input.ClassList.Contains("polhem-field-editor--readonly"));
        }

        [Fact]
        [DisplayName("A read-only layout field makes the editor read-only without any data object")]
        public void Render_ReadOnlyLayoutWithoutData_IsReadOnly()
        {
            var cut = Render<TextEdit>(p => p.Add(c => c.LayoutField, new LayoutField { FieldName = FieldName, ReadOnly = true }));

            Assert.True(cut.Find(InputSelector).HasAttribute("readonly"));
        }

        [Fact]
        [DisplayName("SetControlState overrides the scope's form mode")]
        public void SetControlState_View_MakesEditorReadOnly()
        {
            var dataObject = BuildDataObject();
            var cut = Render<TextEdit>(p => p.Add(c => c.FieldName, FieldName).Add(c => c.DataObject, dataObject));
            Assert.False(cut.Find(InputSelector).HasAttribute("readonly"));

            cut.InvokeAsync(() => cut.Instance.SetControlState(SingleFormMode.View));

            Assert.True(cut.Find(InputSelector).HasAttribute("readonly"));
        }

        [Fact]
        [DisplayName("The schema field's MaxLength becomes the input's maxlength")]
        public void Render_FormFieldMaxLength_SetsMaxLengthAttribute()
        {
            var dataObject = BuildDataObject(schema: BuildSchema(maxLength: 20));

            var cut = Render<TextEdit>(p => p.Add(c => c.FieldName, FieldName).Add(c => c.DataObject, dataObject));

            Assert.Equal("20", cut.Find(InputSelector).GetAttribute("maxlength"));
        }

        [Fact]
        [DisplayName("A row binding reads and writes the bound detail row")]
        public void Render_RowBinding_ReadsAndWritesRow()
        {
            var dataObject = BuildDataObject();
            var detail = dataObject.DataSet.Tables["Skill"]!;
            var row = detail.NewRow();
            row["skill_name"] = "Chess";
            detail.Rows.Add(row);

            var cut = Render<TextEdit>(p => p
                .Add(c => c.FieldName, "skill_name")
                .Add(c => c.Row, row)
                .Add(c => c.DataObject, dataObject));
            Assert.Equal("Chess", cut.Find(InputSelector).GetAttribute("value"));

            cut.Find(InputSelector).Change("Go");

            Assert.Equal("Go", dataObject.GetField(row, "skill_name"));
        }

        [Fact]
        [DisplayName("Disposing the editor releases its data object subscriptions")]
        public async Task Dispose_ReleasesSubscriptions()
        {
            var dataObject = BuildDataObject();
            Render<TextEdit>(p => p.Add(c => c.FieldName, FieldName).Add(c => c.DataObject, dataObject));
            Assert.NotNull(EventDelegate(dataObject, nameof(FormDataObject.FieldValueChanged)));

            await DisposeComponentsAsync();

            Assert.Null(EventDelegate(dataObject, nameof(FormDataObject.FieldValueChanged)));
            Assert.Null(EventDelegate(dataObject, nameof(FormDataObject.DataSetReplaced)));
        }

        private static Delegate? EventDelegate(FormDataObject dataObject, string eventName)
            => (Delegate?)typeof(FormDataObject)
                .GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(dataObject);
    }
}
