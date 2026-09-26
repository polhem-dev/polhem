using System.ComponentModel;
using System.Reflection;
using AngleSharp.Dom;
using Polhem.Base.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Server.DataObjects;
using Bunit;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// Behavior tests for the DynamicForm time input (<see cref="ControlType.TimeEdit"/>), aligned with Avalonia's
    /// <c>TimeEdit</c>: loose input is normalized to fixed-width HH:mm, an empty input means unset, and unparsable input keeps the last valid value.
    /// </summary>
    public class DynamicFormTimeEditTests : BunitContext
    {
        private const string FieldName = "work_start";
        private const string TimeInputSelector = "input.polhem-dynamic-form__input--time";

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Shift", "Shift");
            schema.Tables!.Add("Shift", "Shift").Fields!.Add(FieldName, "Start", FieldDbType.Time);
            return schema;
        }

        private IRenderedComponent<DynamicForm> RenderForm(FormSchema schema, FormDataObject dataObject)
            => Render<DynamicForm>(p => p
                .Add(c => c.Layout, FormLayoutGenerator.Generate(schema, "default"))
                .Add(c => c.DataObject, dataObject));

        private static FormDataObject BuildDataObject(FormSchema schema)
        {
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        private static IElement TimeInput(IRenderedComponent<DynamicForm> cut) => cut.Find(TimeInputSelector);

        [Fact]
        [DisplayName("The time input renders as fixed-width HH:mm")]
        public void Render_DisplaysFixedWidthForm()
        {
            var schema = BuildSchema();
            var dataObject = BuildDataObject(schema);
            // Assigned to the row directly: `SetField` already normalises, which would let this pass
            // without the component doing any normalising of its own.
            dataObject.MasterRow![FieldName] = "8:30";

            var cut = RenderForm(schema, dataObject);

            Assert.Equal("08:30", TimeInput(cut).GetAttribute("value"));
        }

        [Fact]
        [DisplayName("A change normalizes loose input to fixed-width HH:mm")]
        public void Change_NormalizesLooseInput()
        {
            var schema = BuildSchema();
            var dataObject = BuildDataObject(schema);
            var cut = RenderForm(schema, dataObject);

            TimeInput(cut).Change("8:30");

            Assert.Equal("08:30", dataObject.GetField(FieldName));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("Clearing the input writes back an empty string (unset), not 00:00")]
        public void Change_EmptyText_WritesUnset(string empty)
        {
            var schema = BuildSchema();
            var dataObject = BuildDataObject(schema);
            dataObject.SetField(FieldName, "08:30");
            var cut = RenderForm(schema, dataObject);

            TimeInput(cut).Change(empty);

            Assert.Equal(string.Empty, dataObject.GetField(FieldName));
        }

        [Fact]
        [DisplayName("00:00 is a valid time and is written back normally")]
        public void Change_Midnight_IsStored()
        {
            var schema = BuildSchema();
            var dataObject = BuildDataObject(schema);
            var cut = RenderForm(schema, dataObject);

            TimeInput(cut).Change("00:00");

            Assert.Equal("00:00", dataObject.GetField(FieldName));
        }

        [Theory]
        [InlineData("25:00")]
        [InlineData("08:99")]
        [InlineData("abc")]
        [DisplayName("Unparsable input keeps the last valid value instead of clearing the field")]
        public void Change_InvalidText_KeepsLastValidValue(string invalid)
        {
            var schema = BuildSchema();
            var dataObject = BuildDataObject(schema);
            dataObject.SetField(FieldName, "08:30");
            var cut = RenderForm(schema, dataObject);

            TimeInput(cut).Change(invalid);

            Assert.Equal("08:30", dataObject.GetField(FieldName));
        }

        [Fact]
        [DisplayName("The time input's change handler is marked as updating value, so a rejected input reverts to the kept value in the browser")]
        public void ChangeHandler_UpdatesValueAttribute()
        {
            var schema = BuildSchema();
            var dataObject = BuildDataObject(schema);
            var cut = RenderForm(schema, dataObject);

            // A rejected input leaves the model unchanged, so the re-render emits the same `value` as
            // before and the browser would keep showing the typo. The renderer only pushes the kept value
            // back when the change handler is marked as updating `value`, because it then records the
            // typed text before diffing. That marker is asserted directly: bUnit rebuilds its markup from
            // the render tree on every render, so a DOM assertion passes with or without it.
            var changeHandlers = CurrentFrames(cut)
                .Where(f => FrameProperty(f, "FrameType")!.ToString() == "Attribute"
                    && (string?)FrameProperty(f, "AttributeName") == "onchange")
                .ToList();

            var handler = Assert.Single(changeHandlers);
            Assert.Equal("value", FrameProperty(handler, "AttributeEventUpdatesAttributeName"));
        }

        // Reflection keeps the render tree types out of the source: `GetCurrentRenderTreeFrames` is
        // protected on the renderer, and naming the frame types directly is rejected by BL0006.
        private List<object> CurrentFrames(IRenderedComponent<DynamicForm> cut)
        {
            var method = Renderer.GetType().GetMethod("GetCurrentRenderTreeFrames",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(method);
            var range = method!.Invoke(Renderer, [cut.ComponentId])!;
            var array = (Array)range.GetType().GetField("Array")!.GetValue(range)!;
            var count = (int)range.GetType().GetField("Count")!.GetValue(range)!;
            return Enumerable.Range(0, count).Select(i => array.GetValue(i)!).ToList();
        }

        private static object? FrameProperty(object frame, string name)
            => frame.GetType().GetProperty(name)!.GetValue(frame);
    }
}
