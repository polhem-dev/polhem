using System.ComponentModel;
using Polhem.Definition.Collections;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.UI.Avalonia.UnitTests.Controls
{
    /// <summary>
    /// Covers the panel without a UI thread: the buttons are driven through the panel's own methods, which the button
    /// clicks call.
    /// </summary>
    public class CollectionEditPanelTests
    {
        private static (ListItemCollection Items, CollectionEditPanel Panel) Create()
        {
            var items = new ListItemCollection
            {
                { "A", "Active" },
                { "C", "Closed" },
            };
            var panel = new CollectionEditPanel(new CollectionEditSession(items, typeof(ListItem)), null, compact: false);
            return (items, panel);
        }

        private static IReadOnlyList<string> Labels(CollectionEditPanel panel) =>
            panel.List.ItemsSource!.Cast<object>().Select(e => e.ToString()!).ToList();

        [Fact]
        [DisplayName("The panel lists the items and edits the first one in its grid")]
        public void Constructor_ListsItemsAndSelectsFirst()
        {
            var (_, panel) = Create();

            Assert.Equal(["Active", "Closed"], Labels(panel));
            Assert.Equal(0, panel.List.SelectedIndex);
            Assert.Same(panel.Session.Items[0], panel.ItemGrid.SelectedObject);
            Assert.Equal((true, true, false, true), panel.ButtonStates);
        }

        [Fact]
        [DisplayName("AddItem inserts after the selection and selects the new item")]
        public void AddItem_SelectsNewItem()
        {
            var (_, panel) = Create();

            panel.AddItem();

            Assert.Equal(1, panel.List.SelectedIndex);
            Assert.Equal("ListItem1", ((ListItem)panel.ItemGrid.SelectedObject!).Value);
        }

        [Fact]
        [DisplayName("MoveItem moves the selected item and keeps it selected")]
        public void MoveItem_KeepsSelection()
        {
            var (_, panel) = Create();

            panel.MoveItem(1);

            Assert.Equal(["Closed", "Active"], Labels(panel));
            Assert.Equal(1, panel.List.SelectedIndex);
            Assert.Equal((true, true, true, false), panel.ButtonStates);
        }

        [Fact]
        [DisplayName("RemoveItems removes the selection and selects the item that took its place")]
        public void RemoveItems_SelectsNext()
        {
            var (_, panel) = Create();

            panel.RemoveItems();

            Assert.Equal(["Closed"], Labels(panel));
            Assert.Equal(0, panel.List.SelectedIndex);
        }

        [Fact]
        [DisplayName("An edit in the item grid that repeats a key marks both items, and OK is refused with the reason")]
        public void Commit_DuplicateKey_RefusedAndMarked()
        {
            var (items, panel) = Create();
            var committed = false;
            panel.Committed += (_, _) => committed = true;
            var value = panel.ItemGrid.Rows.Single(r => r.Property.Name == nameof(ListItem.Value));

            ((global::Avalonia.Controls.TextBox)value.Editor).Text = "C";
            panel.ItemGrid.CommitText(value);

            Assert.All(Labels(panel), l => Assert.StartsWith(CollectionEditPanel.InvalidMarker, l));
            Assert.False(panel.Commit());
            Assert.False(committed);
            Assert.False(string.IsNullOrEmpty(panel.Message));
            Assert.Equal(["A", "C"], items.Select(i => i.Value));
        }

        [Fact]
        [DisplayName("OK writes the working list back and raises Committed")]
        public void Commit_Valid_WritesBack()
        {
            var (items, panel) = Create();
            var committed = false;
            panel.Committed += (_, _) => committed = true;
            panel.MoveItem(1);

            Assert.True(panel.Commit());

            Assert.True(committed);
            Assert.Equal(["C", "A"], items.Select(i => i.Value));
        }

        [Fact]
        [DisplayName("Cancel leaves the collection untouched and raises Cancelled")]
        public void Cancel_LeavesCollection()
        {
            var (items, panel) = Create();
            var cancelled = false;
            panel.Cancelled += (_, _) => cancelled = true;
            panel.RemoveItems();

            panel.Cancel();

            Assert.True(cancelled);
            Assert.Equal(["A", "C"], items.Select(i => i.Value));
        }
    }
}
