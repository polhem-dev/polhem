using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Exceptions;
using Polhem.Db.Dml;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;

namespace Polhem.Db.UnitTests.Dml
{
    /// <summary>
    /// A filter value that arrives as a string (a <c>Plain</c> body names no CLR type) is converted to the type of the
    /// field it is compared with, so every provider can compare it with the typed column.
    /// </summary>
    public class FilterValueCoercionTests
    {
        private static readonly Guid s_guid = new("6c1f4c0e-8a0b-4d8e-9a3c-2f5b7d9e1a04");

        private static FormTable Table()
        {
            var schema = new FormSchema("Doc", "Doc");
            var table = schema.Tables!.Add("Doc", "Doc");
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            table.Fields.Add(SysFields.Id, "Code", FieldDbType.String);
            table.Fields.Add("due_date", "Due", FieldDbType.Date);
            table.Fields.Add("logged_at", "Logged", FieldDbType.DateTime);
            table.Fields.Add("qty", "Qty", FieldDbType.Integer);
            table.Fields.Add("big", "Big", FieldDbType.Long);
            table.Fields.Add("amount", "Amount", FieldDbType.Decimal);
            table.Fields.Add("active", "Active", FieldDbType.Boolean);
            table.Fields.Add("start_time", "Start", FieldDbType.Time);
            return table;
        }

        private static object? CoercedValue(string field, object value)
        {
            var coerced = Assert.IsType<FilterCondition>(
                FilterValueCoercion.Coerce(new FilterCondition(field, ComparisonOperator.Equal, value), Table()));
            return coerced.Value;
        }

        [Fact]
        [DisplayName("String values become the Guid, integer, long, decimal and boolean their fields declare")]
        public void Coerce_StringValues_BecomeTheFieldType()
        {
            Assert.Equal(s_guid, CoercedValue(SysFields.RowId, s_guid.ToString()));
            Assert.Equal(42, CoercedValue("qty", "42"));
            Assert.Equal(9007199254740993L, CoercedValue("big", "9007199254740993"));
            Assert.Equal(10.5m, CoercedValue("amount", "10.5"));
            Assert.True(CoercedValue("active", "true") is true);
        }

        [Theory]
        [InlineData("2026-01-02T12:00:00Z", 12)]
        [InlineData("2026-01-02T20:00:00+08:00", 12)]
        [InlineData("2026-01-02T12:00:00", 12)]
        [InlineData("2026-01-02T12:00:00.123Z", 12)]
        [InlineData("2026-01-02T12:00Z", 12)]
        [DisplayName("A date and time string becomes the UTC instant it names, and text without an offset is taken as UTC")]
        public void Coerce_DateTimeString_BecomesUtcInstant(string text, int expectedUtcHour)
        {
            var value = Assert.IsType<DateTime>(CoercedValue("logged_at", text));
            var toTheSecond = value.AddTicks(-(value.Ticks % TimeSpan.TicksPerSecond));

            Assert.Equal(new DateTime(2026, 1, 2, expectedUtcHour, 0, 0, DateTimeKind.Utc), toTheSecond);
            Assert.Equal(DateTimeKind.Utc, value.Kind);
        }

        [Theory]
        [InlineData("2026-01-02")]
        [InlineData("2026-01-02T00:00:00+08:00")]
        [DisplayName("A date string becomes the date written, without a time-zone conversion")]
        public void Coerce_DateString_KeepsTheWrittenDate(string text)
        {
            Assert.Equal(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Unspecified), CoercedValue("due_date", text));
        }

        [Fact]
        [DisplayName("Text fields keep their string values")]
        public void Coerce_TextFields_KeepStrings()
        {
            Assert.Equal("2026-01-02", CoercedValue(SysFields.Id, "2026-01-02"));
        }

        [Theory]
        [InlineData("09:30", "09:30")]
        [InlineData("9:30", "09:30")]
        [InlineData("", "")]
        [InlineData("  ", "")]
        [DisplayName("A time-of-day value is written in its fixed-width form, and an empty or blank one matches the rows not filled in")]
        public void Coerce_TimeOfDay_BecomesFixedWidth(string text, string expected)
        {
            Assert.Equal(expected, CoercedValue("start_time", text));
        }

        [Fact]
        [DisplayName("The time-of-day values of an In list and of Between are written in their fixed-width form")]
        public void Coerce_TimeOfDayInAndBetween_BecomeFixedWidth()
        {
            var filter = FilterGroup.All(
                new FilterCondition("start_time", ComparisonOperator.In, new List<string> { "8:00", "17:30" }),
                new FilterCondition("start_time", ComparisonOperator.Between, "8:00", "9:15"));

            var group = Assert.IsType<FilterGroup>(FilterValueCoercion.Coerce(filter, Table()));

            var inValues = Assert.IsType<object?[]>(((FilterCondition)group.Nodes[0]).Value);
            Assert.Equal(["08:00", "17:30"], inValues);
            var between = (FilterCondition)group.Nodes[1];
            Assert.Equal("08:00", between.Value);
            Assert.Equal("09:15", between.SecondValue);
        }

