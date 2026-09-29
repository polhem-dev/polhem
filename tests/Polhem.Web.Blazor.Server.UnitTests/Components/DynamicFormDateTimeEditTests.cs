using System.ComponentModel;
using System.Globalization;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Tests.Shared;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Server.DataObjects;
using Bunit;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// The DynamicForm date-and-time input (<see cref="ControlType.DateTimeEdit"/>) keeps the time of an
    /// instant, which a date input cannot show at all.
    /// </summary>
    public class DynamicFormDateTimeEditTests : BunitContext
    {
        private const string FieldName = "occurred_at";

        private static FormSchema BuildSchema(bool readOnly = false)
        {
            var schema = new FormSchema("Event", "Event");
            var field = schema.Tables!.Add("Event", "Event").Fields!.Add(FieldName, "Occurred At", FieldDbType.DateTime);
            field.ReadOnly = readOnly;
            return schema;
        }

        private static FormDataObject BuildDataObject(FormSchema schema, DateTime value)
        {
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            dataObject.MasterRow![FieldName] = value;
            return dataObject;
        }

        private IRenderedComponent<DynamicForm> RenderForm(FormSchema schema, FormDataObject dataObject)
            => Render<DynamicForm>(p => p
                .Add(c => c.Layout, FormLayoutGenerator.Generate(schema, "default"))
                .Add(c => c.DataObject, dataObject));

        [Fact]
        [DisplayName("A DateTime field renders a datetime-local input carrying the time, also at midnight")]
        public void Render_EditableField_RendersDateTimeLocalWithTime()
        {
            var schema = BuildSchema();
            var dataObject = BuildDataObject(schema, new DateTime(2026, 9, 28, 0, 0, 0));

            var cut = RenderForm(schema, dataObject);

            var input = cut.Find("input[type='datetime-local']");
            Assert.Equal("2026-09-28T00:00:00", input.GetAttribute("value"));
        }

        [Fact]
        [DisplayName("A change writes the picked date and time to the field")]
        public void Change_WritesDateAndTime()
        {
            var schema = BuildSchema();
            var dataObject = BuildDataObject(schema, new DateTime(2026, 9, 28, 14, 30, 0));
            var cut = RenderForm(schema, dataObject);

            cut.Find("input[type='datetime-local']").Change("2026-10-01T08:05");

            Assert.Equal(new DateTime(2026, 10, 1, 8, 5, 0), (DateTime)dataObject.MasterRow![FieldName]);
        }

        [Fact]
        [DisplayName("Clearing the input unsets the field")]
        public void Change_Empty_UnsetsField()
        {
            var schema = BuildSchema();
            var dataObject = BuildDataObject(schema, new DateTime(2026, 9, 28, 14, 30, 0));
            var cut = RenderForm(schema, dataObject);

            cut.Find("input[type='datetime-local']").Change(string.Empty);

            Assert.Equal(string.Empty, dataObject.GetField(FieldName));
        }

        [Fact]
        [DisplayName("A read-only DateTime field shows the date and time in the circuit's culture")]
        public void Render_ReadOnlyField_ShowsCultureText()
        {
            using var culture = new CultureScope("de-DE");
            var value = new DateTime(2026, 9, 28, 14, 30, 15);
            var schema = BuildSchema(readOnly: true);
            var dataObject = BuildDataObject(schema, value);

            var cut = RenderForm(schema, dataObject);

            Assert.Empty(cut.FindAll("input[type='datetime-local']"));
            var input = cut.Find("input.polhem-dynamic-form__input--datetime");
            Assert.Equal(value.ToString("G", CultureInfo.CurrentCulture), input.GetAttribute("value"));
            Assert.True(input.HasAttribute("readonly"));
        }

        [Fact]
        [DisplayName("A date input over a value with a time shows its date instead of rendering blank")]
        public void Render_DateEditOverDateTimeValue_ShowsDatePart()
        {
            var schema = BuildSchema();
            var dataObject = BuildDataObject(schema, new DateTime(2026, 9, 28, 14, 30, 0));
            var layout = FormLayoutGenerator.Generate(schema, "default");
            layout.Sections![0].Fields![0].ControlType = ControlType.DateEdit;

            var cut = Render<DynamicForm>(p => p
                .Add(c => c.Layout, layout)
                .Add(c => c.DataObject, dataObject));

            Assert.Equal("2026-09-28", cut.Find("input[type='date']").GetAttribute("value"));
        }
    }
}
