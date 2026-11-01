using System.ComponentModel;
using Bunit;
using Polhem.Core.Data;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;
using Polhem.Web.Blazor.Controls.Editors;
using Polhem.Web.Blazor.DataObjects;

namespace Polhem.Web.Blazor.UnitTests.Controls.Editors
{
    /// <summary>
    /// The commit rules of the editor bases, driven through probes with no markup, because they belong to the shared
    /// layer and no UI package may be needed to check them.
    /// </summary>
    public class EditorBaseCommitTests : BunitContext
    {
        private const string FieldName = "value";

        private static FormDataObject BuildDataObject(FieldDbType dbType, string? value, Action<FormField>? configure = null)
        {
            var schema = new FormSchema("Probe", "Probe");
            var field = schema.Tables!.Add("Probe", "Probe").Fields!.Add(FieldName, "Value", dbType);
            configure?.Invoke(field);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            if (value is not null) dataObject.SetField(FieldName, value);
            return dataObject;
        }

        private TProbe RenderProbe<TProbe>(FormDataObject dataObject) where TProbe : FieldEditorBase
            => Render<TProbe>(p => p
                .Add(c => c.DataObject, dataObject)
                .Add(c => c.FieldName, FieldName)).Instance;

        [Fact]
        [DisplayName("A date picker's date is written in the value format, and null unsets the field")]
        public void CommitDate_DateThenNull_WritesThenUnsets()
        {
            var dataObject = BuildDataObject(FieldDbType.Date, null);
            var probe = RenderProbe<DateProbe>(dataObject);

            probe.Commit(new DateTime(2026, 3, 9, 0, 0, 0, DateTimeKind.Unspecified));
            Assert.Equal(new DateTime(2026, 3, 9, 0, 0, 0, DateTimeKind.Unspecified), probe.Current);

            probe.Commit(null);
            Assert.Null(probe.Current);
            Assert.Equal(string.Empty, dataObject.GetField(FieldName));
        }

        [Fact]
        [DisplayName("Text that is not a date keeps the stored date")]
        public void CommitInput_InvalidText_KeepsStoredValue()
        {
            var dataObject = BuildDataObject(FieldDbType.Date, "2026-03-09");
            var probe = RenderProbe<DateProbe>(dataObject);

            probe.CommitText("not a date");

            Assert.Equal(new DateTime(2026, 3, 9, 0, 0, 0, DateTimeKind.Unspecified), probe.Current);
        }

        [Fact]
        [DisplayName("A date-and-time picker reporting the shown value writes nothing, keeping fractions of a second")]
        public void CommitValue_UnchangedValue_KeepsStoredFractions()
        {
            var dataObject = BuildDataObject(FieldDbType.DateTime, "2026-03-09T08:30:15.1234567");
            var before = dataObject.GetField(FieldName);
            var probe = RenderProbe<DateTimeProbe>(dataObject);

            probe.Commit(probe.Current!.Value.AddTicks(-(probe.Current.Value.Ticks % TimeSpan.TicksPerSecond)));

            Assert.Equal(before, dataObject.GetField(FieldName));
        }

        [Fact]
        [DisplayName("A date-and-time picker's new value is written, and null unsets the field")]
        public void CommitValue_NewValueThenNull_WritesThenUnsets()
        {
            var dataObject = BuildDataObject(FieldDbType.DateTime, "2026-03-09T08:30:00");
            var probe = RenderProbe<DateTimeProbe>(dataObject);

            probe.Commit(new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Unspecified));
            Assert.Equal(new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Unspecified), probe.Current);

            probe.Commit(null);
            Assert.Null(probe.Current);
        }

        [Theory]
        [InlineData("8:30", "08:30")]
        [InlineData("not a time", "07:15")]
        [InlineData("", "")]
        [DisplayName("A time is normalised, text that is not a time keeps the stored value, and an empty box unsets it")]
        public void CommitText_Time_FollowsTheInputRules(string typed, string expected)
        {
            var dataObject = BuildDataObject(FieldDbType.String, "07:15");
            var probe = RenderProbe<TimeProbe>(dataObject);

            probe.Commit(typed);

            Assert.Equal(expected, dataObject.GetField(FieldName));
        }

        [Fact]
        [DisplayName("Partial numeric input keeps the last valid value")]
        public void CommitText_PartialNumber_KeepsLastValidValue()
        {
            var dataObject = BuildDataObject(FieldDbType.Decimal, "12.5");
            var probe = RenderProbe<NumericProbe>(dataObject);

            probe.Commit("abc");

            Assert.Equal(12.5m, decimal.Parse(dataObject.GetField(FieldName), System.Globalization.CultureInfo.InvariantCulture));
        }

        [Fact]
        [DisplayName("A stored value that matches no item has no selected item, and a choice writes the item's value")]
        public void DropDown_UnknownValueThenChoice_FollowsTheItems()
        {
            var dataObject = BuildDataObject(FieldDbType.String, "gone",
                f => f.ListItems!.Add("a", "Alpha"));
            var probe = RenderProbe<DropDownProbe>(dataObject);
            Assert.Null(probe.Selected);

            probe.Commit("a");

            Assert.Equal("Alpha", probe.Selected?.Text);
            Assert.Equal("a", dataObject.GetField(FieldName));
        }

        [Fact]
        [DisplayName("A check box follows the stored value and writes the chosen state")]
        public void Check_Commit_RoundTripsThroughTheField()
        {
            var dataObject = BuildDataObject(FieldDbType.Boolean, "True");
            var probe = RenderProbe<CheckProbe>(dataObject);
            Assert.True(probe.Current);

            probe.Commit(false);

            Assert.False(probe.Current);
        }

        private sealed class DateProbe : DateEditorBase
        {
            public DateTime? Current => Date;
            public void Commit(DateTime? value) => CommitDate(value);
            public void CommitText(string text) => CommitInput(text);
        }

        private sealed class DateTimeProbe : DateTimeEditorBase
        {
            public DateTime? Current => Value;
            public void Commit(DateTime? value) => CommitValue(value);
        }

        private sealed class TimeProbe : TimeEditorBase
        {
            public void Commit(string text) => CommitText(text);
        }

        private sealed class NumericProbe : NumericEditorBase
        {
            public void Commit(string text) => CommitText(text);
        }

        private sealed class DropDownProbe : DropDownEditorBase
        {
            public ListItem? Selected => SelectedItem;
            public void Commit(string value) => CommitValue(value);
        }

        private sealed class CheckProbe : CheckEditorBase
        {
            public bool Current => IsChecked;
            public void Commit(bool value) => CommitChecked(value);
        }
    }
}
