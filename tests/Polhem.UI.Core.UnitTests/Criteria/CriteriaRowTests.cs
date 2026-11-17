using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.UI.Core.Criteria;

namespace Polhem.UI.Core.UnitTests.Criteria
{
    public class CriteriaRowTests
    {
        private static CriteriaRow NewRow(CriteriaFieldKind kind, FieldDbType dbType, CriteriaOperator? op = null, bool lockOperator = false)
            => new QueryCriteria().Add(new CriteriaField("f", "F", kind, dbType), op, lockOperator: lockOperator);

        [Fact]
        [DisplayName("SetValue trims text and treats blank text as nothing entered")]
        public void SetValue_Text_TrimsAndBlankClears()
        {
            var row = NewRow(CriteriaFieldKind.Text, FieldDbType.String);

            row.SetValue("  Lee ");
            Assert.Equal("Lee", row.Value);
            Assert.True(row.HasValue);

            row.SetValue("   ");
            Assert.Null(row.Value);
            Assert.False(row.HasValue);
        }

        [Fact]
        [DisplayName("A number field converts any numeric type to decimal and refuses text")]
        public void SetValue_Number_ConvertsToDecimal()
        {
            var row = NewRow(CriteriaFieldKind.Number, FieldDbType.Integer, CriteriaOperator.Equal);

            row.SetValue(42);
            Assert.Equal(42m, row.Value);

            Assert.Throws<ArgumentException>(() => row.SetValue("42"));
        }

        [Fact]
        [DisplayName("A date field keeps the day of a DateTime and refuses other types")]
        public void SetValue_Date_TakesDay()
        {
            var row = NewRow(CriteriaFieldKind.Date, FieldDbType.Date, CriteriaOperator.Equal);

            row.SetValue(new DateTime(2026, 11, 17, 15, 30, 0, DateTimeKind.Unspecified));
            Assert.Equal(new DateOnly(2026, 11, 17), row.Value);

            Assert.Throws<ArgumentException>(() => row.SetValue(5));
        }

        [Fact]
        [DisplayName("A yes / no row left unset has nothing entered")]
        public void SetValue_BooleanNull_HasNoValue()
        {
            var row = NewRow(CriteriaFieldKind.Boolean, FieldDbType.Boolean);

            row.SetValue(true);
            Assert.True(row.HasValue);
            row.SetValue(null);
            Assert.False(row.HasValue);
        }

        [Fact]
        [DisplayName("Each setter refuses an operator it does not serve")]
        public void Setters_WrongOperator_Throw()
        {
            var range = NewRow(CriteriaFieldKind.Number, FieldDbType.Integer, CriteriaOperator.Range);
            var single = NewRow(CriteriaFieldKind.Number, FieldDbType.Integer, CriteriaOperator.Equal);
            var choice = NewRow(CriteriaFieldKind.Choice, FieldDbType.String);

            Assert.Throws<InvalidOperationException>(() => range.SetValue(1));
            Assert.Throws<InvalidOperationException>(() => single.SetRange(1, 2));
            Assert.Throws<InvalidOperationException>(() => single.TryAddValue(1, "1"));
            Assert.Throws<InvalidOperationException>(() => choice.SetRange("A", "B"));
        }

        [Fact]
        [DisplayName("A range may leave either end open and still has something entered")]
        public void SetRange_OneEnd_HasValue()
        {
            var row = NewRow(CriteriaFieldKind.Code, FieldDbType.String, CriteriaOperator.Range);

            row.SetRange("C001", null);

            Assert.Equal("C001", row.From);
            Assert.Null(row.To);
            Assert.True(row.HasValue);
        }

        [Fact]
        [DisplayName("A relative date replaces the days entered, and days entered replace a relative date")]
        public void SetRelativeDate_And_SetRange_ReplaceEachOther()
        {
            var row = NewRow(CriteriaFieldKind.Date, FieldDbType.Date);

            row.SetRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
            row.SetRelativeDate(new RelativeDate(RelativeDateKind.ThisMonth));
            Assert.Null(row.From);
            Assert.Null(row.To);
            Assert.Equal(RelativeDateKind.ThisMonth, row.RelativeDate?.Kind);

            row.SetRange(new DateOnly(2026, 2, 1), null);
            Assert.Null(row.RelativeDate);
            Assert.Equal(new DateOnly(2026, 2, 1), row.From);
        }

        [Fact]
        [DisplayName("Only a date or date-time field takes a relative date")]
        public void SetRelativeDate_NumberField_Throws()
        {
            var row = NewRow(CriteriaFieldKind.Number, FieldDbType.Integer);

            Assert.Throws<InvalidOperationException>(() => row.SetRelativeDate(new RelativeDate(RelativeDateKind.Today)));
        }

        [Fact]
        [DisplayName("Changing between single-value operators keeps the text, and changing to Range clears it")]
        public void ChangeOperator_KeepsSingleValueOnly()
        {
            var row = NewRow(CriteriaFieldKind.Code, FieldDbType.String, CriteriaOperator.Contains);
            row.SetValue("A1");

            row.ChangeOperator(CriteriaOperator.Equal);
            Assert.Equal("A1", row.Value);

            row.ChangeOperator(CriteriaOperator.Range);
            Assert.False(row.HasValue);
        }

        [Fact]
        [DisplayName("A locked operator cannot change and an operator the field does not offer is refused")]
        public void ChangeOperator_LockedOrUnoffered_Throws()
        {
            var locked = NewRow(CriteriaFieldKind.Date, FieldDbType.Date, CriteriaOperator.Range, lockOperator: true);
            var text = NewRow(CriteriaFieldKind.Text, FieldDbType.String);

            Assert.Throws<InvalidOperationException>(() => locked.ChangeOperator(CriteriaOperator.Equal));
            Assert.Throws<ArgumentException>(() => text.ChangeOperator(CriteriaOperator.Range));
        }

        [Fact]
        [DisplayName("TryAddValue converts to the stored type, refuses what cannot convert and skips duplicates")]
        public void TryAddValue_ConvertsAndDeduplicates()
        {
            var guidRow = NewRow(CriteriaFieldKind.Relation, FieldDbType.Guid);
            var intRow = NewRow(CriteriaFieldKind.Choice, FieldDbType.Integer);
            var id = Guid.NewGuid();

            Assert.True(guidRow.TryAddValue(id.ToString(), "Alfreds"));
            Assert.True(guidRow.TryAddValue(id, "Alfreds"));
            Assert.False(guidRow.TryAddValue("not a guid", "x"));
            Assert.True(intRow.TryAddValue("2", "Shipped"));

            Assert.Equal([new CriteriaValue(id, "Alfreds")], guidRow.Values);
            Assert.Equal(2L, intRow.Values[0].Value);
        }

        [Fact]
        [DisplayName("RemoveValue takes a value out of an In row")]
        public void RemoveValue_RemovesMatchingValue()
        {
            var row = NewRow(CriteriaFieldKind.Choice, FieldDbType.String);
            row.TryAddValue("A", "Active");
            row.TryAddValue("C", "Closed");

            Assert.True(row.RemoveValue("A"));
            Assert.False(row.RemoveValue("A"));
            Assert.Equal(["C"], row.Values.Select(v => v.Value));
        }
    }
}
