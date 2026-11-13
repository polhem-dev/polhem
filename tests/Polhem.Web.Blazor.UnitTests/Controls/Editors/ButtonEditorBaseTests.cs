using System.ComponentModel;
using System.Data;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Core.DataObjects;
using Polhem.Web.Blazor.Controls.Editors;

namespace Polhem.Web.Blazor.UnitTests.Controls.Editors
{
    /// <summary>
    /// The lookup flow of <see cref="ButtonEditorBase"/> without any UI framework: the display text, the button, the
    /// clear keys, the selection and how a failure is reported.
    /// </summary>
    public class ButtonEditorBaseTests : BunitContext
    {
        private const string LookupField = "dept_rowid";
        private static readonly Guid s_deptId = new("11111111-1111-1111-1111-111111111111");

        private static FormDataObject BuildEmployee(bool withMappings = true)
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            var dept = master.Fields!.Add(LookupField, "Department", FieldDbType.Guid);
            dept.RelationProgId = "Dept";
            if (withMappings)
            {
                dept.RelationFieldMappings!.Add("sys_id", "ref_dept_id");
                dept.RelationFieldMappings.Add("sys_name", "ref_dept_name");
            }
            master.Fields.Add("ref_dept_id", "Department Code", FieldDbType.String);
            master.Fields.Add("ref_dept_name", "Department Name", FieldDbType.String);
            var detail = schema.Tables.Add("Line", "Line");
            var lineDept = detail.Fields!.Add(LookupField, "Department", FieldDbType.Guid);
            lineDept.RelationProgId = "Dept";
            lineDept.RelationFieldMappings!.Add("sys_name", "ref_dept_name");
            detail.Fields.Add("ref_dept_name", "Department Name", FieldDbType.String);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        private static DataRow BuildTargetRow(bool withName = true)
        {
            var table = new DataTable("Dept");
            table.Columns.Add("sys_rowid", typeof(Guid));
            table.Columns.Add("sys_id", typeof(string));
            if (withName) table.Columns.Add("sys_name", typeof(string));
            var row = table.NewRow();
            row["sys_rowid"] = s_deptId;
            row["sys_id"] = "D01";
            if (withName) row["sys_name"] = "Engineering";
            table.Rows.Add(row);
            return row;
        }

        private IRenderedComponent<ProbeButtonEditor> RenderEditor(FormDataObject? dataObject, string fieldName = LookupField,
            Action<ComponentParameterCollectionBuilder<ProbeButtonEditor>>? configure = null)
            => Render<ProbeButtonEditor>(p =>
            {
                p.Add(c => c.FieldName, fieldName);
                if (dataObject is not null) p.Add(c => c.DataObject, dataObject);
                configure?.Invoke(p);
            });

        [Fact]
        [DisplayName("A lookup field without display fields shows an empty text instead of the row id")]
        public void Refresh_NoDisplayFields_ShowsEmpty()
        {
            var dataObject = BuildEmployee(withMappings: false);
            dataObject.SetField(LookupField, s_deptId.ToString());

            var cut = RenderEditor(dataObject);

            Assert.True(cut.Instance.HasLookup);
            Assert.Equal(string.Empty, cut.Find("input").GetAttribute("value"));
        }

        [Fact]
        [DisplayName("A row-bound lookup shows the display fields of its detail row")]
        public void Refresh_RowBinding_ShowsTheRowsDisplayFields()
        {
            var dataObject = BuildEmployee();
            var lines = dataObject.DataSet.Tables["Line"]!;
            var line = lines.NewRow();
            line["ref_dept_name"] = "Sales";
            lines.Rows.Add(line);

            var cut = RenderEditor(dataObject, configure: p => p.Add(c => c.Row, line));

            Assert.Contains("Sales", cut.Find("input").GetAttribute("value"), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The button of a lookup editor opens the lookup")]
        public void Button_Lookup_OpensTheLookup()
        {
            var cut = RenderEditor(BuildEmployee());

            cut.Find("button.open").Click();

            Assert.Equal("true", cut.Find("input").GetAttribute("data-open"));
        }

        [Fact]
        [DisplayName("The button of a read-only editor does nothing")]
        public void Button_ReadOnly_DoesNothing()
        {
            var clicks = 0;
            var cut = RenderEditor(BuildEmployee(), "ref_dept_name", p => p.Add(c => c.ButtonClick, () => clicks++));
            cut.InvokeAsync(() => cut.Instance.SetControlState(SingleFormMode.View));

            cut.Find("button.open").Click();

            Assert.Equal(0, clicks);
            Assert.Equal("false", cut.Find("input").GetAttribute("data-open"));
        }

        [Fact]
        [DisplayName("A plain button editor commits the typed text")]
        public void Commit_WithoutLookup_WritesTheText()
        {
            var dataObject = BuildEmployee();
            var cut = RenderEditor(dataObject, "ref_dept_name");

            cut.Find("input").Change("Typed");

            Assert.Equal("Typed", dataObject.GetField("ref_dept_name"));
        }

        [Theory]
        [InlineData("Delete")]
        [InlineData("Backspace")]
        [DisplayName("Delete or Backspace clears the lookup selection")]
        public void KeyDown_ClearKey_ClearsTheSelection(string key)
        {
            var dataObject = BuildEmployee();
            dataObject.SetField(LookupField, s_deptId.ToString());
            dataObject.SetField("ref_dept_name", "Engineering");
            var cut = RenderEditor(dataObject);

            cut.Find("input").KeyDown(new KeyboardEventArgs { Key = key });

            Assert.Equal(Guid.Empty.ToString(), dataObject.GetField(LookupField));
            Assert.Equal(string.Empty, dataObject.GetField("ref_dept_name"));
        }

        [Fact]
        [DisplayName("Another key leaves the lookup selection")]
        public void KeyDown_OtherKey_KeepsTheSelection()
        {
            var dataObject = BuildEmployee();
            dataObject.SetField(LookupField, s_deptId.ToString());
            var cut = RenderEditor(dataObject);

            cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "a" });

            Assert.Equal(s_deptId.ToString(), dataObject.GetField(LookupField), ignoreCase: true);
        }

