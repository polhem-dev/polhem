using System.ComponentModel;
using System.Data;
using Polhem.Business.Permission;
using Polhem.Definition.Filters;

namespace Polhem.Business.UnitTests.Permission
{
    /// <summary>
    /// <see cref="ScopeFilterEvaluator"/> decides whether a new or changed row lies inside the record scope that the
    /// save path checks it against, with the filter shapes <see cref="ScopeResolver"/> produces.
    /// </summary>
    public class ScopeFilterEvaluatorTests
    {
        private const string Dept = "dept_rowid";
        private const string Owner = "owner_rowid";
        private static readonly Guid s_dept = Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff");
        private static readonly Guid s_other = Guid.Parse("11111111-2222-3333-4444-555555555555");

        private static DataRow Row(params (string Column, Type Type, object? Value)[] columns)
        {
            var table = new DataTable("t");
            foreach (var (column, type, _) in columns) { table.Columns.Add(column, type); }
            var row = table.NewRow();
            foreach (var (column, _, value) in columns) { row[column] = value ?? DBNull.Value; }
            table.Rows.Add(row);
            return row;
        }

        private static bool Matches(FilterNode filter, DataRow row)
            => ScopeFilterEvaluator.Matches(filter, row, DataRowVersion.Current);

        [Theory]
        [InlineData("Guid")]
        [InlineData("String")]
        [InlineData("Bytes")]
        [DisplayName("A department Equal condition matches the same id stored as a Guid, a string or an Oracle byte array")]
        public void Equal_Guid_MatchesEachStorageForm(string storageForm)
        {
            (Type columnType, object stored) = storageForm switch
            {
                "Guid" => (typeof(Guid), (object)s_dept),
                "String" => (typeof(string), s_dept.ToString()),
                _ => (typeof(byte[]), s_dept.ToByteArray()),
            };

            Assert.True(Matches(FilterCondition.Equal(Dept, s_dept), Row((Dept, columnType, stored))));
        }

        [Fact]
        [DisplayName("A department Equal condition refuses a row of another department")]
        public void Equal_Guid_OtherDepartment_Refused()
        {
            Assert.False(Matches(FilterCondition.Equal(Dept, s_dept), Row((Dept, typeof(Guid), s_other))));
        }

        [Fact]
        [DisplayName("An Equal condition on Guid.Empty never matches, even a row whose column is empty")]
        public void Equal_EmptyGuid_NeverMatches()
        {
            var filter = FilterCondition.Equal(Dept, Guid.Empty);

            Assert.False(Matches(filter, Row((Dept, typeof(Guid), Guid.Empty))));
            Assert.False(Matches(filter, Row((Dept, typeof(Guid), null))));
        }

        [Fact]
        [DisplayName("An Or group admits a row that matches any one of its conditions, as the own-or-department scope does")]
        public void OrGroup_AnyConditionMatches_Admits()
        {
            var me = Guid.NewGuid();
            var filter = FilterGroup.Any(FilterCondition.In(Owner, [me]), FilterCondition.Equal(Dept, s_dept));

            Assert.True(Matches(filter, Row((Owner, typeof(Guid), Guid.NewGuid()), (Dept, typeof(Guid), s_dept))));
            Assert.True(Matches(filter, Row((Owner, typeof(Guid), me), (Dept, typeof(Guid), s_other))));
            Assert.False(Matches(filter, Row((Owner, typeof(Guid), Guid.NewGuid()), (Dept, typeof(Guid), s_other))));
        }

        [Fact]
        [DisplayName("An And group admits a row only when every condition matches")]
        public void AndGroup_RequiresEveryCondition()
        {
            var me = Guid.NewGuid();
            var filter = FilterGroup.All(FilterCondition.In(Owner, [me]), FilterCondition.Equal(Dept, s_dept));

            Assert.True(Matches(filter, Row((Owner, typeof(Guid), me), (Dept, typeof(Guid), s_dept))));
            Assert.False(Matches(filter, Row((Owner, typeof(Guid), me), (Dept, typeof(Guid), s_other))));
        }

        [Fact]
        [DisplayName("An In condition matches a department of the subtree and refuses one outside it")]
        public void In_Subtree_MatchesMembersOnly()
        {
            var filter = FilterCondition.In(Dept, [s_dept, Guid.NewGuid()]);

            Assert.True(Matches(filter, Row((Dept, typeof(string), s_dept.ToString()))));
            Assert.False(Matches(filter, Row((Dept, typeof(Guid), s_other))));
        }

        [Fact]
        [DisplayName("An In condition with an empty list, the deny-all scope, refuses every row")]
        public void In_EmptyList_RefusesEveryRow()
        {
            Assert.False(Matches(FilterCondition.In(Dept, []), Row((Dept, typeof(Guid), s_dept))));
        }

        [Theory]
        [InlineData("NotEqual")]
        [InlineData("Contains")]
        [InlineData("StartsWith")]
        [InlineData("Between")]
        [DisplayName("An operator the evaluator does not understand refuses the row, even where the database would admit it")]
        public void UnsupportedOperator_RefusesRow(string operatorName)
        {
            var condition = operatorName switch
            {
                "NotEqual" => FilterCondition.NotEqual(Dept, s_other),
                "Contains" => FilterCondition.Contains(Dept, string.Empty),
                "StartsWith" => FilterCondition.StartsWith(Dept, string.Empty),
                _ => FilterCondition.Between(Dept, Guid.Empty, s_dept),
            };

            Assert.False(Matches(condition, Row((Dept, typeof(string), s_dept.ToString()))));
        }

        [Fact]
        [DisplayName("A scope column the row does not carry is tested as empty, so it does not match")]
        public void MissingColumn_TestedAsEmpty()
        {
            var row = Row(("other", typeof(string), "x"));

            Assert.False(Matches(FilterCondition.Equal(Dept, s_dept), row));
            Assert.False(Matches(FilterCondition.In(Dept, [s_dept]), row));
        }
    }
}
