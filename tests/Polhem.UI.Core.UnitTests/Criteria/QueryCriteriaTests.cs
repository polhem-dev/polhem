using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.UI.Core.Criteria;

namespace Polhem.UI.Core.UnitTests.Criteria
{
    public class QueryCriteriaTests
    {
        private static readonly CriteriaField s_name = new("name", "Name", CriteriaFieldKind.Text, FieldDbType.String);
        private static readonly CriteriaField s_date = new("order_date", "Date", CriteriaFieldKind.Date, FieldDbType.Date);

        [Fact]
        [DisplayName("Add starts a row with the field's default operator and allows the same field twice")]
        public void Add_SameFieldTwice_AddsTwoRows()
        {
            var criteria = new QueryCriteria();

            var first = criteria.Add(s_name);
            criteria.Add(s_name);

            Assert.Equal(2, criteria.Rows.Count);
            Assert.Equal(CriteriaOperator.Contains, first.Operator);
            Assert.Equal(CriteriaRowKind.Optional, first.Kind);
        }

        [Fact]
        [DisplayName("A required row cannot be removed and an optional one can")]
        public void Remove_RequiredRow_IsRefused()
        {
            var criteria = new QueryCriteria();
            var required = criteria.Add(s_date, kind: CriteriaRowKind.Required);
            var optional = criteria.Add(s_name);

            Assert.False(criteria.Remove(required));
            Assert.True(criteria.Remove(optional));
            Assert.Equal([required], criteria.Rows);
        }

        [Fact]
        [DisplayName("Clear removes rows that are not required and clears what the required ones hold")]
        public void Clear_KeepsRequiredRowsEmpty()
        {
            var criteria = new QueryCriteria();
            var required = criteria.Add(s_date, kind: CriteriaRowKind.Required);
            required.SetRelativeDate(new RelativeDate(RelativeDateKind.ThisMonth));
            criteria.Add(s_name, kind: CriteriaRowKind.Default).SetValue("Lee");
            criteria.Add(s_name).SetValue("Kim");

            criteria.Clear();

            Assert.Equal([required], criteria.Rows);
            Assert.False(required.HasValue);
        }

        [Fact]
        [DisplayName("IsComplete is false until every required row has something entered")]
        public void IsComplete_TracksRequiredRows()
        {
            var criteria = new QueryCriteria();
            var required = criteria.Add(s_date, kind: CriteriaRowKind.Required, lockOperator: true);
            criteria.Add(s_name);

            Assert.False(criteria.IsComplete);
            Assert.Equal([required], criteria.MissingRequired);

            required.SetRange(new DateOnly(2026, 11, 1), null);

            Assert.True(criteria.IsComplete);
            Assert.Empty(criteria.MissingRequired);
        }

        [Fact]
        [DisplayName("ActiveCount counts only rows with something entered")]
        public void ActiveCount_CountsRowsWithValues()
        {
            var criteria = new QueryCriteria();
            criteria.Add(s_name).SetValue("Lee");
            criteria.Add(s_name);
            criteria.Add(s_date);

            Assert.Equal(1, criteria.ActiveCount);
        }
    }
}
