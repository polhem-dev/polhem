using System.ComponentModel;
using System.Text.Json;
using Polhem.Base.Serialization;
using Polhem.Definition.Organization;

namespace Polhem.Definition.UnitTests.Organization
{
    /// <summary>
    /// Tests for the query logic of DepartmentTree (subtree, ancestors, cycle protection) and its triple serialization
    /// (XML / JSON; MessagePack is tested in Polhem.Api.Core, where its codec lives). Uses synthetic nodes, no database.
    /// </summary>
    public class DepartmentTreeTests
    {
        // Tree: HQ (root) -> SALES -> SALES1, plus ADMIN as a separate root.
        private static DepartmentTree Build(out Guid hq, out Guid sales, out Guid sales1, out Guid admin)
        {
            hq = Guid.NewGuid();
            sales = Guid.NewGuid();
            sales1 = Guid.NewGuid();
            admin = Guid.NewGuid();
            var rows = new List<DepartmentRow>
            {
                new(hq, "HQ", "總公司", Guid.Empty, Guid.Empty),
                new(sales, "SALES", "業務部", hq, Guid.Empty),
                new(sales1, "SALES1", "業務一課", sales, Guid.Empty),
                new(admin, "ADMIN", "管理部", Guid.Empty, Guid.Empty),
            };
            return new DepartmentTree("C001", rows);
        }

        [Fact]
        [DisplayName("GetSelfAndDescendants of a root returns the whole subtree")]
        public void GetSelfAndDescendants_Root_ReturnsWholeSubtree()
        {
            var tree = Build(out var hq, out var sales, out var sales1, out _);

            var set = tree.GetSelfAndDescendants(hq);

            Assert.Equal(3, set.Count);
            Assert.Contains(hq, set);
            Assert.Contains(sales, set);
            Assert.Contains(sales1, set);
        }

        [Fact]
        [DisplayName("GetSelfAndDescendants of a middle node returns itself and its descendants")]
        public void GetSelfAndDescendants_Mid_ReturnsSelfAndDescendants()
        {
            var tree = Build(out var hq, out var sales, out var sales1, out _);

            var set = tree.GetSelfAndDescendants(sales);

            Assert.Equal(2, set.Count);
            Assert.Contains(sales, set);
            Assert.Contains(sales1, set);
            Assert.DoesNotContain(hq, set);
        }

        [Fact]
        [DisplayName("GetSelfAndDescendants of a leaf returns only itself")]
        public void GetSelfAndDescendants_Leaf_ReturnsSelf()
        {
            var tree = Build(out _, out _, out var sales1, out _);

            var set = tree.GetSelfAndDescendants(sales1);

            Assert.Single(set);
            Assert.Contains(sales1, set);
        }

        [Fact]
        [DisplayName("GetSelfAndDescendants of an unknown node returns an empty set")]
        public void GetSelfAndDescendants_Unknown_ReturnsEmpty()
        {
            var tree = Build(out _, out _, out _, out _);

            Assert.Empty(tree.GetSelfAndDescendants(Guid.NewGuid()));
        }

        [Fact]
        [DisplayName("Contains and Roots return the expected results")]
        public void ContainsNodeRoots_Correct()
        {
            var tree = Build(out var hq, out _, out _, out var admin);

            Assert.True(tree.Contains(hq));
            Assert.False(tree.Contains(Guid.NewGuid()));
            // Two roots: HQ and ADMIN.
            Assert.Equal(2, tree.Roots!.Count);
            Assert.Contains(tree.Roots, n => n.RowId == hq);
            Assert.Contains(tree.Roots, n => n.RowId == admin);
        }

        [Fact]
        [DisplayName("A parent-child cycle does not cause infinite recursion")]
        public void GetSelfAndDescendants_Cycle_DoesNotLoopForever()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var tree = new DepartmentTree("C001",
            [
                new DepartmentRow(a, "A", "A", b, Guid.Empty),  // The parent of A is B.
                new DepartmentRow(b, "B", "B", a, Guid.Empty),  // The parent of B is A, which closes the cycle.
            ]);

            var ex = Record.Exception(() =>
            {
                _ = tree.GetSelfAndDescendants(a);
            });

            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("XML round-trip restores the nesting and consistent queries (the index is rebuilt)")]
        public void XmlRoundTrip_PreservesNestingAndQueries()
        {
            var tree = Build(out var hq, out var sales, out var sales1, out _);

            var xml = XmlCodec.Serialize(tree);
            var restored = XmlCodec.Deserialize<DepartmentTree>(xml)!;

            Assert.Equal("C001", restored.CompanyId);
            Assert.Equal(2, restored.Roots!.Count);
            Assert.Equal(3, restored.GetSelfAndDescendants(hq).Count);
            Assert.Equal(new[] { sales, sales1 }, restored.GetSelfAndDescendants(sales));
        }

        [Fact]
        [DisplayName("JSON round-trip restores the nesting and consistent queries (the index is rebuilt)")]
        public void JsonRoundTrip_PreservesNestingAndQueries()
        {
            var tree = Build(out var hq, out var sales, out var sales1, out _);

            var json = JsonSerializer.Serialize(tree);
            var restored = JsonSerializer.Deserialize<DepartmentTree>(json)!;

            Assert.Equal("C001", restored.CompanyId);
            Assert.Equal(2, restored.Roots!.Count);
            Assert.Equal(3, restored.GetSelfAndDescendants(hq).Count);
            Assert.Equal(new[] { sales, sales1 }, restored.GetSelfAndDescendants(sales));
        }
    }
}
