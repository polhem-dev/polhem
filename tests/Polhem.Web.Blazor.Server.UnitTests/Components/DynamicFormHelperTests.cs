using System.ComponentModel;
using System.Reflection;
using Polhem.Base.Data;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    public class DynamicFormHelperTests
    {
        private static readonly Type[] s_layoutFieldParam = [typeof(LayoutField)];
        private static readonly Type[] s_layoutSectionParam = [typeof(LayoutSection)];

        [Fact]
        [DisplayName("BuildGridStyle returns a one-column CSS grid style by default when Layout is null")]
        public void BuildGridStyle_NullLayout_DefaultsToOneColumn()
        {
            var component = new DynamicForm();
            var method = typeof(DynamicForm).GetMethod("BuildGridStyle",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var result = method!.Invoke(component, null) as string;
            Assert.Equal("display:grid;grid-template-columns:repeat(1,minmax(0,1fr));gap:8px", result);
        }

        [Fact]
        [DisplayName("BuildGridStyle returns a three-column CSS grid style when ColumnCount is 3")]
        public void BuildGridStyle_ThreeColumns_ReturnsThreeColumnStyle()
        {
            var component = new DynamicForm();
            typeof(DynamicForm)
                .GetProperty("Layout", BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(component, new FormLayout { ColumnCount = 3 });
            var method = typeof(DynamicForm).GetMethod("BuildGridStyle",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var result = method!.Invoke(component, null) as string;
            Assert.Equal("display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:8px", result);
        }

        [Fact]
        [DisplayName("BuildFieldStyle returns grid-row:span 1;grid-column:span 1 for the default span")]
        public void BuildFieldStyle_DefaultSpans_ReturnsSpanOneOne()
        {
            var field = new LayoutField();
            var method = typeof(DynamicForm).GetMethod("BuildFieldStyle",
                BindingFlags.NonPublic | BindingFlags.Static, null, s_layoutFieldParam, null);
            Assert.NotNull(method);
            var result = method!.Invoke(null, new object[] { field }) as string;
            Assert.Equal("grid-row:span 1;grid-column:span 1", result);
        }

        [Theory]
        [InlineData(2, 3, "grid-row:span 2;grid-column:span 3")]
        [InlineData(1, 4, "grid-row:span 1;grid-column:span 4")]
        [DisplayName("BuildFieldStyle returns the matching CSS grid style for a given span")]
        public void BuildFieldStyle_CustomSpans_ReturnsCorrectStyle(int rowSpan, int colSpan, string expected)
        {
            var field = new LayoutField { RowSpan = rowSpan, ColumnSpan = colSpan };
            var method = typeof(DynamicForm).GetMethod("BuildFieldStyle",
                BindingFlags.NonPublic | BindingFlags.Static, null, s_layoutFieldParam, null);
            Assert.NotNull(method);
            var result = method!.Invoke(null, new object[] { field }) as string;
            Assert.Equal(expected, result);
        }

        [Fact]
        [DisplayName("FieldInputId returns an HTML id combining IdPrefix and FieldName")]
        public void FieldInputId_WithPrefix_ReturnsPrefixedId()
        {
            var component = new DynamicForm();
            typeof(DynamicForm)
                .GetProperty("IdPrefix", BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(component, "frm");
            var field = new LayoutField { FieldName = "emp_name" };
            var method = typeof(DynamicForm).GetMethod("FieldInputId",
                BindingFlags.NonPublic | BindingFlags.Instance, null, s_layoutFieldParam, null);
            Assert.NotNull(method);
            var result = method!.Invoke(component, new object[] { field }) as string;
            Assert.Equal("frm-emp_name", result);
        }

        [Fact]
        [DisplayName("EnumerateSections returns an empty sequence when Layout is null")]
        public void EnumerateSections_NullLayout_ReturnsEmpty()
        {
            var component = new DynamicForm();
            var method = typeof(DynamicForm).GetMethod("EnumerateSections",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var result = method!.Invoke(component, null) as IEnumerable<LayoutSection>;
            Assert.NotNull(result);
            Assert.Empty(result!);
        }

        [Fact]
        [DisplayName("EnumerateSections returns the section when Layout contains one Section")]
        public void EnumerateSections_WithOneSection_ReturnsSingleSection()
        {
            var component = new DynamicForm();
            var layout = new FormLayout();
            layout.Sections!.Add(new LayoutSection { Name = "Main" });
            typeof(DynamicForm)
                .GetProperty("Layout", BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(component, layout);
            var method = typeof(DynamicForm).GetMethod("EnumerateSections",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var result = method!.Invoke(component, null) as IEnumerable<LayoutSection>;
            Assert.NotNull(result);
            Assert.Single(result!);
        }

        [Fact]
        [DisplayName("EnumerateFields returns only fields whose Visible is true")]
        public void EnumerateFields_MixedVisibility_ReturnsOnlyVisibleFields()
        {
            var section = new LayoutSection();
            section.Fields!.Add(new LayoutField { FieldName = "visible_field", Visible = true });
            section.Fields.Add(new LayoutField { FieldName = "hidden_field", Visible = false });
            var method = typeof(DynamicForm).GetMethod("EnumerateFields",
                BindingFlags.NonPublic | BindingFlags.Static, null, s_layoutSectionParam, null);
            Assert.NotNull(method);
            var result = method!.Invoke(null, new object[] { section }) as IEnumerable<LayoutField>;
            Assert.NotNull(result);
            var fields = result!.ToList();
            Assert.Single(fields);
            Assert.Equal("visible_field", fields[0].FieldName);
        }

        [Fact]
        [DisplayName("EnumerateOptions returns an empty collection when DataObject is null")]
        public void EnumerateOptions_NullDataObject_ReturnsEmpty()
        {
            var component = new DynamicForm();
            var field = new LayoutField { FieldName = "status" };
            var method = typeof(DynamicForm).GetMethod("EnumerateOptions",
                BindingFlags.NonPublic | BindingFlags.Instance, null, s_layoutFieldParam, null);
            Assert.NotNull(method);
            var result = method!.Invoke(component, new object[] { field }) as IEnumerable<ListItem>;
            Assert.NotNull(result);
            Assert.Empty(result!);
        }

        [Fact]
        [DisplayName("BuildGridStyle corrects a ColumnCount of 0 to one column and returns the matching CSS grid style")]
        public void BuildGridStyle_ZeroColumnCount_DefaultsToOneColumn()
        {
            var component = new DynamicForm();
            typeof(DynamicForm)
                .GetProperty("Layout", BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(component, new FormLayout { ColumnCount = 0 });
            var method = typeof(DynamicForm).GetMethod("BuildGridStyle",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var result = method!.Invoke(component, null) as string;
            Assert.Equal("display:grid;grid-template-columns:repeat(1,minmax(0,1fr));gap:8px", result);
        }

        [Fact]
        [DisplayName("EnumerateOptions returns an empty collection when DataObject has fields but not the named one")]
        public void EnumerateOptions_DataObjectFieldNotFound_ReturnsEmpty()
        {
            var schema = new FormSchema("Employee", "Employee");
            schema.Tables!.Add("Employee", "Employee");
            var dataObject = new FormDataObject(schema);

            var component = new DynamicForm();
            typeof(DynamicForm)
                .GetProperty("DataObject", BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(component, dataObject);

            var field = new LayoutField { FieldName = "nonexistent_field" };
            var method = typeof(DynamicForm).GetMethod("EnumerateOptions",
                BindingFlags.NonPublic | BindingFlags.Instance, null, s_layoutFieldParam, null);
            Assert.NotNull(method);
            var result = method!.Invoke(component, new object[] { field }) as IEnumerable<ListItem>;
            Assert.NotNull(result);
            Assert.Empty(result!);
        }

        [Fact]
        [DisplayName("EnumerateOptions returns the matching option list when the field has ListItems")]
        public void EnumerateOptions_DataObjectFieldWithListItems_ReturnsItems()
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            var schemaField = master.Fields!.Add("status", "Status", FieldDbType.String);
            schemaField.ListItems!.Add("A", "Active");
            schemaField.ListItems.Add("I", "Inactive");

            var dataObject = new FormDataObject(schema);

            var component = new DynamicForm();
            typeof(DynamicForm)
                .GetProperty("DataObject", BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(component, dataObject);

            var layoutField = new LayoutField { FieldName = "status" };
            var method = typeof(DynamicForm).GetMethod("EnumerateOptions",
                BindingFlags.NonPublic | BindingFlags.Instance, null, s_layoutFieldParam, null);
            Assert.NotNull(method);
            var result = method!.Invoke(component, new object[] { layoutField }) as IEnumerable<ListItem>;
            Assert.NotNull(result);
            Assert.Equal(2, result!.Count());
        }
    }
}
