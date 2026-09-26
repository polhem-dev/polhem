using System.ComponentModel;
using Polhem.Api.Core.MessagePack;
using Polhem.Definition.Organization;

namespace Polhem.Api.Core.UnitTests.Organization
{
    /// <summary>
    /// MessagePack round-trip of DepartmentTree (the third format; XML and JSON are tested in Polhem.Definition.UnitTests).
    /// Verifies that the tree built from flat rows is restored across MessagePack and that the lookup index is rebuilt.
    /// </summary>
    public class DepartmentTreeMessagePackTests
    {
        [Fact]
        [DisplayName("DepartmentTree lookups give the same results after a MessagePack round-trip")]
        public void DepartmentTree_MessagePack_RoundTrip()
        {
            var hq = Guid.NewGuid();
            var sales = Guid.NewGuid();
            var sales1 = Guid.NewGuid();
            var tree = new DepartmentTree("C001",
            [
                new DepartmentRow(hq, "HQ", "總公司", Guid.Empty, Guid.Empty),
                new DepartmentRow(sales, "SALES", "業務部", hq, Guid.Empty),
                new DepartmentRow(sales1, "SALES1", "業務一課", sales, Guid.Empty),
            ]);

            var bytes = MessagePackCodec.Serialize(tree);
            var restored = MessagePackCodec.Deserialize<DepartmentTree>(bytes)!;

            Assert.Equal("C001", restored.CompanyId);
            Assert.Single(restored.Roots!);                                   // Nested: a single root (HQ).
            // The index is rebuilt lazily after deserialization.
            Assert.Equal(3, restored.GetSelfAndDescendants(hq).Count);
            Assert.Equal(2, restored.GetSelfAndDescendants(sales).Count);
            Assert.Equal(new[] { sales1, sales, hq }, restored.GetSelfAndAncestors(sales1)); // The nested parent chain is restored.
        }

        [Fact]
        [DisplayName("An empty DepartmentTree round-trips through MessagePack without a NullReferenceException")]
        public void EmptyDepartmentTree_MessagePack_RoundTrip()
        {
            var tree = new DepartmentTree("C001", []);

            var bytes = MessagePackCodec.Serialize(tree);
            var restored = MessagePackCodec.Deserialize<DepartmentTree>(bytes)!;

            Assert.Equal("C001", restored.CompanyId);
            Assert.Empty(restored.Roots!);
        }
    }
}
