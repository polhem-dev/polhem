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
    public class MessagePackCollectionItemTests
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

    /// <summary>
    /// Tests for behavior specific to KeyCollectionItem (setting Key, Remove).
    /// </summary>
    public class MessagePackKeyCollectionItemTests
    {
        [Fact]
        [DisplayName("Default construction gives an empty Key, a null Collection and a null Tag")]
        public void DefaultState_IsExpected()
        {
            var item = new Parameter();

            Assert.Equal(string.Empty, item.Key);
            Assert.Null(item.Collection);
            Assert.Null(item.Tag);
        }

        [Fact]
        [DisplayName("Key can be set freely when the item is not in a collection")]
        public void Key_Settable_WhenNotInCollection()
        {
            var item = new Parameter { Key = "Alpha" };

            Assert.Equal("Alpha", item.Key);
            Assert.Equal("Alpha", item.Name);
        }

        [Fact]
        [DisplayName("Changing Key after the item is added updates the collection index")]
        public void Key_Change_WhileInCollection_UpdatesCollectionIndex()
        {
            var collection = new ParameterCollection
            {
                new Parameter("Alpha", 1)
            };
            var item = collection["Alpha"];

            item.Key = "Beta";

            Assert.False(collection.Contains("Alpha"));
            Assert.True(collection.Contains("Beta"));
            Assert.Same(item, collection["Beta"]);
        }

        [Fact]
        [DisplayName("Setting Key to the same value is a no-op")]
        public void Key_SetSameValue_NoOp()
        {
            var collection = new ParameterCollection
            {
                new Parameter("Alpha", 1)
            };
            var item = collection["Alpha"];

            item.Key = "Alpha";

            Assert.True(collection.Contains("Alpha"));
            Assert.Single(collection);
        }

        [Fact]
        [DisplayName("Remove removes the item from its owning collection")]
        public void Remove_RemovesSelfFromCollection()
        {
            var collection = new ParameterCollection
            {
                new Parameter("A", 1),
                new Parameter("B", 2)
            };
            var item = collection["A"];

            item.Remove();

            Assert.False(collection.Contains("A"));
            Assert.True(collection.Contains("B"));
        }

        [Fact]
        [DisplayName("Remove does not throw when the item is not in a collection")]
        public void Remove_WithoutCollection_DoesNotThrow()
        {
            var item = new Parameter("X", 1);

            item.Remove();

            Assert.Null(item.Collection);
        }
    }
}
