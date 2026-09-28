using System.ComponentModel;
using System.Data;
using System.Globalization;
using Avalonia.Input;
using Polhem.Base.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Tests.Shared;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// Behaviour checks for <see cref="DateTimeEdit"/>: an instant is shown and parsed with its time of
    /// day in the user's culture, an untouched value is never rewritten, and input that does not parse
    /// keeps the stored value.
    /// </summary>
    public class DateTimeEditTests
    {
        private static readonly DateTime s_stored = new(2026, 9, 28, 14, 30, 15, 250, DateTimeKind.Unspecified);

        private static FormDataObject BuildDataObject()
        {
            var schema = new FormSchema("Event", "Event");
            var master = schema.Tables!.Add("Event", "Event");
            master.Fields!.Add("occurred_at", "Occurred At", FieldDbType.DateTime);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            dataObject.MasterRow!["occurred_at"] = s_stored;
            return dataObject;
        }

        private static LayoutField Field() => new() { FieldName = "occurred_at" };

        // Commit through Enter (a KeyDown routed event), as the other text editor tests do; the LostFocus
        // routed event carries a FocusChangedEventArgs that cannot be synthesised here.
        private static void Commit(DateTimeEdit editor)
            => editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

        private static DateTime Stored(FormDataObject dataObject) => (DateTime)dataObject.MasterRow!["occurred_at"];

        [Fact]
        [DisplayName("After Bind the value shows its date and time of day in the user's culture")]
        public void Bind_ShowsDateAndTimeInUserCulture()
        {
            using var culture = new CultureScope("de-DE");
            var editor = new DateTimeEdit();

            editor.Bind(BuildDataObject(), Field());

            Assert.Equal(s_stored.ToString("G", CultureInfo.CurrentCulture), editor.Text);
            Assert.Contains("14:30", editor.Text, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("An instant stored at midnight still shows its time, unlike a date-only editor")]
        public void Bind_Midnight_ShowsTime()
        {
            using var culture = new CultureScope("en-US");
            var dataObject = BuildDataObject();
            var midnight = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Unspecified);
            dataObject.MasterRow!["occurred_at"] = midnight;
            var editor = new DateTimeEdit();

            editor.Bind(dataObject, Field());

            Assert.Equal(midnight.ToString("G", CultureInfo.CurrentCulture), editor.Text);
        }

        [Fact]
        [DisplayName("A commit parses the text in the user's culture and keeps the time part")]
        public void Commit_CultureText_WritesDateAndTime()
        {
            using var culture = new CultureScope("de-DE");
            var dataObject = BuildDataObject();
            var editor = new DateTimeEdit();
            editor.Bind(dataObject, Field());

            editor.Text = "01.10.2026 08:05";
            Commit(editor);

            Assert.Equal(new DateTime(2026, 10, 1, 8, 5, 0), Stored(dataObject));
        }

        [Fact]
        [DisplayName("An ISO date and time is accepted whatever the user's culture")]
        public void Commit_IsoText_IsAccepted()
        {
            using var culture = new CultureScope("en-US");
            var dataObject = BuildDataObject();
            var editor = new DateTimeEdit();
            editor.Bind(dataObject, Field());

            editor.Text = "2026-10-01 20:45";
            Commit(editor);

            Assert.Equal(new DateTime(2026, 10, 1, 20, 45, 0), Stored(dataObject));
        }

        [Fact]
        [DisplayName("Committing the text as shown leaves the stored value untouched, fractions of a second included")]
        public void Commit_UnchangedText_KeepsStoredValueAndDoesNotMarkDirty()
        {
            using var culture = new CultureScope("en-US");
            var dataObject = BuildDataObject();
            dataObject.DataSet.AcceptChanges();
            var editor = new DateTimeEdit();
            editor.Bind(dataObject, Field());

            Commit(editor);

            Assert.Equal(s_stored, Stored(dataObject));
            Assert.Equal(DataRowState.Unchanged, dataObject.MasterRow!.RowState);
        }

        [Fact]
        [DisplayName("Text that does not parse keeps the stored value instead of erasing it")]
        public void Commit_UnparseableText_KeepsStoredValue()
        {
            using var culture = new CultureScope("en-US");
            var dataObject = BuildDataObject();
            var editor = new DateTimeEdit();
            editor.Bind(dataObject, Field());

            editor.Text = "not a date";
            Commit(editor);

            Assert.Equal(s_stored, Stored(dataObject));
        }

        [Fact]
        [DisplayName("Clearing the text unsets the field")]
        public void Commit_EmptyText_UnsetsField()
        {
            var dataObject = BuildDataObject();
            var editor = new DateTimeEdit();
            editor.Bind(dataObject, Field());

            editor.Text = string.Empty;
            Commit(editor);

            Assert.Equal(string.Empty, dataObject.GetField("occurred_at"));
        }

        [Fact]
        [DisplayName("The placeholder spells the culture's date and time pattern the editor accepts")]
        public void Constructor_PlaceholderShowsCulturePattern()
        {
            using var culture = new CultureScope("de-DE");
            var editor = new DateTimeEdit();

            var format = CultureInfo.CurrentCulture.DateTimeFormat;
            Assert.Equal(format.ShortDatePattern + " " + format.LongTimePattern, editor.PlaceholderText);
        }

        [Fact]
        [DisplayName("The in-grid editor of a DateTimeEdit column writes the parsed date and time to the row")]
        public void GridCellEditor_DateTimeColumn_WritesParsedValue()
        {
            using var culture = new CultureScope("en-US");
            var rows = new DataTable("Event");
            rows.Columns.Add("occurred_at", typeof(DateTime));
            rows.Rows.Add(s_stored);
            var column = new LayoutColumn("occurred_at", "Occurred At", ControlType.DateTimeEdit);
            var layout = new LayoutGrid("Event", "Event");
            layout.Columns!.Add(column);
            var grid = new GridControl();
            grid.Bind(layout, rows);

            var editor = (global::Avalonia.Controls.TextBox)typeof(GridControl)
                .GetMethod("BuildCellEditor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(grid, [rows.DefaultView[0], column])!;
            Assert.Equal(s_stored.ToString("G", CultureInfo.CurrentCulture), editor.Text);

            editor.Text = "10/1/2026 8:05 PM";
            editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

            Assert.Equal(new DateTime(2026, 10, 1, 20, 5, 0), (DateTime)rows.Rows[0]["occurred_at"]);
        }
    }
}
