using System.ComponentModel;
using System.Reflection;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Server.Controls;

namespace Polhem.Web.Blazor.Server.UnitTests.Controls
{
    public class FormLayoutPresenterHelperTests
    {
        private static readonly Type[] s_layoutFieldParam = [typeof(LayoutField)];
        private static readonly Type[] s_fieldStyleParams = [typeof(LayoutField), typeof(int)];
        private static readonly Type[] s_layoutSectionParam = [typeof(LayoutSection)];

        [Fact]
        [DisplayName("BuildGridStyle returns a one-column CSS grid style by default when Layout is null")]
        public void BuildGridStyle_NullLayout_DefaultsToOneColumn()
        {
            var component = new FormLayoutPresenter();
            var method = typeof(FormLayoutPresenter).GetMethod("BuildGridStyle",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var result = method!.Invoke(component, null) as string;
            Assert.Equal("display:grid;grid-template-columns:repeat(1,minmax(0,1fr));gap:8px", result);
        }

        [Fact]
        [DisplayName("BuildGridStyle returns a three-column CSS grid style when ColumnCount is 3")]
        public void BuildGridStyle_ThreeColumns_ReturnsThreeColumnStyle()
        {
            var component = new FormLayoutPresenter();
            typeof(FormLayoutPresenter)
                .GetProperty("Layout", BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(component, new FormLayout { ColumnCount = 3 });
            var method = typeof(FormLayoutPresenter).GetMethod("BuildGridStyle",
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
            var result = InvokeBuildFieldStyle(field, columnCount: 4);
            Assert.Equal("grid-row:span 1;grid-column:span 1", result);
        }

        [Theory]
        [InlineData(2, 3, "grid-row:span 2;grid-column:span 3")]
        [InlineData(1, 4, "grid-row:span 1;grid-column:span 4")]
        [DisplayName("BuildFieldStyle returns the matching CSS grid style for a given span")]
        public void BuildFieldStyle_CustomSpans_ReturnsCorrectStyle(int rowSpan, int colSpan, string expected)
        {
            var field = new LayoutField { RowSpan = rowSpan, ColumnSpan = colSpan };
            var result = InvokeBuildFieldStyle(field, columnCount: 4);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(3, 2, "grid-row:span 1;grid-column:span 2")]
        [InlineData(2, 1, "grid-row:span 1;grid-column:span 1")]
        [DisplayName("BuildFieldStyle clamps a column span wider than the grid, so CSS adds no implicit column")]
        public void BuildFieldStyle_SpanWiderThanGrid_ClampsToColumnCount(int colSpan, int columnCount, string expected)
        {
            var field = new LayoutField { ColumnSpan = colSpan };
            var result = InvokeBuildFieldStyle(field, columnCount);
            Assert.Equal(expected, result);
        }

        private static string? InvokeBuildFieldStyle(LayoutField field, int columnCount)
        {
            var method = typeof(FormLayoutPresenter).GetMethod("BuildFieldStyle",
                BindingFlags.NonPublic | BindingFlags.Static, null, s_fieldStyleParams, null);
            Assert.NotNull(method);
            return method!.Invoke(null, new object[] { field, columnCount }) as string;
        }

        [Fact]
        [DisplayName("FieldInputId returns an HTML id combining IdPrefix and FieldName")]
        public void FieldInputId_WithPrefix_ReturnsPrefixedId()
        {
            var component = new FormLayoutPresenter();
            typeof(FormLayoutPresenter)
                .GetProperty("IdPrefix", BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(component, "frm");
            var field = new LayoutField { FieldName = "emp_name" };
            var method = typeof(FormLayoutPresenter).GetMethod("FieldInputId",
                BindingFlags.NonPublic | BindingFlags.Instance, null, s_layoutFieldParam, null);
            Assert.NotNull(method);
            var result = method!.Invoke(component, new object[] { field }) as string;
            Assert.Equal("frm-emp_name", result);
        }

        [Fact]
        [DisplayName("EnumerateSections returns an empty sequence when Layout is null")]
        public void EnumerateSections_NullLayout_ReturnsEmpty()
        {
            var component = new FormLayoutPresenter();
            var method = typeof(FormLayoutPresenter).GetMethod("EnumerateSections",
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
            var component = new FormLayoutPresenter();
            var layout = new FormLayout();
            layout.Sections!.Add(new LayoutSection { Name = "Main" });
            typeof(FormLayoutPresenter)
                .GetProperty("Layout", BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(component, layout);
            var method = typeof(FormLayoutPresenter).GetMethod("EnumerateSections",
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
            var method = typeof(FormLayoutPresenter).GetMethod("EnumerateFields",
                BindingFlags.NonPublic | BindingFlags.Static, null, s_layoutSectionParam, null);
            Assert.NotNull(method);
            var result = method!.Invoke(null, new object[] { section }) as IEnumerable<LayoutField>;
            Assert.NotNull(result);
            var fields = result!.ToList();
            Assert.Single(fields);
            Assert.Equal("visible_field", fields[0].FieldName);
        }

        [Fact]
        [DisplayName("BuildGridStyle corrects a ColumnCount of 0 to one column and returns the matching CSS grid style")]
        public void BuildGridStyle_ZeroColumnCount_DefaultsToOneColumn()
        {
            var component = new FormLayoutPresenter();
            typeof(FormLayoutPresenter)
                .GetProperty("Layout", BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(component, new FormLayout { ColumnCount = 0 });
            var method = typeof(FormLayoutPresenter).GetMethod("BuildGridStyle",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var result = method!.Invoke(component, null) as string;
            Assert.Equal("display:grid;grid-template-columns:repeat(1,minmax(0,1fr));gap:8px", result);
        }

    }
}
