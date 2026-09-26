using System.ComponentModel;
using Polhem.Db.Dml;
using Polhem.Definition.Database;
using Polhem.Definition.Sorting;

namespace Polhem.Db.UnitTests
{
    public class SortBuilderTests
    {
        [Fact]
        [DisplayName("Build returns an empty string for a null sort collection")]
        public void Build_NullSorts_ReturnsEmptyString()
        {
            var builder = new SortBuilder(DatabaseType.SQLServer);
            var result = builder.Build(null, null);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        [DisplayName("Build returns an empty string for an empty sort collection")]
        public void Build_EmptySorts_ReturnsEmptyString()
        {
            var builder = new SortBuilder(DatabaseType.SQLServer);
            var result = builder.Build([], null);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        [DisplayName("Build returns the ORDER BY clause for a single sort field")]
        public void Build_SingleSortItem_ReturnsCorrectOrderByClause()
        {
            var builder = new SortBuilder(DatabaseType.SQLServer);
            var sorts = new SortFieldCollection()
            {
                new SortField("Name", SortDirection.Asc)
            };
            var result = builder.Build(sorts, null);
            Assert.Equal("ORDER BY Name ASC", result);
        }

        [Fact]
        [DisplayName("Build returns the ORDER BY clause for several sort fields")]
        public void Build_MultipleSortItems_ReturnsCorrectOrderByClause()
        {
            var builder = new SortBuilder(DatabaseType.SQLServer);
            var sorts = new SortFieldCollection()
            {
                new SortField("Name", SortDirection.Asc),
                new SortField("Age", SortDirection.Desc)
            };
            var result = builder.Build(sorts, null);
            Assert.Equal("ORDER BY Name ASC, Age DESC", result);
        }

        [Fact]
        [DisplayName("Build returns the ORDER BY clause for a sort field containing a SQL expression")]
        public void Build_SortItemWithSqlExpression_ReturnsCorrectOrderByClause()
        {
            var builder = new SortBuilder(DatabaseType.SQLServer);
            var sorts = new SortFieldCollection()
            {
                new SortField("LEN(Name)", SortDirection.Desc)
            };
            var result = builder.Build(sorts, null);
            Assert.Equal("ORDER BY LEN(Name) DESC", result);
        }
    }
}
