using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Collections;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Tests.Shared;
using Polhem.UI.Core.Criteria;
using Polhem.UI.Core.Lists;

namespace Polhem.UI.Core.UnitTests.Criteria
{
    public class ColumnFilterCriteriaTests
    {
        private static readonly DateOnly s_today = new(2026, 3, 15);

        private static FormField Field(FieldDbType type) => new("f", "F", type);

        private static List<FilterCondition> Conditions(QueryCriteria criteria)
            => GetListFilterConverter.ToFilter(criteria, s_today, DayOfWeek.Monday) switch
            {
                FilterCondition condition => [condition],
                FilterGroup group => [.. group.Nodes!.Cast<FilterCondition>()],
                _ => [],
            };

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
            => Assert.Equal(expected, ColumnFilterCriteria.GetKind(new LayoutColumn("f", "F", ControlType.TextEdit), Field(type)));

        [Fact]
        [DisplayName("A drop-down column with listed values filters by one of them, and an unknown column offers none")]
        public void GetKind_ChoiceAndUnknown()
        {
            var field = Field(FieldDbType.String);
            field.ListItems!.Add("A", "Alpha");

            Assert.Equal(ColumnFilterKind.Choice, ColumnFilterCriteria.GetKind(new LayoutColumn("f", "F", ControlType.DropDownEdit), field));
            Assert.Equal(ColumnFilterKind.None, ColumnFilterCriteria.GetKind(new LayoutColumn("f", "F", ControlType.TextEdit), null));
        }

        [Fact]
        [DisplayName("Text becomes a contains condition, and the column shows what it holds")]
        public void TrySet_Text_IsContains()
        {
            var filters = new ColumnFilterCriteria(new QueryCriteria());

            Assert.True(filters.TrySet(Field(FieldDbType.String), ColumnFilterKind.Text, " Ali ", null));

            var condition = Assert.Single(Conditions(filters.Criteria));
            Assert.Equal(ComparisonOperator.Contains, condition.Operator);
            Assert.Equal("Ali", condition.Value);
            Assert.Equal(("Ali", string.Empty), filters.GetEntered("f"));
            Assert.True(filters.IsFiltered("F"));
        }

        [Fact]
        [DisplayName("A number range uses both ends, read and shown in the user's culture")]
        public void TrySet_NumberRange_ReadsCulture()
        {
            using var culture = new CultureScope("de-DE");
            var filters = new ColumnFilterCriteria(new QueryCriteria());

            Assert.True(filters.TrySet(Field(FieldDbType.Decimal), ColumnFilterKind.Number, "1,5", "2,5"));

            var conditions = Conditions(filters.Criteria);
            Assert.Equal(ComparisonOperator.GreaterThanOrEqual, conditions[0].Operator);
            Assert.Equal(1.5m, conditions[0].Value);
            Assert.Equal(ComparisonOperator.LessThanOrEqual, conditions[1].Operator);
            Assert.Equal(2.5m, conditions[1].Value);
            Assert.Equal(("1,5", "2,5"), filters.GetEntered("f"));
        }

        [Fact]
        [DisplayName("One end of a range is enough")]
        public void TrySet_OpenRange_HasOneCondition()
        {
            var filters = new ColumnFilterCriteria(new QueryCriteria());

            Assert.True(filters.TrySet(Field(FieldDbType.Integer), ColumnFilterKind.Number, null, "9"));

            Assert.Equal(ComparisonOperator.LessThanOrEqual, Assert.Single(Conditions(filters.Criteria)).Operator);
        }

        [Fact]
        [DisplayName("A calendar day travels as a DateOnly, which the time zone conversion never shifts")]
        public void TrySet_Date_IsDateOnly()
        {
            var filters = new ColumnFilterCriteria(new QueryCriteria());

            Assert.True(filters.TrySet(Field(FieldDbType.Date), ColumnFilterKind.Date, "2026-03-09", null));

            Assert.Equal(new DateOnly(2026, 3, 9), Assert.Single(Conditions(filters.Criteria)).Value);
            Assert.Equal(("2026-03-09", string.Empty), filters.GetEntered("f"));
        }

