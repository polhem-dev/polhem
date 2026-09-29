using System.ComponentModel;
using System.Reflection;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// Additional coverage for <see cref="DateEdit"/>: the null and invalid paths of ParseToOffset,
    /// each type branch of the FieldValue setter, and the detail-row binding Bind(FormDataObject, LayoutFieldBase, DataRow).
    /// </summary>
    public class DateEditCoverageTests
    {
        private static DateTimeOffset? InvokeParseToOffset(string? raw)
        {
            var method = typeof(DateEdit).GetMethod(
                "ParseToOffset", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (DateTimeOffset?)method!.Invoke(null, new object?[] { raw });
        }

        private static FormDataObject BuildDataObject()
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add("hire_date", "Hire Date", FieldDbType.Date);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        private static FormDataObject BuildDataObjectWithDetail()
        {
            var schema = new FormSchema("Employee", "Employee");
            schema.Tables!.Add("Employee", "Employee");
            var detail = schema.Tables.Add("EmployeePhone", "Phones");
            detail.Fields!.Add("call_date", "Call Date", FieldDbType.Date);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        [Fact]
        [DisplayName("ParseToOffset returns null for a null string")]
        public void ParseToOffset_NullString_ReturnsNull()
        {
            var result = InvokeParseToOffset(null);

            Assert.Null(result);
        }

        [Fact]
        [DisplayName("ParseToOffset returns null for an invalid date string")]
        public void ParseToOffset_InvalidDateString_ReturnsNull()
        {
            var result = InvokeParseToOffset("not-a-date");

            Assert.Null(result);
        }

        [Fact]
        [DisplayName("ParseToOffset returns the matching DateTimeOffset for a valid date string (offset Zero, Kind Unspecified)")]
        public void ParseToOffset_ValidDateString_ReturnsDateTimeOffset()
        {
            var result = InvokeParseToOffset("2026-06-01");

            Assert.NotNull(result);
            Assert.Equal(2026, result!.Value.Year);
            Assert.Equal(6, result.Value.Month);
            Assert.Equal(1, result.Value.Day);
            Assert.Equal(TimeSpan.Zero, result.Value.Offset);
        }

        [Fact]
        [DisplayName("FieldValue setter sets SelectedDate to null for a null value")]
        public void FieldValue_SetNull_SetsSelectedDateNull()
        {
            var dataObject = BuildDataObject();
            dataObject.SetField("hire_date", "2026-01-15");
            var editor = new DateEdit();
            editor.Bind(dataObject, "hire_date");
            Assert.NotNull(editor.SelectedDate);

            editor.FieldValue = null;

            Assert.Null(editor.SelectedDate);
        }

        [Fact]
        [DisplayName("FieldValue setter assigns a DateTimeOffset value directly to SelectedDate")]
        public void FieldValue_SetDateTimeOffset_SetsSelectedDate()
        {
            var editor = new DateEdit();
            var dto = new DateTimeOffset(new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero);

            editor.FieldValue = dto;

            Assert.Equal(dto, editor.SelectedDate);
        }

        [Fact]
        [DisplayName("FieldValue setter converts a DateTime value to a DateTimeOffset with Kind Unspecified and offset Zero")]
        public void FieldValue_SetDateTime_SetsDateOnlyWithUnspecifiedKind()
        {
            var editor = new DateEdit();
            var dt = new DateTime(2026, 5, 20, 10, 30, 0, DateTimeKind.Local);

            editor.FieldValue = dt;

            Assert.NotNull(editor.SelectedDate);
            Assert.Equal(2026, editor.SelectedDate!.Value.Year);
            Assert.Equal(5, editor.SelectedDate.Value.Month);
            Assert.Equal(20, editor.SelectedDate.Value.Day);
            Assert.Equal(TimeSpan.Zero, editor.SelectedDate.Value.Offset);
        }

        [Fact]
        [DisplayName("FieldValue setter parses a string value through ParseToOffset and sets SelectedDate")]
        public void FieldValue_SetStringDate_ParsesAndSetsSelectedDate()
        {
            var editor = new DateEdit();

            editor.FieldValue = "2026-08-15";

            Assert.NotNull(editor.SelectedDate);
            Assert.Equal(2026, editor.SelectedDate!.Value.Year);
            Assert.Equal(8, editor.SelectedDate.Value.Month);
            Assert.Equal(15, editor.SelectedDate.Value.Day);
        }

        [Fact]
        [DisplayName("Bind(FormDataObject, LayoutFieldBase, DataRow) reads the row value after binding a detail row")]
        public void Bind_WithDataRow_LoadsValueFromRow()
        {
            var dataObject = BuildDataObjectWithDetail();
            var detailTable = dataObject.DataSet.Tables["EmployeePhone"]!;
            var row = detailTable.NewRow();
            row["call_date"] = "2026-04-10";
            detailTable.Rows.Add(row);

            var field = new LayoutField { FieldName = "call_date" };
            var editor = new DateEdit();
            editor.Bind(dataObject, field, row);

            Assert.NotNull(editor.SelectedDate);
            Assert.Equal(2026, editor.SelectedDate!.Value.Year);
            Assert.Equal(4, editor.SelectedDate.Value.Month);
            Assert.Equal(10, editor.SelectedDate.Value.Day);
        }
    }
}
