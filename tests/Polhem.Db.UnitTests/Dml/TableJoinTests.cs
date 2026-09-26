using System.ComponentModel;
using Polhem.Db.Dml;

namespace Polhem.Db.UnitTests.Dml
{
    /// <summary>
    /// Tests for the <c>TableJoin</c> property defaults, reading and writing Key, and the ToString output format.
    /// </summary>
    public class TableJoinTests
    {
        [Fact]
        [DisplayName("TableJoin defaults to JoinType Left and empty strings for the other string properties")]
        public void Defaults_AreLeftJoinAndEmptyStrings()
        {
            var join = new TableJoin();

            Assert.Equal(JoinType.Left, join.JoinType);
            Assert.Equal(string.Empty, join.LeftTable);
            Assert.Equal(string.Empty, join.LeftAlias);
            Assert.Equal(string.Empty, join.LeftField);
            Assert.Equal(string.Empty, join.RightTable);
            Assert.Equal(string.Empty, join.RightAlias);
            Assert.Equal(string.Empty, join.RightField);
        }

        [Fact]
        [DisplayName("The Key property can be read and written and stays in sync with base.Key")]
        public void Key_IsReadWrite()
        {
            var join = new TableJoin { Key = "join1" };

            Assert.Equal("join1", join.Key);

            join.Key = "join2";
            Assert.Equal("join2", join.Key);
        }

        [Theory]
        [InlineData(JoinType.Left, "LEFT JOIN tb_detail D ON M.id = D.main_id")]
        [InlineData(JoinType.Inner, "INNER JOIN tb_detail D ON M.id = D.main_id")]
        [InlineData(JoinType.Right, "RIGHT JOIN tb_detail D ON M.id = D.main_id")]
        [InlineData(JoinType.Full, "FULL JOIN tb_detail D ON M.id = D.main_id")]
        [DisplayName("ToString produces the JOIN syntax with the keyword of the JoinType")]
        public void ToString_FormatsAccordingToJoinType(JoinType joinType, string expected)
        {
            var join = new TableJoin
            {
                JoinType = joinType,
                LeftTable = "tb_main",
                LeftAlias = "M",
                LeftField = "id",
                RightTable = "tb_detail",
                RightAlias = "D",
                RightField = "main_id"
            };

            Assert.Equal(expected, join.ToString());
        }
    }
}
