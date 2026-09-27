using System.ComponentModel;
using Polhem.Definition.Collections;
using Polhem.Definition.Sorting;

namespace Polhem.Definition.UnitTests.Collections
{
    /// <summary>
    /// Tests for the base behavior of CollectionItem / KeyCollectionItem.
    /// Uses <see cref="SortField"/>/<see cref="SortFieldCollection"/> (not keyed)
    /// and <see cref="Parameter"/>/<see cref="ParameterCollection"/> (keyed) as the subjects under test.
    /// </summary>
    public class CollectionItemBehaviorTests
    {
        [Fact]
        [DisplayName("Default-constructed CollectionItem has a null Tag and a null Collection")]
        public void DefaultState_IsExpected()
        {
            var item = new SortField("Id", SortDirection.Asc);

            Assert.Null(item.Tag);
            Assert.Null(item.Collection);
        }

        [Fact]
        [DisplayName("Tag can be set and read back")]
        public void Tag_Settable()
        {
            var item = new SortField();
            var marker = new object();

            item.Tag = marker;

            Assert.Same(marker, item.Tag);
        }

        [Fact]
        [DisplayName("After being added, Collection returns the owning collection")]
        public void Collection_AfterAdd_ReturnsOwner()
        {
            var collection = new SortFieldCollection();
            var item = new SortField("Id", SortDirection.Asc);

            collection.Add(item);

            Assert.Same(collection, item.Collection);
        }

        [Fact]
        [DisplayName("Remove removes the item from its owning collection")]
        public void Remove_RemovesSelfFromCollection()
        {
            var collection = new SortFieldCollection();
            var item = new SortField("Id", SortDirection.Asc);
            collection.Add(item);
            Assert.Single(collection);

            item.Remove();

            Assert.Empty(collection);
        }

        [Fact]
        [DisplayName("Remove does not throw when the item is not in a collection")]
        public void Remove_WithoutCollection_DoesNotThrow()
        {
            var item = new SortField();

            item.Remove();

            Assert.Null(item.Collection);
        }
    }
}
