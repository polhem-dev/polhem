using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// Behaviour checks for <see cref="DateEdit"/> / <see cref="YearMonthEdit"/>:
    /// ISO round-trip and the year-month variant.
    /// </summary>
    public class DateEditTests
    {
        private static FormDataObject BuildDataObject()
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add("hire_date", "Hire Date", FieldDbType.Date);
            master.Fields.Add("pay_month", "Pay Month", FieldDbType.String);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        [Fact]
        [DisplayName("Bind loads the initial date value")]
        public void Bind_ExistingDate_LoadsIntoSelectedDate()
        {
            var dataObject = BuildDataObject();
            dataObject.SetField("hire_date", "2026-06-11");

            var editor = new DateEdit();
            editor.Bind(dataObject, "hire_date");

            Assert.NotNull(editor.SelectedDate);
            Assert.Equal(new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Unspecified), editor.SelectedDate!.Value.DateTime);
        }

        [Fact]
        [DisplayName("A selected date is written back as yyyy-MM-dd")]
        public void SelectedDateChanged_AfterBind_WritesBackIsoDate()
        {
            var dataObject = BuildDataObject();
            var editor = new DateEdit();
            editor.Bind(dataObject, "hire_date");

            editor.SelectedDate = new DateTimeOffset(
                new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero);

            Assert.Equal("2026-01-15", dataObject.GetField("hire_date"));
        }

        [Fact]
        [DisplayName("YearMonthEdit hides the day part and writes back yyyy-MM")]
        public void YearMonthEdit_SelectedDate_WritesBackYearMonth()
        {
            var dataObject = BuildDataObject();
            var editor = new YearMonthEdit();
            Assert.False(editor.DayVisible);

            editor.Bind(dataObject, "pay_month");
            editor.SelectedDate = new DateTimeOffset(
                new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero);

            Assert.Equal("2026-03", dataObject.GetField("pay_month"));
        }

        [Fact]
        [DisplayName("SelectedDate is null after binding an empty field")]
        public void Bind_EmptyValue_LeavesSelectedDateNull()
        {
            var dataObject = BuildDataObject();
            var editor = new DateEdit();

            editor.Bind(dataObject, "pay_month");

            Assert.Null(editor.SelectedDate);
        }

        [Fact]
        [DisplayName("With AllowEditModes=Add only Add mode enables the editor")]
        public void SetControlState_AllowEditModesAdd_OnlyAddEnabled()
        {
            var dataObject = BuildDataObject();
            var field = new LayoutField { FieldName = "hire_date", AllowEditModes = FormEditModes.Add };
            var editor = new DateEdit();
            editor.Bind(dataObject, field);

            // Read-only swaps to a flat, non-interactive display rather than greying out,
            // so editability is observed through IsHitTestVisible instead of IsEnabled.
            editor.SetControlState(SingleFormMode.Add);
            Assert.True(editor.IsHitTestVisible);

            editor.SetControlState(SingleFormMode.Edit);
            Assert.False(editor.IsHitTestVisible);

            editor.SetControlState(SingleFormMode.View);
            Assert.False(editor.IsHitTestVisible);
        }

        [Fact]
        [DisplayName("ReadOnlyText is formatted as yyyy-MM-dd after Bind")]
        public void ReadOnlyText_AfterBind_FormatsSelectedDate()
        {
            var dataObject = BuildDataObject();
            dataObject.SetField("hire_date", "2026-06-11");

            var editor = new DateEdit();
            editor.Bind(dataObject, "hire_date");

            Assert.Equal("2026-06-11", editor.ReadOnlyText);
        }

        [Fact]
        [DisplayName("ReadOnlyText of YearMonthEdit is formatted as yyyy-MM")]
        public void ReadOnlyText_YearMonthEdit_FormatsYearMonth()
        {
            var dataObject = BuildDataObject();
            dataObject.SetField("pay_month", "2026-06");

            var editor = new YearMonthEdit();
            editor.Bind(dataObject, "pay_month");

            Assert.Equal("2026-06", editor.ReadOnlyText);
        }
    }
}
