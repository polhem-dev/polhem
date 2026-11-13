using System.ComponentModel;
using Polhem.Definition.Organization;

namespace Polhem.Definition.UnitTests.Organization
{
    /// <summary>
    /// Edge cases of how <see cref="DepartmentTree"/> assembles flat department rows into a forest: missing input,
    /// orphans, a cycle above the row being attached, and an empty deserialized tree.
    /// </summary>
    public class DepartmentTreeEdgeTests
    {
        [Fact]
        [DisplayName("The constructor rejects a null company id")]
        public void Constructor_NullCompanyId_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new DepartmentTree(null!, []));
        }

        [Fact]
        [DisplayName("Null rows build an empty forest")]
        public void Constructor_NullRows_BuildsEmptyForest()
        {
            var tree = new DepartmentTree("C001", null!);

            Assert.Empty(tree.Roots!);
            Assert.Equal("C001", tree.GetKey());
        }

        [Fact]
        [DisplayName("Rows given as a lazy sequence build the same nesting, and a row whose parent is missing becomes a root")]
        public void Constructor_LazyRowsWithOrphan_BuildsForest()
        {
            var hq = Guid.NewGuid();
            var sales = Guid.NewGuid();
            var orphan = Guid.NewGuid();
            var rows = new[]
            {
                new DepartmentRow(hq, "HQ", "HQ", Guid.Empty, Guid.Empty),
                new DepartmentRow(sales, "SALES", "Sales", hq, Guid.Empty),
                new DepartmentRow(orphan, "LOST", "Lost", Guid.NewGuid(), Guid.Empty),
            }.Where(_ => true);

            var tree = new DepartmentTree("C001", rows);

            Assert.Equal([hq, orphan], tree.Roots!.Select(n => n.RowId));
            Assert.Equal([hq, sales], tree.GetSelfAndDescendants(hq));
        }

        [Fact]
        [DisplayName("A row whose ancestors loop among themselves becomes a root instead of joining the loop")]
        public void Constructor_CycleAboveRow_MakesRowARoot()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();

            var tree = new DepartmentTree("C001",
            [
                new DepartmentRow(a, "A", "A", b, Guid.Empty),
                new DepartmentRow(b, "B", "B", c, Guid.Empty),
                new DepartmentRow(c, "C", "C", b, Guid.Empty),
            ]);

            Assert.Contains(tree.Roots!, n => n.RowId == a);
            Assert.True(tree.Contains(a));
        }

        [Fact]
        [DisplayName("A tree created empty, as a deserializer does before filling it, contains no department")]
        public void Contains_EmptyTree_ReturnsFalse()
        {
            var tree = new DepartmentTree();

            Assert.False(tree.Contains(Guid.NewGuid()));
            Assert.Empty(tree.GetSelfAndDescendants(Guid.NewGuid()));
        }
    }
}