        [Fact]
        [DisplayName("A clear key on a read-only lookup keeps the selection")]
        public void KeyDown_ReadOnly_KeepsTheSelection()
        {
            var dataObject = BuildEmployee();
            dataObject.SetField(LookupField, s_deptId.ToString());
            var cut = RenderEditor(dataObject);
            cut.InvokeAsync(() => cut.Instance.SetControlState(SingleFormMode.View));

            cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "Delete" });

            Assert.Equal(s_deptId.ToString(), dataObject.GetField(LookupField), ignoreCase: true);
        }

        [Fact]
        [DisplayName("A selection writes the row id and the mapped fields and closes the lookup")]
        public void Select_WritesTheSelection()
        {
            var dataObject = BuildEmployee();
            var cut = RenderEditor(dataObject);
            cut.Find("button.open").Click();
            cut.Instance.Selected = BuildTargetRow();

            cut.Find("button.select").Click();

            Assert.Equal(s_deptId.ToString(), dataObject.GetField(LookupField), ignoreCase: true);
            Assert.Equal("Engineering", dataObject.GetField("ref_dept_name"));
            Assert.Equal("false", cut.Find("input").GetAttribute("data-open"));
        }

        [Fact]
        [DisplayName("A selection on an unbound editor only closes the lookup")]
        public void Select_Unbound_OnlyCloses()
        {
            var cut = RenderEditor(null);
            cut.Instance.Selected = BuildTargetRow();

            cut.Find("button.select").Click();

            Assert.Equal("false", cut.Find("input").GetAttribute("data-open"));
            Assert.Equal(string.Empty, cut.Find("input").GetAttribute("data-failure"));
        }

        [Fact]
        [DisplayName("A selection missing a mapped field keeps the failure for the editor when nobody handles it")]
        public void Select_MappingOutsideResult_KeepsTheFailure()
        {
            var cut = RenderEditor(BuildEmployee());
            cut.Find("button.open").Click();
            cut.Instance.Selected = BuildTargetRow(withName: false);

            cut.Find("button.select").Click();

            Assert.Contains("sys_name", cut.Find("input").GetAttribute("data-failure"), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A selection missing a mapped field reaches the LookupFailed handler")]
        public void Select_MappingOutsideResult_RaisesLookupFailed()
        {
            Exception? reported = null;
            var cut = RenderEditor(BuildEmployee(), configure: p => p.Add(c => c.LookupFailed, ex => reported = ex));
            cut.Find("button.open").Click();
            cut.Instance.Selected = BuildTargetRow(withName: false);

            cut.Find("button.select").Click();

            Assert.IsType<InvalidOperationException>(reported);
            Assert.Equal(string.Empty, cut.Find("input").GetAttribute("data-failure"));
            Assert.Equal("false", cut.Find("input").GetAttribute("data-open"));
        }

        [Fact]
        [DisplayName("Closing the lookup writes nothing")]
        public void Close_WritesNothing()
        {
            var dataObject = BuildEmployee();
            var cut = RenderEditor(dataObject);
            cut.Find("button.open").Click();

            cut.Find("button.close").Click();

            Assert.Equal("false", cut.Find("input").GetAttribute("data-open"));
            Assert.Equal(Guid.Empty.ToString(), dataObject.GetField(LookupField));
        }

        // The lookup flow every button editor shares, with its state made visible and its handlers on plain buttons.
        private sealed class ProbeButtonEditor : ButtonEditorBase
        {
            public DataRow? Selected { get; set; }

            protected override void BuildRenderTree(RenderTreeBuilder builder)
            {
                builder.OpenElement(0, "input");
                builder.AddAttribute(1, "value", Text);
                builder.AddAttribute(2, "data-open", IsLookupOpen ? "true" : "false");
                builder.AddAttribute(3, "data-failure", LookupFailure ?? string.Empty);
                builder.AddAttribute(4, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, e => CommitText(e.Value?.ToString())));
                builder.AddAttribute(5, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, OnKeyDown));
                builder.CloseElement();
                AddButton(builder, 10, "open", () => OnButtonClickAsync());
                AddButton(builder, 20, "select", () => ApplySelectionAsync(Selected!));
                AddButton(builder, 30, "close", () =>
                {
                    CloseLookup();
                    return Task.CompletedTask;
                });
            }

            private void AddButton(RenderTreeBuilder builder, int sequence, string name, Func<Task> handler)
            {
                builder.OpenElement(sequence, "button");
                builder.AddAttribute(sequence + 1, "class", name);
                builder.AddAttribute(sequence + 2, "onclick", EventCallback.Factory.Create(this, handler));
                builder.CloseElement();
            }
        }
    }
}
