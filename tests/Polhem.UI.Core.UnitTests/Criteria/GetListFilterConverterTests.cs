using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Filters;
using Polhem.UI.Core.Criteria;

namespace Polhem.UI.Core.UnitTests.Criteria
{
    public class GetListFilterConverterTests
    {
        private static readonly DateOnly s_today = new(2026, 11, 17);

        private static FilterNode? Convert(QueryCriteria criteria)
            => GetListFilterConverter.ToFilter(criteria, s_today, DayOfWeek.Monday);

        private static CriteriaField Field(CriteriaFieldKind kind, FieldDbType dbType, string name = "f")
            => new(name, name, kind, dbType);

        private static FilterCondition[] Conditions(FilterNode? node) => node switch
        {
            FilterCondition c => [c],
            FilterGroup g => [.. g.Nodes.Cast<FilterCondition>()],
            _ => [],
        };

        [Fact]
        [DisplayName("No rows, or rows with nothing entered, give no filter")]
        public void ToFilter_NothingEntered_ReturnsNull()
        {
            var criteria = new QueryCriteria();
            Assert.Null(Convert(criteria));

            criteria.Add(Field(CriteriaFieldKind.Text, FieldDbType.String));
            Assert.Null(Convert(criteria));
        }

        [Fact]
        [DisplayName("A single condition is returned without a group")]
        public void ToFilter_OneCondition_NoGroup()
        {
            var criteria = new QueryCriteria();
            criteria.Add(Field(CriteriaFieldKind.Text, FieldDbType.String, "name")).SetValue("Lee");

            var condition = Assert.IsType<FilterCondition>(Convert(criteria));

            Assert.Equal("name", condition.FieldName);
            Assert.Equal(ComparisonOperator.Contains, condition.Operator);
            Assert.Equal("Lee", condition.Value);
        }

        [Fact]
        [DisplayName("Several rows, the same field included, are combined with AND")]
        public void ToFilter_SeveralRows_AndGroup()
        {
            var criteria = new QueryCriteria();
            var name = Field(CriteriaFieldKind.Text, FieldDbType.String, "name");
            criteria.Add(name).SetValue("Lee");
            criteria.Add(name, CriteriaOperator.StartsWith).SetValue("A");

            var group = Assert.IsType<FilterGroup>(Convert(criteria));

            Assert.Equal(LogicalOperator.And, group.Operator);
            Assert.Equal([ComparisonOperator.Contains, ComparisonOperator.StartsWith], Conditions(group).Select(c => c.Operator));
        }

        [Fact]
        [DisplayName("A code range becomes inclusive bounds, and an open end produces no bound")]
        public void ToFilter_CodeRange_InclusiveBounds()
        {
            var both = new QueryCriteria();
            both.Add(Field(CriteriaFieldKind.Code, FieldDbType.String, "sys_id")).SetRange("C001", "C099");
            var open = new QueryCriteria();
            open.Add(Field(CriteriaFieldKind.Code, FieldDbType.String, "sys_id")).SetRange(null, "C099");

            var bounds = Conditions(Convert(both));
            var upper = Assert.Single(Conditions(Convert(open)));

            Assert.Equal([(ComparisonOperator.GreaterThanOrEqual, (object?)"C001"), (ComparisonOperator.LessThanOrEqual, "C099")],
                bounds.Select(c => (c.Operator, c.Value)));
            Assert.Equal((ComparisonOperator.LessThanOrEqual, (object?)"C099"), (upper.Operator, upper.Value));
        }

        [Fact]
        [DisplayName("A relative date range on a date field resolves against today")]
        public void ToFilter_RelativeDate_ResolvesToDays()
        {
            var criteria = new QueryCriteria();
            criteria.Add(Field(CriteriaFieldKind.Date, FieldDbType.Date, "order_date"))
                .SetRelativeDate(new RelativeDate(RelativeDateKind.ThisMonth));

            var bounds = Conditions(Convert(criteria));

            Assert.Equal([(ComparisonOperator.GreaterThanOrEqual, (object?)new DateOnly(2026, 11, 1)),
                (ComparisonOperator.LessThanOrEqual, new DateOnly(2026, 11, 30))], bounds.Select(c => (c.Operator, c.Value)));
        }

