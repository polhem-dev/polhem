using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Tests.Shared;
using Polhem.UI.Core.Connectors;

namespace Polhem.UI.Core.UnitTests.Connectors
{
    public class ListFilterStateTests
    {
        private static FormField Field(FieldDbType type) => new("f", "F", type);

        private static FilterCondition Single(ListFilterState state) => Assert.IsType<FilterCondition>(state.CreateFilter());

        [Theory]
        [InlineData(FieldDbType.String, ColumnFilterKind.Text)]
        [InlineData(FieldDbType.Text, ColumnFilterKind.None)]
        [InlineData(FieldDbType.Boolean, ColumnFilterKind.Boolean)]
        [InlineData(FieldDbType.Integer, ColumnFilterKind.Number)]
        [InlineData(FieldDbType.Currency, ColumnFilterKind.Number)]
        [InlineData(FieldDbType.Date, ColumnFilterKind.Date)]
        [InlineData(FieldDbType.DateTime, ColumnFilterKind.DateTime)]
        [InlineData(FieldDbType.Guid, ColumnFilterKind.None)]
        [InlineData(FieldDbType.Binary, ColumnFilterKind.None)]
        [InlineData(FieldDbType.Time, ColumnFilterKind.None)]
        [DisplayName("A field's type decides what a user can enter")]
        public void GetKind_FollowsType(FieldDbType type, ColumnFilterKind expected)
            => Assert.Equal(expected, ListFilterState.GetKind(new LayoutColumn("f", "F", ControlType.TextEdit), Field(type)));

        [Fact]
        [DisplayName("A column the schema does not know offers no filter")]
        public void GetKind_NoField_IsNone()
            => Assert.Equal(ColumnFilterKind.None, ListFilterState.GetKind(new LayoutColumn("f", "F", ControlType.TextEdit), null));

        [Fact]
        [DisplayName("Text becomes a contains condition")]
        public void TrySet_Text_IsContains()
        {
            var state = new ListFilterState();

            Assert.True(state.TrySet(Field(FieldDbType.String), ColumnFilterKind.Text, " Ali ", null));

            var condition = Single(state);
            Assert.Equal(ComparisonOperator.Contains, condition.Operator);
            Assert.Equal("Ali", condition.Value);
        }

        [Fact]
        [DisplayName("A number range uses both ends, read in the user's culture")]
        public void TrySet_NumberRange_ReadsCulture()
        {
            using var culture = new CultureScope("de-DE");
            var state = new ListFilterState();

            Assert.True(state.TrySet(Field(FieldDbType.Decimal), ColumnFilterKind.Number, "1,5", "2,5"));

            var nodes = Assert.IsType<FilterGroup>(state.CreateFilter()).Nodes!.Cast<FilterCondition>().ToList();
            Assert.Equal(ComparisonOperator.GreaterThanOrEqual, nodes[0].Operator);
            Assert.Equal(1.5m, nodes[0].Value);
            Assert.Equal(ComparisonOperator.LessThanOrEqual, nodes[1].Operator);
            Assert.Equal(2.5m, nodes[1].Value);
        }

        [Fact]
        [DisplayName("One end of a range is enough")]
        public void TrySet_OpenRange_HasOneCondition()
        {
            var state = new ListFilterState();

            Assert.True(state.TrySet(Field(FieldDbType.Integer), ColumnFilterKind.Number, null, "9"));

            Assert.Equal(ComparisonOperator.LessThanOrEqual, Single(state).Operator);
        }

        [Fact]
        [DisplayName("A calendar day travels as a DateOnly, which the time zone conversion never shifts")]
        public void TrySet_Date_IsDateOnly()
        {
            var state = new ListFilterState();

            Assert.True(state.TrySet(Field(FieldDbType.Date), ColumnFilterKind.Date, "2026-03-09", null));

            Assert.Equal(new DateOnly(2026, 3, 9), Single(state).Value);
        }

        [Fact]
        [DisplayName("The end of a date and time range includes the whole day")]
        public void TrySet_DateTimeEnd_IncludesTheDay()
        {
            var state = new ListFilterState();

            Assert.True(state.TrySet(Field(FieldDbType.DateTime), ColumnFilterKind.DateTime, null, "2026-03-09"));

            var condition = Single(state);
            Assert.Equal(ComparisonOperator.LessThan, condition.Operator);
            var end = Assert.IsType<DateTime>(condition.Value);
            Assert.Equal(new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Unspecified), end);
            Assert.Equal(DateTimeKind.Unspecified, end.Kind);
        }

        [Fact]
        [DisplayName("A choice over a number field travels as a number")]
        public void TrySet_NumberChoice_IsTyped()
        {
            var state = new ListFilterState();

            Assert.True(state.TrySet(Field(FieldDbType.Integer), ColumnFilterKind.Choice, "2", null));

            Assert.Equal(2L, Single(state).Value);
        }

        [Fact]
        [DisplayName("A value that cannot be read is refused and leaves the filter as it was")]
        public void TrySet_Invalid_KeepsPrevious()
        {
            var state = new ListFilterState();
            var field = Field(FieldDbType.Integer);
            state.TrySet(field, ColumnFilterKind.Number, "1", null);

            Assert.False(state.TrySet(field, ColumnFilterKind.Number, "abc", null));

            Assert.Equal(("1", string.Empty), state.GetEntered("f"));
            Assert.Equal(1m, Single(state).Value);
        }

        [Fact]
        [DisplayName("Empty values remove the column's filter, and no filter means none is sent")]
        public void TrySet_Empty_Removes()
        {
            var state = new ListFilterState();
            var field = Field(FieldDbType.String);
            state.TrySet(field, ColumnFilterKind.Text, "a", null);
            Assert.True(state.IsFiltered("F") || state.IsFiltered("f"));

            state.TrySet(field, ColumnFilterKind.Text, " ", null);

            Assert.False(state.HasFilters);
            Assert.Null(state.CreateFilter());
        }

        [Fact]
        [DisplayName("Two columns combine with AND")]
        public void CreateFilter_TwoColumns_AreAll()
        {
            var state = new ListFilterState();
            state.TrySet(new FormField("a", "A", FieldDbType.String), ColumnFilterKind.Text, "x", null);
            state.TrySet(new FormField("b", "B", FieldDbType.String), ColumnFilterKind.Text, "y", null);

            var group = Assert.IsType<FilterGroup>(state.CreateFilter());

            Assert.Equal(LogicalOperator.And, group.Operator);
            Assert.Equal(2, group.Nodes!.Count);
        }
    }
}
