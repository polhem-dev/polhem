using System.ComponentModel;
using Bunit;
using MudBlazor;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Tests.Shared;
using Polhem.UI.Core.DataObjects;
using Polhem.Web.Blazor.Mud.Controls.Editors;
using Polhem.Web.Blazor.Mud.DependencyInjection;

namespace Polhem.Web.Blazor.Mud.UnitTests
{
    /// <summary>
    /// How the MudBlazor date-and-time editor turns what its date and time pickers report into the field value.
    /// </summary>
    public class MudDateTimeEditTests : BunitContext, IAsyncLifetime
    {
        private const string FieldName = "value";
        private const string Stored = "2026-03-09T08:30:00";

        /// <summary>
        /// Initializes a new instance of <see cref="MudDateTimeEditTests"/>.
        /// </summary>
        public MudDateTimeEditTests()
        {
            Services.AddPolhemMud();
            JSInterop.Mode = JSRuntimeMode.Loose;
            Render<MudPopoverProvider>();
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

        private static void EnterDate(IRenderedComponent<DateTimeEdit> cut, string text)
            => cut.FindAll(".polhem-field-editor--datetime input")[0].Change(text);

        private static void EnterTime(IRenderedComponent<DateTimeEdit> cut, string text)
            => cut.FindAll(".polhem-field-editor--datetime input")[1].Change(text);

        [Fact]
        [DisplayName("A date typed in the culture pattern keeps the stored time of day")]
        public void Date_CulturePattern_KeepsTheTime()
        {
            using var culture = new CultureScope("en-US");
            var dataObject = BuildDataObject(Stored);
            var cut = RenderEditor(dataObject);

            EnterDate(cut, "4/1/2026");

            Assert.StartsWith("2026-04-01T08:30", dataObject.GetField(FieldName), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A date typed in the ISO form goes through the shared input rules and keeps the time of day")]
        public void Date_IsoForm_KeepsTheTime()
        {
            using var culture = new CultureScope("en-US");
            var dataObject = BuildDataObject(Stored);
            var cut = RenderEditor(dataObject);

            EnterDate(cut, "2026-04-02");

            Assert.StartsWith("2026-04-02T08:30", dataObject.GetField(FieldName), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Text in the date box that is not a date keeps the stored value")]
        public void Date_NotADate_KeepsTheValue()
        {
            using var culture = new CultureScope("en-US");
            var dataObject = BuildDataObject(Stored);
            var before = dataObject.GetField(FieldName);
            var cut = RenderEditor(dataObject);

            EnterDate(cut, "not a date");

            Assert.Equal(before, dataObject.GetField(FieldName));
        }

        [Fact]
        [DisplayName("Clearing the date box unsets the field")]
        public void Date_Cleared_UnsetsTheField()
        {
            using var culture = new CultureScope("en-US");
            var dataObject = BuildDataObject(Stored);
            var cut = RenderEditor(dataObject);

            EnterDate(cut, string.Empty);

            Assert.Equal(string.Empty, dataObject.GetField(FieldName));
        }

        [Fact]
        [DisplayName("A time typed into the time box keeps the stored date")]
        public void Time_Typed_KeepsTheDate()
        {
            using var culture = new CultureScope("en-US");
            var dataObject = BuildDataObject(Stored);
            var cut = RenderEditor(dataObject);

            EnterTime(cut, "17:45");

            Assert.StartsWith("2026-03-09T17:45", dataObject.GetField(FieldName), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Text in the time box that is not a time keeps the stored value")]
        public void Time_NotATime_KeepsTheValue()
        {
            using var culture = new CultureScope("en-US");
            var dataObject = BuildDataObject(Stored);
            var before = dataObject.GetField(FieldName);
            var cut = RenderEditor(dataObject);

            EnterTime(cut, "later");

            Assert.Equal(before, dataObject.GetField(FieldName));
        }

        [Fact]
        [DisplayName("Clearing the time box keeps the date at midnight")]
        public void Time_Cleared_KeepsTheDateAtMidnight()
        {
            using var culture = new CultureScope("en-US");
            var dataObject = BuildDataObject(Stored);
            var cut = RenderEditor(dataObject);

            EnterTime(cut, string.Empty);

            Assert.StartsWith("2026-03-09", dataObject.GetField(FieldName), StringComparison.Ordinal);
            Assert.DoesNotContain("08:30", dataObject.GetField(FieldName), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A time typed while the field has no date writes nothing")]
        public void Time_NoDate_WritesNothing()
        {
            using var culture = new CultureScope("en-US");
            var dataObject = BuildDataObject(string.Empty);
            var cut = RenderEditor(dataObject);

            EnterTime(cut, "17:45");

            Assert.Equal(string.Empty, dataObject.GetField(FieldName));
        }
    }
}