        [Theory]
        [InlineData("25:00")]
        [InlineData("noon")]
        [InlineData("09:30:15")]
        [DisplayName("A value that is not a time of day is refused as an invalid request naming the field")]
        public void Coerce_InvalidTimeOfDay_IsRefused(string text)
        {
            var ex = Assert.Throws<InvalidRequestValueException>(() => CoercedValue("start_time", text));

            Assert.Contains("start_time", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The elements of an In list and both Between values are converted, from any list type")]
        public void Coerce_InAndBetween_ConvertEveryValue()
        {
            var filter = FilterGroup.All(
                new FilterCondition("qty", ComparisonOperator.In, new List<string> { "1", "2" }),
                new FilterCondition("amount", ComparisonOperator.Between, "1.5", "2.5"));

            var group = Assert.IsType<FilterGroup>(FilterValueCoercion.Coerce(filter, Table()));

            Assert.Equal(new object[] { 1, 2 }, Assert.IsType<object?[]>(((FilterCondition)group.Nodes[0]).Value));
            var between = (FilterCondition)group.Nodes[1];
            Assert.Equal(1.5m, between.Value);
            Assert.Equal(2.5m, between.SecondValue);
        }

        [Theory]
        [InlineData(ComparisonOperator.Like)]
        [InlineData(ComparisonOperator.Contains)]
        [InlineData(ComparisonOperator.StartsWith)]
        [InlineData(ComparisonOperator.EndsWith)]
        [DisplayName("The LIKE operators keep their string values, because they match text")]
        public void Coerce_LikeOperators_KeepStrings(ComparisonOperator op)
        {
            var coerced = (FilterCondition)FilterValueCoercion.Coerce(new FilterCondition("qty", op, "4"), Table());

            Assert.Equal("4", coerced.Value);
        }

        [Fact]
        [DisplayName("A string that is not valid for its field's type is refused with the field's name")]
        public void Coerce_InvalidValue_Throws()
        {
            var ex = Assert.Throws<InvalidRequestValueException>(() => CoercedValue(SysFields.RowId, "not-a-guid"));

            Assert.Contains(SysFields.RowId, ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("not-a-guid", ex.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("logged_at", "01/02/2026")]
        [InlineData("logged_at", "12:00")]
        [InlineData("due_date", "Jan 2 2026")]
        [InlineData("amount", "1,0,0")]
        [InlineData("amount", "10-")]
        [InlineData("active", "1")]
        [InlineData("qty", "")]
        [DisplayName("Text a database might once have converted is refused: non-ISO dates, grouped or trailing-sign numbers, 1 for a boolean, an empty number")]
        public void Coerce_LenientForms_AreRefused(string field, string text)
        {
            Assert.Throws<InvalidRequestValueException>(() => CoercedValue(field, text));
        }

        [Fact]
        [DisplayName("The company column keeps its value whatever type a form declares for it")]
        public void Coerce_CompanyColumn_IsLeftAlone()
        {
            var schema = new FormSchema("Doc", "Doc") { CategoryId = DbCategoryIds.Company };
            var table = schema.Tables!.Add("Doc", "Doc");
            table.Fields!.Add(SysFields.CompanyId, "Company", FieldDbType.Guid);

            var coerced = (FilterCondition)FilterValueCoercion.Coerce(
                FilterCondition.Equal(SysFields.CompanyId, "acme"), table);

            Assert.Equal("acme", coerced.Value);
        }

        [Fact]
        [DisplayName("A DELETE still names an unsupported relation field before it reads the field's value")]
        public void DeleteCommandBuilder_RelationFieldWithBadValue_ReportsTheField()
        {
            var schema = Table().Collection!.Owner as FormSchema;
            var table = schema!.Tables!["Doc"];
            table.Fields!.Add(new FormField("ref_uid", "Ref", FieldDbType.Guid) { Type = FieldType.RelationField });
            var builder = new DeleteCommandBuilder(schema, DatabaseType.PostgreSQL);

            Assert.Throws<NotSupportedException>(() => builder.Build("Doc", FilterCondition.Equal("ref_uid", "not-a-guid")));
        }

        [Fact]
        [DisplayName("The caller's filter is not changed, and typed values pass through untouched")]
        public void Coerce_DoesNotChangeTheInput()
        {
            var original = new FilterCondition("qty", ComparisonOperator.Equal, "7");
            var typed = new FilterCondition(SysFields.RowId, ComparisonOperator.Equal, s_guid);

            FilterValueCoercion.Coerce(original, Table());

            Assert.Equal("7", original.Value);
            Assert.Equal(s_guid, ((FilterCondition)FilterValueCoercion.Coerce(typed, Table())).Value);
        }

        [Fact]
        [DisplayName("A DELETE built from a filter with a string Guid binds a Guid parameter")]
        public void DeleteCommandBuilder_StringGuid_BindsGuid()
        {
            var schema = Table().Collection!.Owner as FormSchema;
            var builder = new DeleteCommandBuilder(schema!, DatabaseType.PostgreSQL);

            var spec = builder.Build("Doc", FilterCondition.Equal(SysFields.RowId, s_guid.ToString()));

            Assert.Equal(s_guid, spec.Parameters[0].Value);
        }
    }
}
