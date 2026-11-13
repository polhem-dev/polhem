using System.ComponentModel;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Core.DataObjects;
using Polhem.Web.Blazor.Controls.Editors;

namespace Polhem.Web.Blazor.UnitTests.Controls.Editors
{
    /// <summary>
    /// The explicit binds, <see cref="FieldEditorBase.FieldValue"/> and the layout's read-only flag of
    /// <see cref="FieldEditorBase"/>, exercised through a minimal <see cref="TextEditorBase"/>.
    /// </summary>
    public class FieldEditorBaseBindingTests : BunitContext
    {
        private const string FieldName = "emp_name";
        private const string InputSelector = "input";

        private static FormDataObject BuildDataObject(string value = "Ada")
        {
            var schema = new FormSchema("Employee", "Employee");
            schema.Tables!.Add("Employee", "Employee").Fields!.Add(FieldName, "Name", FieldDbType.String);
            schema.Tables.Add("Skill", "Skill").Fields!.Add("skill_name", "Skill", FieldDbType.String);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            dataObject.SetField(FieldName, value);
            return dataObject;
        }

        [Fact]
        [DisplayName("FieldValue of an unbound editor is null")]
        public void FieldValue_Unbound_IsNull()
        {
            var cut = Render<ProbeEditor>();

            Assert.Null(cut.Instance.FieldValue);
        }

        [Fact]
        [DisplayName("Setting FieldValue writes the data object and shows the new value")]
        public void FieldValue_Set_WritesAndRefreshes()
        {
            var dataObject = BuildDataObject();
            var cut = Render<ProbeEditor>(p => p.Add(c => c.FieldName, FieldName).Add(c => c.DataObject, dataObject));

            cut.InvokeAsync(() => cut.Instance.FieldValue = "Grace");
            cut.Render();

            Assert.Equal("Grace", dataObject.GetField(FieldName));
            Assert.Equal("Grace", cut.Instance.FieldValue);
            Assert.Equal("Grace", cut.Find(InputSelector).GetAttribute("value"));
        }

        [Fact]
        [DisplayName("A commit on an unbound editor writes nothing and keeps the editor empty")]
        public void Commit_Unbound_WritesNothing()
        {
            var cut = Render<ProbeEditor>();

            cut.Find(InputSelector).Change("Grace");

            Assert.Equal(string.Empty, cut.Find(InputSelector).GetAttribute("value"));
        }

        [Fact]
        [DisplayName("Bind with a layout field binds its field and follows its read-only flag")]
        public void Bind_LayoutField_FollowsItsReadOnlyFlag()
        {
            var dataObject = BuildDataObject();
            var cut = Render<ProbeEditor>();

            cut.InvokeAsync(() => cut.Instance.Bind(dataObject, new LayoutField { FieldName = FieldName, ReadOnly = true }));

            Assert.Equal("Ada", cut.Find(InputSelector).GetAttribute("value"));
            Assert.Equal("true", cut.Find(InputSelector).GetAttribute("data-layout-readonly"));
            Assert.True(cut.Find(InputSelector).HasAttribute("readonly"));
        }

        [Fact]
        [DisplayName("Bind by field name keeps the LayoutField parameter for the editor's state")]
        public void Bind_FieldName_KeepsTheLayoutFieldParameter()
        {
            var dataObject = BuildDataObject();
            var cut = Render<ProbeEditor>(p => p.Add(c => c.LayoutField, new LayoutField { FieldName = "other", ReadOnly = true }));

            cut.InvokeAsync(() => cut.Instance.Bind(dataObject, FieldName));

            Assert.Equal("Ada", cut.Find(InputSelector).GetAttribute("value"));
            Assert.Equal("true", cut.Find(InputSelector).GetAttribute("data-layout-readonly"));
        }

        [Fact]
        [DisplayName("Bind with a row reads and writes that detail row")]
        public void Bind_Row_ReadsAndWritesTheRow()
        {
            var dataObject = BuildDataObject();
            var detail = dataObject.DataSet.Tables["Skill"]!;
            var row = detail.NewRow();
            row["skill_name"] = "Chess";
            detail.Rows.Add(row);
            var cut = Render<ProbeEditor>();

            cut.InvokeAsync(() => cut.Instance.Bind(dataObject, new LayoutColumn { FieldName = "skill_name" }, row));
            Assert.Equal("Chess", cut.Find(InputSelector).GetAttribute("value"));
            Assert.Equal("false", cut.Find(InputSelector).GetAttribute("data-layout-readonly"));

            cut.Find(InputSelector).Change("Go");

            Assert.Equal("Go", dataObject.GetField(row, "skill_name"));
        }

        [Fact]
        [DisplayName("Bind rejects a missing data object, field or row")]
        public void Bind_MissingArgument_Throws()
        {
            var dataObject = BuildDataObject();
            var cut = Render<ProbeEditor>();
            var editor = cut.Instance;

            Assert.Throws<ArgumentNullException>(() => editor.Bind(null!, new LayoutField { FieldName = FieldName }));
            Assert.Throws<ArgumentNullException>(() => editor.Bind(dataObject, (LayoutField)null!));
            Assert.Throws<ArgumentException>(() => editor.Bind(dataObject, " "));
            Assert.Throws<ArgumentNullException>(() => editor.Bind(dataObject, new LayoutColumn { FieldName = "skill_name" }, null!));
        }

        [Fact]
        [DisplayName("Disposing an editor twice is harmless")]
        public void Dispose_Twice_IsHarmless()
        {
            var dataObject = BuildDataObject();
            var cut = Render<ProbeEditor>(p => p.Add(c => c.FieldName, FieldName).Add(c => c.DataObject, dataObject));

            cut.Instance.Dispose();
            var exception = Record.Exception(cut.Instance.Dispose);

            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("A disposed editor no longer follows the data object")]
        public void Dispose_StopsFollowingTheDataObject()
        {
            var dataObject = BuildDataObject();
            var cut = Render<ProbeEditor>(p => p.Add(c => c.FieldName, FieldName).Add(c => c.DataObject, dataObject));

            cut.Instance.Dispose();
            cut.InvokeAsync(() => dataObject.SetField(FieldName, "Linus"));

            Assert.Equal("Ada", cut.Find(InputSelector).GetAttribute("value"));
        }

        // The part every text editor shares, with the layout's read-only flag made visible.
        private sealed class ProbeEditor : TextEditorBase
        {
            protected override void BuildRenderTree(RenderTreeBuilder builder)
            {
                builder.OpenElement(0, "input");
                builder.AddAttribute(1, "value", Text);
                builder.AddAttribute(2, "readonly", !IsEditable);
                builder.AddAttribute(3, "data-layout-readonly", IsLayoutReadOnly ? "true" : "false");
                builder.AddAttribute(4, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, e => CommitText(e.Value?.ToString())));
                builder.CloseElement();
            }
        }
    }
}
