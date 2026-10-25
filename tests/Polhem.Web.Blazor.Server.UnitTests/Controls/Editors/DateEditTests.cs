using System.ComponentModel;
using System.Globalization;
using Bunit;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Tests.Shared;
using Polhem.Web.Blazor.Server.Controls.Editors;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.UnitTests.Controls.Editors
{
    /// <summary>
    /// Behavior tests for <see cref="DateEdit"/> and <see cref="YearMonthEdit"/>.
    /// </summary>
    public class DateEditTests : BunitContext
    {
        private const string DateField = "hire_date";
        private const string MonthField = "report_month";

        private static FormDataObject BuildDataObject()
        {
            var schema = new FormSchema("Employee", "Employee");
            var fields = schema.Tables!.Add("Employee", "Employee").Fields!;
            fields.Add(DateField, "Hired", FieldDbType.Date);
            fields.Add(MonthField, "Month", FieldDbType.String);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        [Fact]
        [DisplayName("A date change is written back as yyyy-MM-dd")]
        public void DateEdit_Change_WritesDate()
        {
            var dataObject = BuildDataObject();
            var cut = Render<DateEdit>(p => p.Add(c => c.FieldName, DateField).Add(c => c.DataObject, dataObject));

            cut.Find("input[type='date']").Change("2026-10-25");

            Assert.Equal(new DateTime(2026, 10, 25), (DateTime)dataObject.MasterRow![DateField]);
            Assert.Equal("2026-10-25", cut.Find("input[type='date']").GetAttribute("value"));
        }

        [Fact]
        [DisplayName("The read-only state shows the date in the user's culture instead of a date input")]
        public void DateEdit_ReadOnly_ShowsCultureText()
        {
            using var culture = new CultureScope("de-DE");
            var dataObject = BuildDataObject();
            dataObject.SetField(DateField, "2026-10-25");

            var cut = Render<DateEdit>(p => p
                .Add(c => c.LayoutField, new LayoutField { FieldName = DateField, ReadOnly = true })
                .Add(c => c.DataObject, dataObject));

            Assert.Empty(cut.FindAll("input[type='date']"));
            var input = cut.Find("input.polhem-field-editor--date");
            Assert.True(input.HasAttribute("readonly"));
            Assert.Equal(new DateTime(2026, 10, 25).ToString("d", CultureInfo.CurrentCulture), input.GetAttribute("value"));
        }

        [Fact]
        [DisplayName("A year-month change is written back as yyyy-MM")]
        public void YearMonthEdit_Change_WritesYearMonth()
        {
            var dataObject = BuildDataObject();
            var cut = Render<YearMonthEdit>(p => p.Add(c => c.FieldName, MonthField).Add(c => c.DataObject, dataObject));

            cut.Find("input[type='month']").Change("2026-03");

            Assert.Equal("2026-03", dataObject.GetField(MonthField));
        }
    }
}
