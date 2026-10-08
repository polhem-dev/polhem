using System.ComponentModel;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.UI.Avalonia.UnitTests.Controls
{
    public class CollectionEditSessionTests
    {
        private static ListItemCollection CreateListItems()
        {
            var items = new ListItemCollection
            {
                { "A", "Active" },
                { "C", "Closed" },
            };
            return items;
        }

        private static CollectionEditSession Edit(ListItemCollection items) => new(items, typeof(ListItem));

        [Fact]
        [DisplayName("The working list holds copies, so editing them leaves the collection untouched")]
        public void Constructor_CopiesItems()
        {
            var items = CreateListItems();
            var session = Edit(items);

            ((ListItem)session.Items[0]).Text = "Changed";

            Assert.True(session.IsCancelable);
            Assert.NotSame(items[0], session.Items[0]);
            Assert.Equal("Active", items[0].Text);
        }

        [Fact]
        [DisplayName("Removing and moving items changes only the working list until Commit")]
        public void Edits_BeforeCommit_LeaveCollectionUntouched()
        {
            var items = CreateListItems();
            var session = Edit(items);

            session.Move(1, -1);
            session.Remove([1]);

            Assert.Equal(["C"], session.Items.Cast<ListItem>().Select(i => i.Value));
            Assert.Equal(["A", "C"], items.Select(i => i.Value));
        }

        [Fact]
        [DisplayName("Commit replaces the items in the working list's order and unlinks the old items")]
        public void Commit_ReplacesItemsAndUnlinksOldOnes()
        {
            var items = CreateListItems();
            var oldFirst = items[0];
            var session = Edit(items);
            session.Move(0, 1);

            session.Commit();

            Assert.Equal(["C", "A"], items.Select(i => i.Value));
            Assert.Null(oldFirst.Collection);
            Assert.All(items, i => Assert.Same(items, i.Collection));
            Assert.Equal("Active", items["A"].Text);
        }

        [Fact]
        [DisplayName("Add gives a new keyed item the first free key named after its type and puts it after the given item")]
        public void Add_KeyedItem_GetsFreeKeyAfterIndex()
        {
            var items = CreateListItems();
            items.Add("ListItem1", "Taken");
            var session = Edit(items);

            var index = session.Add(0);

            Assert.Equal(1, index);
            Assert.Equal("ListItem2", ((ListItem)session.Items[1]).Value);
        }

        [Fact]
        [DisplayName("Add with no selected item appends to the end")]
        public void Add_NoIndex_Appends()
        {
            var session = new CollectionEditSession(new FieldMappingCollection(), typeof(FieldMapping));

            Assert.Equal(0, session.Add(-1));
            Assert.Equal(1, session.Add(-1));
        }

        [Fact]
        [DisplayName("Move leaves an item in place when the move would leave the list")]
        public void Move_OutOfRange_KeepsIndex()
        {
            var session = Edit(CreateListItems());

            Assert.Equal(0, session.Move(0, -1));
            Assert.Equal(1, session.Move(1, 1));
        }

        [Fact]
        [DisplayName("Keys that differ only in case count as the same key, as KeyCollectionBase compares them")]
        public void GetInvalidIndices_CaseInsensitiveDuplicates()
        {
            var session = Edit(CreateListItems());

            ((ListItem)session.Items[1]).Value = "a";

            Assert.Equal([0, 1], session.GetInvalidIndices().OrderBy(i => i));
            Assert.NotNull(session.Validate());
        }

        [Fact]
        [DisplayName("Two items may swap keys in the working list without a conflict")]
        public void SwapKeys_InWorkingList_IsValid()
        {
            var items = CreateListItems();
            var session = Edit(items);

            ((ListItem)session.Items[0]).Value = "C";
            ((ListItem)session.Items[1]).Value = "A";
            session.Commit();

            Assert.Equal("Closed", items["A"].Text);
            Assert.Equal("Active", items["C"].Text);
        }

        [Fact]
        [DisplayName("Commit with a repeated key throws and leaves the collection as it was")]
        public void Commit_DuplicateKey_ThrowsAndKeepsCollection()
        {
            var items = CreateListItems();
            var session = Edit(items);
            ((ListItem)session.Items[1]).Value = "A";

            Assert.Throws<InvalidOperationException>(session.Commit);
            Assert.Equal(["A", "C"], items.Select(i => i.Value));
        }

        [Fact]
        [DisplayName("An item type without a parameterless constructor edits the items themselves and cannot be cancelled")]
        public void Constructor_UncopyableItems_EditsOriginals()
        {
            var items = new List<UncopyableItem> { new(1) };

            var session = new CollectionEditSession(items, typeof(UncopyableItem));

            Assert.False(session.IsCancelable);
            Assert.Same(items[0], session.Items[0]);
            Assert.False(session.CanAdd);
        }

        [Fact]
        [DisplayName("CanCreate refuses an abstract type and a string")]
        public void CanCreate_AbstractOrString_IsFalse()
        {
            Assert.False(CollectionEditSession.CanCreate(typeof(LayoutFieldBase)));
            Assert.False(CollectionEditSession.CanCreate(typeof(string)));
            Assert.True(CollectionEditSession.CanCreate(typeof(ListItem)));
        }

        [Fact]
        [DisplayName("GetLabel uses the item's text, then its key, then the type name and position")]
        public void GetLabel_FallsBackInOrder()
        {
            var items = CreateListItems();
            items.Add("K", string.Empty);
            var session = Edit(items);

            Assert.Equal("Active", session.GetLabel(0, null));
            Assert.Equal("K", session.GetLabel(2, null));

            var plain = new CollectionEditSession(new List<PlainItem> { new() }, typeof(PlainItem));
            Assert.Equal("PlainItem #1", plain.GetLabel(0, null));
        }

        [Fact]
        [DisplayName("GetLabel translates the TreeNode display format as an ItemLabel of the item's type")]
        public void GetLabel_TranslatesDisplayFormatAsItemLabel()
        {
            var rules = new PermissionRuleCollection { new PermissionRule(PermissionActions.Read, ScopeStrategy.Own) };
            var session = new CollectionEditSession(rules, typeof(PermissionRule));
            PropertyGridText? received = null;

            var label = session.GetLabel(0, t => { received = t; return "{0} = {1}"; });

            Assert.Equal("Read = Own", label);
            Assert.NotNull(received);
            Assert.Equal(PropertyGridTextKind.ItemLabel, received.Kind);
            Assert.Equal(typeof(PermissionRule), received.ComponentType);
            Assert.Null(received.PropertyName);
            Assert.Equal("{0} : {1}", received.Text);
        }

        [Fact]
        [DisplayName("A nested edit commits into the outer copy only, so the collection changes when the outer edit commits")]
        public void NestedEdit_ReachesCollectionOnlyThroughOuterCommit()
        {
            var parent = new NestingItem { Name = "Drinks" };
            parent.Children.Add(new PlainItem { Name = "Tea" });
            var collection = new List<NestingItem> { parent };
            var outer = new CollectionEditSession(collection, typeof(NestingItem));
            var copy = (NestingItem)outer.Items[0];

            var inner = new CollectionEditSession(copy.Children, typeof(PlainItem));
            ((PlainItem)inner.Items[inner.Add(0)]).Name = "Coffee";
            inner.Commit();

            Assert.True(outer.IsCancelable);
            Assert.True(inner.IsCancelable);
            Assert.Equal(["Tea", "Coffee"], copy.Children.Select(c => c.Name));
            Assert.Equal(["Tea"], parent.Children.Select(c => c.Name));

            outer.Commit();

            Assert.Equal(["Tea", "Coffee"], Assert.Single(collection).Children.Select(c => c.Name));
        }

        [Fact]
        [DisplayName("NextKey skips the keys already used, ignoring case")]
        public void NextKey_SkipsUsedKeys()
        {
            Assert.Equal("value3", CollectionEditSession.NextKey("value", ["VALUE1", "value2"]));
        }

        /// <summary>An item type <c>XmlSerializer</c> cannot handle, because it has no parameterless constructor.</summary>
        public sealed class UncopyableItem(int value)
        {
            public int Value { get; set; } = value;
        }

        /// <summary>An item type with neither a label annotation nor its own <c>ToString</c>.</summary>
        public sealed class PlainItem
        {
            public string Name { get; set; } = string.Empty;
        }

        /// <summary>An item that holds a collection of its own, as a nested dialog edits it.</summary>
        public sealed class NestingItem
        {
            public string Name { get; set; } = string.Empty;

            public List<PlainItem> Children { get; } = [];
        }
    }
}
