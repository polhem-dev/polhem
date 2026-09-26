using System.ComponentModel;
using Polhem.Db.Dml;
using Polhem.Definition.Database;

namespace Polhem.Db.UnitTests
{
    public class FromBuilderTests
    {
        [Fact]
        [DisplayName("Build with joins=null produces only the FROM clause of the main table")]
        public void Build_NullJoins_ReturnsMainTableOnly()
        {
            var builder = new FromBuilder(DatabaseType.SQLServer);

            var result = builder.Build("st_user", null!);

            Assert.Equal("FROM [st_user] A", result);
        }

        [Fact]
        [DisplayName("Build with empty joins produces only the FROM clause of the main table")]
        public void Build_EmptyJoins_ReturnsMainTableOnly()
        {
            var builder = new FromBuilder(DatabaseType.SQLServer);

            var result = builder.Build("st_user", []);

            Assert.Equal("FROM [st_user] A", result);
        }

        [Fact]
        [DisplayName("Build with a single join produces a LEFT JOIN clause")]
        public void Build_SingleJoin_ProducesLeftJoinClause()
        {
            var joins = new TableJoinCollection
            {
                new TableJoin
                {
                    Key = "j1",
                    JoinType = JoinType.Left,
                    LeftTable = "st_user",
                    LeftAlias = "A",
                    LeftField = "dept_id",
                    RightTable = "st_dept",
                    RightAlias = "B",
                    RightField = "dept_id"
                }
            };

            var builder = new FromBuilder(DatabaseType.SQLServer);
            var result = builder.Build("st_user", joins);

            Assert.Contains("FROM [st_user] A", result);
            Assert.Contains("LEFT JOIN [st_dept] B ON A.[dept_id] = B.[dept_id]", result);
        }

        [Fact]
        [DisplayName("Build with several joins orders them by RightAlias")]
        public void Build_MultipleJoins_OrderedByRightAlias()
        {
            var joins = new TableJoinCollection
            {
                new TableJoin
                {
                    Key = "jc",
                    JoinType = JoinType.Left,
                    LeftTable = "main",
                    LeftAlias = "A",
                    LeftField = "f1",
                    RightTable = "tbl_c",
                    RightAlias = "C",
                    RightField = "f1"
                },
                new TableJoin
                {
                    Key = "jb",
                    JoinType = JoinType.Left,
                    LeftTable = "main",
                    LeftAlias = "A",
                    LeftField = "f2",
                    RightTable = "tbl_b",
                    RightAlias = "B",
                    RightField = "f2"
                }
            };

            var builder = new FromBuilder(DatabaseType.SQLServer);
            var result = builder.Build("main", joins);

            int posB = result.IndexOf("[tbl_b] B", StringComparison.Ordinal);
            int posC = result.IndexOf("[tbl_c] C", StringComparison.Ordinal);
            Assert.True(posB > 0 && posC > 0);
            Assert.True(posB < posC);
        }
    }
}
