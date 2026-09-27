using System.ComponentModel;
using Polhem.Base.Collections;
using Polhem.Definition.Sorting;

namespace Polhem.Definition.UnitTests.Collections
{
    /// <summary>
    /// Tests for the base behavior of CollectionBase.
    /// Uses SortFieldCollection/SortField as the concrete subtypes.
    /// </summary>
    public class CollectionBaseBehaviorTests
    {
        /// <summary>
        /// A subclass for testing the protected members (the owner constructor and SetOwner).
        /// </summary>
        private sealed class OwnerAwareCollection : CollectionBase<SortField>
        {
            public OwnerAwareCollection() : base() { }
            public OwnerAwareCollection(object owner) : base(owner) { }
            public void CallSetOwner(object owner) => SetOwner(owner);
        }

        private static SortField MakeField(string name = "F") =>
            new SortField(name, SortDirection.Asc);

        [Fact]
        [DisplayName("Default constructor leaves Owner null")]
        public void DefaultConstructor_OwnerIsNull()
        {
            var col = new SortFieldCollection();
            Assert.Null(col.Owner);
        }

        [Fact]
        [DisplayName("Add calls InsertItem and sets item.Collection")]
        public void Add_SetsItemCollection()
        {
            var col = new SortFieldCollection();
            var item = MakeField();

            col.Add(item);

            Assert.Same(col, item.Collection);
            Assert.Single(col);
        }

        [Fact]
        [DisplayName("ICollectionItem overload of Add accepts a compatible type")]
        public void Add_ViaInterface_Works()
        {
            var col = new SortFieldCollection();
            ICollectionItem item = MakeField();

            ((ICollectionBase)col).Add(item);

            Assert.Single(col);
            Assert.Same(col, ((SortField)item).Collection);
        }

        [Fact]
        [DisplayName("ICollectionItem overload of Insert inserts at the given index")]
        public void Insert_ViaInterface_InsertsAtIndex()
        {
            var col = new SortFieldCollection
            {
                MakeField("A"),
                MakeField("B")
            };

            ICollectionItem newItem = MakeField("C");
            col.Insert(1, newItem);

            Assert.Equal(3, col.Count);
            Assert.Equal("C", col[1].FieldName);
        }

        [Fact]
        [DisplayName("ICollectionItem overload of Remove removes the item and clears item.Collection")]
        public void Remove_ViaInterface_ClearsItemCollection()
        {
            var col = new SortFieldCollection();
            var item = MakeField();
            col.Add(item);

            col.Remove((ICollectionItem)item);

            Assert.Empty(col);
            Assert.Null(item.Collection);
        }

        [Fact]
        [DisplayName("Clear removes all items")]
        public void Clear_RemovesAllItems()
        {
            var col = new SortFieldCollection
            {
                MakeField("A"),
                MakeField("B")
            };

            col.Clear();

            Assert.Empty(col);
        }

        [Fact]
        [DisplayName("Tag defaults to null and accepts any object")]
        public void Tag_DefaultAndAssignment()
        {
            var col = new SortFieldCollection();
            Assert.Null(col.Tag);

            col.Tag = "meta";
            Assert.Equal("meta", col.Tag);
        }

        [Fact]
        [DisplayName("Constructor taking an owner sets Owner")]
        public void Constructor_WithOwner_SetsOwner()
        {
            var owner = new object();
            var col = new OwnerAwareCollection(owner);

            Assert.Same(owner, col.Owner);
        }

        [Fact]
        [DisplayName("SetOwner updates the Owner property")]
        public void SetOwner_UpdatesOwner()
        {
            var col = new OwnerAwareCollection();
            var owner = new object();

            col.CallSetOwner(owner);

            Assert.Same(owner, col.Owner);
        }
    }
}
