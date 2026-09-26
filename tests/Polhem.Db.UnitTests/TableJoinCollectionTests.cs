using System.ComponentModel;
using Polhem.Db.Dml;

namespace Polhem.Db.UnitTests
{
    public class TableJoinCollectionTests
    {
        private static TableJoinCollection BuildSampleCollection()
        {
            var collection = new TableJoinCollection
            {
                new TableJoin
                {
                    Key = "join1",
                    LeftTable = "tb_main",
                    LeftAlias = "M",
                    LeftField = "id",
                    RightTable = "tb_detail",
                    RightAlias = "D",
                    RightField = "main_id"
                },
                new TableJoin
                {
                    Key = "join2",
                    LeftTable = "tb_main",
                    LeftAlias = "M",
                    LeftField = "user_id",
                    RightTable = "tb_user",
                    RightAlias = "U",
                    RightField = "id"
                }
            };
            return collection;
        }

        [Fact]
        [DisplayName("FindRightAlias returns null for a null rightAlias")]
        public void FindRightAlias_NullAlias_ReturnsNull()
        {
            var collection = BuildSampleCollection();

            Assert.Null(collection.FindRightAlias(null!));
        }

        [Fact]
        [DisplayName("FindRightAlias returns null for an empty rightAlias")]
        public void FindRightAlias_EmptyAlias_ReturnsNull()
        {
            var collection = BuildSampleCollection();

            Assert.Null(collection.FindRightAlias(string.Empty));
        }

        [Fact]
        [DisplayName("FindRightAlias returns the TableJoin with the matching alias")]
        public void FindRightAlias_Found_ReturnsMatchingJoin()
        {
            var collection = BuildSampleCollection();

            var join = collection.FindRightAlias("U");

            Assert.NotNull(join);
            Assert.Equal("tb_user", join!.RightTable);
        }

        [Fact]
        [DisplayName("FindRightAlias matches case-insensitively (the framework default IgnoreCase)")]
        public void FindRightAlias_CaseInsensitive_DifferentCaseFound()
        {
            // `FindRightAlias` uses `StringUtilities.IsEquals` (case-insensitive by default), so the lower-case input
            // "d" still finds the upper-case alias "D".
            var collection = BuildSampleCollection();

            var join = collection.FindRightAlias("d");

            Assert.NotNull(join);
        }

        [Fact]
        [DisplayName("FindRightAlias returns null when no alias matches")]
        public void FindRightAlias_NotFound_ReturnsNull()
        {
            var collection = BuildSampleCollection();

            Assert.Null(collection.FindRightAlias("X"));
        }

        [Fact]
        [DisplayName("FindRightAlias returns null on an empty collection")]
        public void FindRightAlias_EmptyCollection_ReturnsNull()
        {
            var collection = new TableJoinCollection();

            Assert.Null(collection.FindRightAlias("anything"));
        }
    }
}
