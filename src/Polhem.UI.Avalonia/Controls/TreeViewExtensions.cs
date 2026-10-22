using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// Extension methods for <see cref="TreeView"/>.
    /// </summary>
    public static class TreeViewExtensions
    {
        /// <summary>
        /// Keeps a single-selection tree to one selected item: when an item is added to
        /// <see cref="TreeView.SelectedItems"/> while another is selected, the added item becomes
        /// <see cref="TreeView.SelectedItem"/> and replaces the other.
        /// </summary>
        /// <remarks>
        /// Selecting a row through the macOS accessibility interface (setting the row's selected attribute, as UI
        /// automation does) sets the row's <see cref="TreeViewItem.IsSelected"/>. <see cref="TreeView"/> adds that item to
        /// <see cref="TreeView.SelectedItems"/> and leaves <see cref="TreeView.SelectedItem"/> on the item selected
        /// before, so the row is highlighted but a binding to <see cref="TreeView.SelectedItem"/> never sees it. A tree
        /// whose <see cref="TreeView.SelectionMode"/> allows several items is left alone. Call it once per tree.
        /// </remarks>
        /// <param name="treeView">The tree to keep to a single selection.</param>
        public static void KeepSelectionSingle(this TreeView treeView)
        {
            ArgumentNullException.ThrowIfNull(treeView);
            treeView.AddHandler(SelectingItemsControl.SelectionChangedEvent, (_, e) => OnSelectionChanged(treeView, e));
        }

        private static void OnSelectionChanged(TreeView treeView, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.Source, treeView)
                || treeView.SelectionMode.HasFlag(SelectionMode.Multiple)
                || treeView.SelectedItems.Count < 2
                || e.AddedItems.Count == 0)
            {
                return;
            }
            treeView.SelectedItem = e.AddedItems[e.AddedItems.Count - 1];
        }
    }
}
