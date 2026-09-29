using System.ComponentModel;
using System.Reflection;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Server.DataObjects;
using Microsoft.AspNetCore.Components;
using Polhem.Definition.Layouts;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// Structural checks for <see cref="DynamicForm"/>: the public component surface, and that wiring through
    /// <see cref="FormLayoutGenerator.Generate"/> produces a layout the component is willing to consume. Rendering is
    /// covered with bUnit by <see cref="DynamicFormRenderTests"/>, <see cref="DynamicFormControlTypeRenderTests"/> and
    /// <see cref="DynamicFormTimeEditTests"/>.
    /// </summary>
    public class DynamicFormTests
    {
        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add("emp_id", "ID", FieldDbType.String);
            master.Fields.Add("is_active", "Active", FieldDbType.Boolean);
            master.Fields.Add("hire_date", "Hire Date", FieldDbType.Date);
            return schema;
        }

        private static PropertyInfo GetProperty(string name)
        {
            var property = typeof(DynamicForm).GetProperty(
                name, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(property);
            return property!;
        }

        [Fact]
        [DisplayName("DynamicForm is a subclass of Blazor ComponentBase")]
        public void Type_IsComponentBaseSubclass()
        {
            Assert.True(typeof(ComponentBase).IsAssignableFrom(typeof(DynamicForm)));
        }

        [Theory]
        [InlineData(nameof(DynamicForm.Layout))]
        [InlineData(nameof(DynamicForm.DataObject))]
        [InlineData(nameof(DynamicForm.IdPrefix))]
        [DisplayName("Public properties are all marked with [Parameter]")]
        public void PublicProperties_AreMarkedAsParameters(string name)
        {
            var property = GetProperty(name);
            Assert.NotNull(property.GetCustomAttribute<ParameterAttribute>());
        }

        [Fact]
        [DisplayName("DynamicForm can be instantiated and its Layout/DataObject parameters assigned through reflection")]
        public void CanInstantiateAndAssignParameters()
        {
            var schema = BuildSchema();
            var layout = FormLayoutGenerator.Generate(schema, "default");
            var dataObject = new FormDataObject(schema);

            var component = new DynamicForm();

            // Use reflection to bypass BL0005 — production callers assign parameters
            // through the Blazor renderer (SetParametersAsync), which is hard to drive
            // outside bUnit; this is a structural smoke check only.
            GetProperty(nameof(DynamicForm.Layout)).SetValue(component, layout);
            GetProperty(nameof(DynamicForm.DataObject)).SetValue(component, dataObject);
            GetProperty(nameof(DynamicForm.IdPrefix)).SetValue(component, "test-prefix");

            Assert.Same(layout, component.Layout);
            Assert.Same(dataObject, component.DataObject);
            Assert.Equal("test-prefix", component.IdPrefix);
        }

        [Fact]
        [DisplayName("The FormLayout produced by FormLayoutGenerator contains at least one Section that DynamicForm can consume")]
        public void GeneratedLayout_HasAtLeastOneSection()
        {
            var schema = BuildSchema();
            var layout = FormLayoutGenerator.Generate(schema, "default");

            Assert.NotNull(layout.Sections);
            Assert.NotEmpty(layout.Sections!);
            Assert.All(layout.Sections!, section =>
            {
                Assert.NotNull(section.Fields);
                Assert.NotEmpty(section.Fields!);
            });
        }
    }
}
