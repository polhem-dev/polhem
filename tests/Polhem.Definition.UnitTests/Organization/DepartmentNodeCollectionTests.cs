using System.ComponentModel;
using Polhem.Definition.Organization;

namespace Polhem.Definition.UnitTests.Organization
{
    public class DepartmentNodeCollectionTests
    {
        private static DepartmentNode CreateNode(string id)
            => new DepartmentNode(Guid.NewGuid(), id, id, Guid.Empty);

        [Fact]
        [DisplayName("AddRange returns without throwing for null")]
        public void AddRange_NullInput_DoesNotThrow()
        {
            var collection = new DepartmentNodeCollection();
            var exception = Record.Exception(() => collection.AddRange(null!));
            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("AddRange adds no nodes for an empty collection")]
        public void AddRange_EmptyCollection_DoesNotAddAny()
        {
            var collection = new DepartmentNodeCollection();
            collection.AddRange(Array.Empty<DepartmentNode>());
            Assert.Empty(collection);
        }

        [Fact]
        [DisplayName("AddRange skips null elements and adds only valid nodes")]
        public void AddRange_CollectionWithNullElements_SkipsNulls()
        {
            var collection = new DepartmentNodeCollection();
            var node = CreateNode("DEPT01");
            var nodes = new DepartmentNode[] { node, null! };
            collection.AddRange(nodes);
            Assert.Single(collection);
        }

        [Fact]
        [DisplayName("AddRange adds every node of a valid collection")]
        public void AddRange_ValidNodes_AddsAll()
        {
            var collection = new DepartmentNodeCollection();
            var nodes = new[] { CreateNode("A"), CreateNode("B"), CreateNode("C") };
            collection.AddRange(nodes);
            Assert.Equal(3, collection.Count);
        }
    }
}
