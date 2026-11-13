using System.ComponentModel;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.FluentUI.AspNetCore.Components;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.UI.Core.DataObjects;
using Polhem.Web.Blazor.Fluent.Controls.Editors;
using Polhem.Web.Blazor.Fluent.DependencyInjection;

namespace Polhem.Web.Blazor.Fluent.UnitTests
{
    /// <summary>
    /// How the Fluent UI date-and-time editor turns what its date picker and time box report into the field value.
    /// </summary>
    public class FluentDateTimeEditTests : BunitContext, IAsyncLifetime
    {
        private const string FieldName = "value";
        private const string Stored = "2026-03-09T08:30:00";
        private const string Inputs = ".polhem-field-editor--datetime fluent-text-input";

        /// <summary>
        /// Initializes a new instance of <see cref="FluentDateTimeEditTests"/>.
        /// </summary>
        public FluentDateTimeEditTests()
        {
            Services.AddPolhemFluent();
            JSInterop.Mode = JSRuntimeMode.Loose;
        }

        /// <inheritdoc />
        Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

        /// <inheritdoc />
        async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync().ConfigureAwait(true);

        private static FormDataObject BuildDataObject(string value)
        {
            var schema = new FormSchema("Probe", "Probe");
            schema.Tables!.Add("Probe", "Probe").Fields!.Add(FieldName, "Value", FieldDbType.DateTime);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            dataObject.SetField(FieldName, value);
            return dataObject;
        }

        private IRenderedComponent<DateTimeEdit> RenderEditor(FormDataObject dataObject)
            => Render<DateTimeEdit>(p => p
                .Add(c => c.FieldName, FieldName)
                .Add(c => c.DataObject, dataObject));

        // The time box is the last text input; a recreated one is a new component instance.
        private static FluentTextInput TimeInput(IRenderedComponent<DateTimeEdit> cut)
            => cut.FindComponents<FluentTextInput>()[^1].Instance;

        private static void EnterTime(IRenderedComponent<DateTimeEdit> cut, string text)
            => cut.FindAll(Inputs)[1].Change(text);

        [Fact]
        [DisplayName("A date the picker reports keeps the stored time of day")]
        public async Task Date_Picked_KeepsTheTime()
        {
            var dataObject = BuildDataObject(Stored);
            var cut = RenderEditor(dataObject);
            var picker = cut.FindComponent<FluentDatePicker<DateTime?>>();

            await cut.InvokeAsync(() => picker.Instance.ValueChanged.InvokeAsync(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Unspecified)));

            Assert.StartsWith("2026-04-01T08:30", dataObject.GetField(FieldName), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A cleared date picker unsets the field")]
        public async Task Date_Cleared_UnsetsTheField()
        {
            var dataObject = BuildDataObject(Stored);
            var cut = RenderEditor(dataObject);
            var picker = cut.FindComponent<FluentDatePicker<DateTime?>>();

            await cut.InvokeAsync(() => picker.Instance.ValueChanged.InvokeAsync(null));

            Assert.Equal(string.Empty, dataObject.GetField(FieldName));
        }

        [Fact]
        [DisplayName("A time typed into the time box keeps the stored date")]
        public void Time_Typed_KeepsTheDate()
        {
            var dataObject = BuildDataObject(Stored);
            var cut = RenderEditor(dataObject);

            EnterTime(cut, "17:45");

            Assert.StartsWith("2026-03-09T17:45", dataObject.GetField(FieldName), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Clearing the time box keeps the date at midnight")]
        public void Time_Cleared_KeepsTheDateAtMidnight()
        {
            var dataObject = BuildDataObject(Stored);
            var cut = RenderEditor(dataObject);

            EnterTime(cut, string.Empty);

            Assert.StartsWith("2026-03-09", dataObject.GetField(FieldName), StringComparison.Ordinal);
            Assert.DoesNotContain("08:30", dataObject.GetField(FieldName), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Text in the time box that is not a time keeps the stored value and shows it again")]
        public void Time_NotATime_KeepsTheValue()
        {
            var dataObject = BuildDataObject(Stored);
            var before = dataObject.GetField(FieldName);
            var cut = RenderEditor(dataObject);

            EnterTime(cut, "later");

            Assert.Equal(before, dataObject.GetField(FieldName));
            cut.WaitForAssertion(() => Assert.Equal("08:30", cut.FindAll(Inputs)[1].GetAttribute("value")));
        }

        [Fact]
        [DisplayName("A time typed while the field has no date writes nothing")]
        public void Time_NoDate_WritesNothing()
        {
            var dataObject = BuildDataObject(string.Empty);
            var cut = RenderEditor(dataObject);

            EnterTime(cut, "17:45");

            Assert.Equal(string.Empty, dataObject.GetField(FieldName));
        }

        [Fact]
        [DisplayName("Leaving the editor shows the stored value again once the focus has settled elsewhere")]
        public void Leave_FocusSettledElsewhere_RecreatesTheInputs()
        {
            var dataObject = BuildDataObject(Stored);
            var cut = RenderEditor(dataObject);
            var before = TimeInput(cut);
            var editor = cut.Find("div.polhem-field-editor--datetime");

            editor.TriggerEvent("onfocusin", new FocusEventArgs());
            editor.TriggerEvent("onfocusout", new FocusEventArgs());

            cut.WaitForAssertion(() => Assert.NotSame(before, TimeInput(cut)), TimeSpan.FromSeconds(5));
            Assert.Equal("08:30", cut.FindAll(Inputs)[1].GetAttribute("value"));
        }

        [Fact]
        [DisplayName("Focus that comes back before the delay ends keeps the inputs")]
        public async Task Leave_FocusReturns_KeepsTheInputs()
        {
            var dataObject = BuildDataObject(Stored);
            var cut = RenderEditor(dataObject);
            var before = TimeInput(cut);

            var leaving = cut.Find("div.polhem-field-editor--datetime").TriggerEventAsync("onfocusout", new FocusEventArgs());
            cut.Find("div.polhem-field-editor--datetime").TriggerEvent("onfocusin", new FocusEventArgs());
            await leaving.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Same(before, TimeInput(cut));
        }
    }
}
