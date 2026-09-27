using System.ComponentModel;
using System.Data;
using Polhem.Base.Collections;

namespace Polhem.Base.UnitTests
{
    public class CollectionBaseTests
    {
        private static readonly string[] s_expectedNames = { "a", "b", "c" };

        private sealed class Item : CollectionItem
        {
            public string Name { get; set; } = string.Empty;
        }

        private sealed class Items : CollectionBase<Item>
        {
            public Items() { }
            public Items(object owner) : base(owner) { }
        }

        [Fact]
        [DisplayName("Add sets Item.Collection and Remove clears it")]
        public void AddAndRemove_UpdatesOwningCollectionReference()
        {
            var items = new Items();
            var item = new Item { Name = "a" };

            items.Add(item);
            Assert.Same(items, item.Collection);

            items.Remove(item);
            Assert.Null(item.Collection);
        }

        [Fact]
        [DisplayName("Item.Remove removes the item from its collection")]
        public void Item_Remove_RemovesFromOwningCollection()
        {
            var items = new Items();
            var item = new Item { Name = "a" };
            items.Add(item);

            item.Remove();

            Assert.Empty(items);
            Assert.Null(item.Collection);
        }

        [Fact]
        [DisplayName("Insert places the item at the given index and sets Collection")]
        public void Insert_AtIndex_InsertsAndSetsCollection()
        {
            var items = new Items
            {
                new Item { Name = "a" },
                new Item { Name = "c" }
            };

            var middle = new Item { Name = "b" };
            items.Insert(1, middle);

            Assert.Equal(s_expectedNames, items.Select(i => i.Name));
            Assert.Same(items, middle.Collection);
        }

        [Fact]
        [DisplayName("Operations through the ICollectionBase interface behave like the typed methods")]
        public void InterfaceMethods_BehaveLikeTypedMethods()
        {
            var items = new Items();
            ICollectionBase untyped = items;
            var item = new Item { Name = "x" };

            untyped.Add(item);
            Assert.Single(items);
            Assert.Same(items, item.Collection);

            untyped.Insert(0, new Item { Name = "y" });
            Assert.Equal(2, items.Count);

            untyped.Remove(item);
            Assert.Null(item.Collection);
        }

        [Fact]
        [DisplayName("Owner set in the constructor is readable and Tag is read-write")]
        public void Owner_AndTag_AreSettable()
        {
            var owner = new object();
            var items = new Items(owner) { Tag = "tag" };

            Assert.Same(owner, items.Owner);
            Assert.Equal("tag", items.Tag);
        }
    }

    public class KeyCollectionBaseTests
    {
        private sealed class KeyedItem : KeyCollectionItem
        {
            public int Value { get; set; }
        }

        private sealed class KeyedItems : KeyCollectionBase<KeyedItem>
        {
            public KeyedItems() { }
            public KeyedItems(object owner) : base(owner) { }
        }

        [Fact]
        [DisplayName("An added item can be looked up by Key case-insensitively")]
        public void Add_AllowsCaseInsensitiveLookup()
        {
            var items = new KeyedItems
            {
                new KeyedItem { Key = "Alpha", Value = 1 }
            };

            Assert.True(items.Contains("alpha"));
            Assert.True(items.Contains("ALPHA"));
            Assert.Equal(1, items["alpha"].Value);
        }

        [Fact]
        [DisplayName("GetOrDefault returns null for a missing key")]
        public void GetOrDefault_MissingKey_ReturnsNull()
        {
            var items = new KeyedItems
            {
                new KeyedItem { Key = "Alpha" }
            };

            Assert.NotNull(items.GetOrDefault("alpha"));
            Assert.Null(items.GetOrDefault("beta"));
        }

        [Fact]
        [DisplayName("Changing Item.Key updates the collection index")]
        public void ChangingItemKey_UpdatesCollectionIndex()
        {
            var items = new KeyedItems();
            var item = new KeyedItem { Key = "Old" };
            items.Add(item);

            item.Key = "New";

            Assert.False(items.Contains("Old"));
            Assert.True(items.Contains("New"));
        }

        [Fact]
        [DisplayName("The ChangeItemKey interface method re-registers the key")]
        public void ChangeItemKey_UpdatesIndex()
        {
            var items = new KeyedItems();
            var item = new KeyedItem { Key = "Old" };
            items.Add(item);

            ((IKeyCollectionBase)items).ChangeItemKey("Renamed", item);

            Assert.True(items.Contains("Renamed"));
        }

        [Fact]
        [DisplayName("KeyCollectionItem.Remove removes the item from its collection")]
        public void KeyedItem_Remove_RemovesFromCollection()
        {
            var items = new KeyedItems();
            var item = new KeyedItem { Key = "x" };
            items.Add(item);

            item.Remove();

            Assert.Empty(items);
            Assert.Null(item.Collection);
        }

        [Fact]
        [DisplayName("Add, Insert and Remove through the IKeyCollectionBase interface behave like the typed methods")]
        public void InterfaceMethods_BehaveLikeTypedMethods()
        {
            var items = new KeyedItems();
            IKeyCollectionBase untyped = items;

            var a = new KeyedItem { Key = "a" };
            var b = new KeyedItem { Key = "b" };
            untyped.Add(a);
            untyped.Insert(0, b);

            Assert.Equal(2, items.Count);
            Assert.Equal("b", items[0].Key);

            untyped.Remove(a);
            Assert.Single(items);
        }

        [Fact]
        [DisplayName("Owner set in the constructor is readable")]
        public void Owner_IsSet()
        {
            var owner = new object();
            var items = new KeyedItems(owner);
            Assert.Same(owner, items.Owner);
        }
    }

    public class StringHashSetTests
    {
        [Fact]
        [DisplayName("Strings differing only in case are treated as equal and not added twice")]
        public void Add_IsCaseInsensitive()
        {
            var set = new StringHashSet { "Apple" };
            Assert.False(set.Add("apple"));
            Assert.Single(set);
        }

        [Fact]
        [DisplayName("Add(string, delimiter) splits the string and adds every token")]
        public void AddWithDelimiter_SplitsAndAddsTokens()
        {
            var set = new StringHashSet
            {
                { "a,b,c", "," }
            };

            Assert.Equal(3, set.Count);
            Assert.Contains("a", set);
            Assert.Contains("c", set);
        }

        [Fact]
        [DisplayName("Add(string, delimiter) ignores an empty string")]
        public void AddWithDelimiter_EmptyInput_NoOp()
        {
            var set = new StringHashSet
            {
                { string.Empty, "," }
            };

            Assert.Empty(set);
        }
    }

    public class DictionaryTests
    {
        [Fact]
        [DisplayName("Dictionary<T> uses case-insensitive keys")]
        public void Lookup_IsCaseInsensitive()
        {
            var dict = new Dictionary<int> { ["Alpha"] = 1 };

            Assert.Equal(1, dict["alpha"]);
            Assert.True(dict.ContainsKey("ALPHA"));
        }
    }

    public class CollectionExtensionsTests
    {
        [Fact]
        [DisplayName("GetValue returns the stored value when the key exists")]
        public void GetValue_Hit_ReturnsValue()
        {
            var table = new DataTable();
            table.ExtendedProperties["Key"] = 42;

            int result = table.ExtendedProperties.GetValue<int>("Key", 0);
            Assert.Equal(42, result);
        }

        [Fact]
        [DisplayName("GetValue returns the default value when the key is missing")]
        public void GetValue_Miss_ReturnsDefault()
        {
            var table = new DataTable();

            int result = table.ExtendedProperties.GetValue<int>("Missing", -1);
            Assert.Equal(-1, result);
        }
    }
}
