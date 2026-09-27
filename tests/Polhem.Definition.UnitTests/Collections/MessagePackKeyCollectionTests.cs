using System.ComponentModel;
using Polhem.Base.Collections;
using Polhem.Definition.Collections;

namespace Polhem.Definition.UnitTests.Collections
{
    /// <summary>
    /// Tests for the behavior of KeyCollectionBase / KeyCollectionItem.
    /// Uses the existing <see cref="Parameter"/> / <see cref="ParameterCollection"/> as the subjects under test.
    /// </summary>
    public class MessagePackKeyCollectionTests
    {
        /// <summary>
        /// A subclass for testing the protected members (the owner constructor).
        /// </summary>
        private sealed class OwnerAwareKeyCollection : KeyCollectionBase<Parameter>
        {
            public OwnerAwareKeyCollection() : base() { }
            public OwnerAwareKeyCollection(object owner) : base(owner) { }
        }

        [Fact]
        [DisplayName("After Add, items can be retrieved by index and by Key")]
        public void Add_Item_CanBeRetrievedByKeyAndIndex()
        {
            // Arrange
            var collection = new ParameterCollection
            {
                new Parameter("P1", 100),
                new Parameter("P2", "ABC")
            };

            // Act
            var byKey = collection["P2"];
            var byIndex = collection[0];

            // Assert
            Assert.Equal("P2", byKey.Name);
            Assert.Equal("ABC", byKey.Value);
            Assert.Equal("P1", byIndex.Name);
        }

        [Fact]
        [DisplayName("Contains returns whether the Key exists")]
        public void Contains_ReturnsWhetherKeyExists()
        {
            // Arrange
            var collection = new ParameterCollection
            {
                new Parameter("P1", 1)
            };

            // Act & Assert
            Assert.True(collection.Contains("P1"));
            Assert.False(collection.Contains("missing"));
        }

        [Fact]
        [DisplayName("Key comparison ignores case")]
        public void Contains_IsCaseInsensitive()
        {
            // Arrange
            var collection = new ParameterCollection
            {
                new Parameter("MyParam", 1)
            };

            // Act & Assert
            Assert.True(collection.Contains("myparam"));
            Assert.True(collection.Contains("MYPARAM"));
        }

        [Fact]
        [DisplayName("Remove removes the item with the given Key")]
        public void Remove_RemovesItemByKey()
        {
            // Arrange
            var collection = new ParameterCollection
            {
                new Parameter("P1", 1),
                new Parameter("P2", 2)
            };

            // Act
            collection.Remove("P1");

            // Assert
            Assert.Single(collection);
            Assert.False(collection.Contains("P1"));
            Assert.True(collection.Contains("P2"));
        }

        [Fact]
        [DisplayName("Insert inserts the item at the given position")]
        public void Insert_AddsItemAtSpecifiedIndex()
        {
            // Arrange
            var collection = new ParameterCollection
            {
                new Parameter("P1", 1),
                new Parameter("P3", 3)
            };

            // Act
            collection.Insert(1, new Parameter("P2", 2));

            // Assert
            Assert.Equal(3, collection.Count);
            Assert.Equal("P2", collection[1].Name);
        }

        [Fact]
        [DisplayName("Clear removes all items")]
        public void Clear_RemovesAllItems()
        {
            // Arrange
            var collection = new ParameterCollection
            {
                new Parameter("P1", 1),
                new Parameter("P2", 2)
            };

            // Act
            collection.Clear();

            // Assert
            Assert.Empty(collection);
        }

        [Fact]
        [DisplayName("Constructor taking an owner sets Owner")]
        public void Constructor_WithOwner_SetsOwner()
        {
            var owner = new object();
            var col = new OwnerAwareKeyCollection(owner);
            Assert.Same(owner, col.Owner);
        }

        [Fact]
        [DisplayName("Add(IKeyCollectionItem) adds the item and it can be retrieved by index")]
        public void Add_ViaInterface_AddsItem()
        {
            var col = new ParameterCollection();
            IKeyCollectionItem item = new Parameter("P1", 1);

            ((IKeyCollectionBase)col).Add(item);

            Assert.Single(col);
            Assert.Equal("P1", col[0].Name);
        }

        [Fact]
        [DisplayName("Insert(IKeyCollectionItem) inserts at the given index")]
        public void Insert_ViaInterface_InsertsAtIndex()
        {
            var col = new ParameterCollection
            {
                new Parameter("A", 1),
                new Parameter("C", 3)
            };
            IKeyCollectionItem mid = new Parameter("B", 2);

            col.Insert(1, mid);

            Assert.Equal(3, col.Count);
            Assert.Equal("B", col[1].Name);
        }

        [Fact]
        [DisplayName("Remove(IKeyCollectionItem) removes the item by Key")]
        public void Remove_ViaInterface_RemovesByKey()
        {
            var col = new ParameterCollection
            {
                new Parameter("P1", 1),
                new Parameter("P2", 2)
            };
            IKeyCollectionItem target = col[0];

            col.Remove(target);

            Assert.Single(col);
            Assert.False(col.Contains("P1"));
        }

        [Fact]
        [DisplayName("ChangeItemKey updates the Key index of the item")]
        public void ChangeItemKey_UpdatesKeyIndex()
        {
            var col = new ParameterCollection
            {
                new Parameter("Old", 1)
            };
            var item = col["Old"];
            item.Name = "New";

            col.ChangeItemKey("New", item);

            Assert.True(col.Contains("New"));
            Assert.False(col.Contains("Old"));
            Assert.Equal(1, col["New"].Value);
        }

        [Fact]
        [DisplayName("Tag defaults to null and accepts any object")]
        public void Tag_DefaultAndAssignment()
        {
            var col = new ParameterCollection();
            Assert.Null(col.Tag);

            col.Tag = "meta";
            Assert.Equal("meta", col.Tag);
        }

    }
}
