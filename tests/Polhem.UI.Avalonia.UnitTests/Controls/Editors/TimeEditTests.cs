using System.ComponentModel;
using Avalonia.Input;
using Polhem.Core;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// Behaviour checks for <see cref="TimeEdit"/>: commit-time normalisation to the fixed-width
    /// <c>"HH:mm"</c> storage form, an explicit empty meaning "unset", and tolerance of input that
    /// does not parse.
    /// </summary>
    public class TimeEditTests
    {
        private static FormDataObject BuildDataObject()
        {
            var schema = new FormSchema("Shift", "Shift");
            var master = schema.Tables!.Add("Shift", "Shift");
            master.Fields!.Add("work_start", "Start", FieldDbType.Time);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        private static LayoutField StartField() => new() { FieldName = "work_start" };

        // Commit is exercised via Enter (a KeyDown routed event), matching NumericEditTests; the
        // LostFocus routed event carries a FocusChangedEventArgs that cannot be synthesised here.
        private static void Commit(TimeEdit editor)
            => editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

        [Fact]
        [DisplayName("After Bind the value is displayed as fixed-width HH:mm")]
        public void Bind_DisplaysFixedWidthForm()
        {
            var dataObject = BuildDataObject();
            // Assigned to the row directly: `SetField` already normalises, which would let this pass
            // without the editor doing any normalising of its own.
            dataObject.MasterRow!["work_start"] = "8:30";

            var editor = new TimeEdit();
            editor.Bind(dataObject, StartField());

            Assert.Equal("08:30", editor.Text);
        }

        [Fact]
        [DisplayName("A commit normalizes loose input to fixed-width HH:mm")]
        public void Commit_NormalizesLooseInput()
        {
            var dataObject = BuildDataObject();
            var editor = new TimeEdit();
            editor.Bind(dataObject, StartField());

            editor.Text = "8:30";
            Commit(editor);

            // Fixed width is what makes the stored value sort chronologically.
            Assert.Equal("08:30", dataObject.GetField("work_start"));
        }

        [Fact]
        [DisplayName("Clearing the field writes back an empty string (unset), not 00:00")]
        public void Commit_EmptyText_WritesUnset()
        {
            var dataObject = BuildDataObject();
            dataObject.SetField("work_start", "08:30");
            var editor = new TimeEdit();
            editor.Bind(dataObject, StartField());

            editor.Text = string.Empty;
            Commit(editor);

            // Midnight is a legal value, so it cannot double as "unset".
            Assert.Equal(string.Empty, dataObject.GetField("work_start"));
        }

        [Fact]
        [DisplayName("00:00 is a valid time and is written back normally")]
        public void Commit_Midnight_IsStored()
        {
            var dataObject = BuildDataObject();
            var editor = new TimeEdit();
            editor.Bind(dataObject, StartField());

            editor.Text = "00:00";
            Commit(editor);

            Assert.Equal("00:00", dataObject.GetField("work_start"));
        }

        [Theory]
        [InlineData("25:00")]
        [InlineData("08:99")]
        [InlineData("abc")]
        [DisplayName("Unparsable input keeps the last valid value instead of clearing the field")]
        public void Commit_InvalidText_KeepsLastValidValue(string invalid)
        {
            var dataObject = BuildDataObject();
            dataObject.SetField("work_start", "08:30");
            var editor = new TimeEdit();
            editor.Bind(dataObject, StartField());

            editor.Text = invalid;
            Commit(editor);

            Assert.Equal("08:30", dataObject.GetField("work_start"));
        }

        [Fact]
        [DisplayName("The maximum input length is the width of the time format")]
        public void MaxLength_MatchesStorageWidth()
        {
            Assert.Equal(ValueUtilities.TimeOnlyLength, new TimeEdit().MaxLength);
        }

        [Fact]
        [DisplayName("The editor factory creates a TimeEdit control for TimeEdit")]
        public void Factory_TimeEdit_CreatesTimeEditor()
        {
            Assert.IsType<TimeEdit>(FieldEditorFactory.Create(ControlType.TimeEdit));
        }
    }
}