        [Fact]
        [DisplayName("A date-time range covers its end day whole with unspecified-kind instants")]
        public void ToFilter_DateTimeRange_EndsBeforeNextDay()
        {
            var criteria = new QueryCriteria();
            criteria.Add(Field(CriteriaFieldKind.DateTime, FieldDbType.DateTime, "shipped_at"))
                .SetRange(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 17));

            var bounds = Conditions(Convert(criteria));

            Assert.Equal([ComparisonOperator.GreaterThanOrEqual, ComparisonOperator.LessThan], bounds.Select(c => c.Operator));
            Assert.Equal(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Unspecified), bounds[0].Value);
            Assert.Equal(new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Unspecified), bounds[1].Value);
            Assert.All(bounds, c => Assert.Equal(DateTimeKind.Unspecified, ((DateTime)c.Value!).Kind));
        }

        [Fact]
        [DisplayName("Equal on a date-time field matches the whole day")]
        public void ToFilter_DateTimeEqual_CoversDay()
        {
            var criteria = new QueryCriteria();
            criteria.Add(Field(CriteriaFieldKind.DateTime, FieldDbType.DateTime, "shipped_at"), CriteriaOperator.Equal)
                .SetValue(new DateOnly(2026, 11, 17));

            var bounds = Conditions(Convert(criteria));

            Assert.Equal([(ComparisonOperator.GreaterThanOrEqual, (object?)new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Unspecified)),
                (ComparisonOperator.LessThan, new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Unspecified))], bounds.Select(c => (c.Operator, c.Value)));
        }

        [Fact]
        [DisplayName("Equal on a number field compares the decimal value")]
        public void ToFilter_NumberEqual_Decimal()
        {
            var criteria = new QueryCriteria();
            criteria.Add(Field(CriteriaFieldKind.Number, FieldDbType.Currency, "freight"), CriteriaOperator.Equal).SetValue(12.5);

            var condition = Assert.IsType<FilterCondition>(Convert(criteria));

            Assert.Equal((ComparisonOperator.Equal, (object?)12.5m), (condition.Operator, condition.Value));
        }

        [Fact]
        [DisplayName("An In row with one value becomes Equal and with several becomes In over an object array")]
        public void ToFilter_InRow_EqualOrIn()
        {
            var shipper = Field(CriteriaFieldKind.Relation, FieldDbType.Guid, "shipper_rowid");
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var one = new QueryCriteria();
            one.Add(shipper).TryAddValue(a, "Speedy");
            var two = new QueryCriteria();
            var row = two.Add(shipper);
            row.TryAddValue(a, "Speedy");
            row.TryAddValue(b, "United");

            var equal = Assert.IsType<FilterCondition>(Convert(one));
            var @in = Assert.IsType<FilterCondition>(Convert(two));

            Assert.Equal((ComparisonOperator.Equal, (object?)a), (equal.Operator, equal.Value));
            Assert.Equal(ComparisonOperator.In, @in.Operator);
            Assert.Equal(new object[] { a, b }, Assert.IsType<object[]>(@in.Value));
        }

        [Fact]
        [DisplayName("A yes / no row compares the boolean")]
        public void ToFilter_Boolean_Equal()
        {
            var criteria = new QueryCriteria();
            criteria.Add(Field(CriteriaFieldKind.Boolean, FieldDbType.Boolean, "discontinued")).SetValue(false);

            var condition = Assert.IsType<FilterCondition>(Convert(criteria));

            Assert.Equal((ComparisonOperator.Equal, (object?)false), (condition.Operator, condition.Value));
        }

        [Fact]
        [DisplayName("A required row with nothing entered makes the conversion fail")]
        public void ToFilter_MissingRequired_Throws()
        {
            var criteria = new QueryCriteria();
            criteria.Add(Field(CriteriaFieldKind.Date, FieldDbType.Date, "order_date"), kind: CriteriaRowKind.Required);

            Assert.Throws<InvalidOperationException>(() => Convert(criteria));
        }
    }
}