        [Fact]
        [DisplayName("The end of a date and time range includes the whole day")]
        public void TrySet_DateTimeEnd_IncludesTheDay()
        {
            var filters = new ColumnFilterCriteria(new QueryCriteria());

            Assert.True(filters.TrySet(Field(FieldDbType.DateTime), ColumnFilterKind.DateTime, null, "2026-03-09"));

            var condition = Assert.Single(Conditions(filters.Criteria));
            Assert.Equal(ComparisonOperator.LessThan, condition.Operator);
            var end = Assert.IsType<DateTime>(condition.Value);
            Assert.Equal(new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Unspecified), end);
        }

        [Fact]
        [DisplayName("A choice over a number field travels as a number, and yes or no as a Boolean")]
        public void TrySet_ChoiceAndBoolean_AreTyped()
        {
            var filters = new ColumnFilterCriteria(new QueryCriteria());
            var level = new FormField("level", "Level", FieldDbType.Integer);
            level.ListItems!.Add("2", "Two");

            Assert.True(filters.TrySet(level, ColumnFilterKind.Choice, "2", null));
            Assert.True(filters.TrySet(new FormField("active", "Active", FieldDbType.Boolean), ColumnFilterKind.Boolean, "True", null));

            var conditions = Conditions(filters.Criteria);
            Assert.Equal(2L, conditions[0].Value);
            Assert.Equal(true, conditions[1].Value);
            Assert.Equal(("2", string.Empty), filters.GetEntered("level"));
            Assert.Equal((bool.TrueString, string.Empty), filters.GetEntered("active"));
        }

        [Fact]
        [DisplayName("A value that cannot be read is refused and leaves the criteria as they were")]
        public void TrySet_Invalid_KeepsPrevious()
        {
            var filters = new ColumnFilterCriteria(new QueryCriteria());
            var field = Field(FieldDbType.Integer);
            filters.TrySet(field, ColumnFilterKind.Number, "1", null);

            Assert.False(filters.TrySet(field, ColumnFilterKind.Number, "abc", null));
            Assert.False(ColumnFilterCriteria.CanRead(field, ColumnFilterKind.Number, "abc", null));

            Assert.Single(filters.Criteria.Rows);
            Assert.Equal(1m, Assert.Single(Conditions(filters.Criteria)).Value);
        }

        [Fact]
        [DisplayName("Setting a column again replaces its own row, and an empty entry removes it")]
        public void TrySet_SameColumn_ReplacesThenRemoves()
        {
            var filters = new ColumnFilterCriteria(new QueryCriteria());
            var field = Field(FieldDbType.String);
            filters.TrySet(field, ColumnFilterKind.Text, "a", null);

            filters.TrySet(field, ColumnFilterKind.Text, "b", null);

            Assert.Equal("b", Assert.Single(filters.Criteria.Rows).Value);

            filters.TrySet(field, ColumnFilterKind.Text, " ", null);

            Assert.Empty(filters.Criteria.Rows);
            Assert.False(filters.IsFiltered("f"));
        }

        [Fact]
        [DisplayName("A condition the pane added on the same field stays, and still marks the column filtered")]
        public void TrySet_PaneRowOnSameField_IsKept()
        {
            var criteria = new QueryCriteria();
            criteria.Add(new CriteriaField("f", "F", CriteriaFieldKind.Text, FieldDbType.String)).SetValue("pane");
            var filters = new ColumnFilterCriteria(criteria);

            Assert.True(filters.IsFiltered("f"));
            Assert.Equal((string.Empty, string.Empty), filters.GetEntered("f"));

            filters.TrySet(Field(FieldDbType.String), ColumnFilterKind.Text, "column", null);
            filters.TrySet(Field(FieldDbType.String), ColumnFilterKind.Text, string.Empty, null);

            Assert.Equal("pane", Assert.Single(criteria.Rows).Value);
        }

        [Fact]
        [DisplayName("A column's row removed in the pane or by its chip no longer shows in the column")]
        public void GetEntered_RowRemovedElsewhere_IsEmpty()
        {
            var filters = new ColumnFilterCriteria(new QueryCriteria());
            filters.TrySet(Field(FieldDbType.String), ColumnFilterKind.Text, "a", null);

            filters.Criteria.Remove(filters.Criteria.Rows[0]);

            Assert.Equal((string.Empty, string.Empty), filters.GetEntered("f"));
            Assert.False(filters.IsFiltered("f"));
        }
    }
}
