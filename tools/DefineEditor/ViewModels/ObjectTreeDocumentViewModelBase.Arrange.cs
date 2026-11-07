using System.Collections;
using CommunityToolkit.Mvvm.Input;
using Polhem.Core.Collections;
using Polhem.Core.Serialization;
using Polhem.Definition.ObjectTree;

namespace Polhem.DefineEditor.ViewModels;

// Duplicate, Move Up and Move Down: commands that copy or reorder the selected object within the define collection
// that holds it, the same collection drag and drop reorders.
public abstract partial class ObjectTreeDocumentViewModelBase
{
    /// <summary>
    /// Inserts a copy of the selected object right after it and selects the copy. A keyed object gets a free key
    /// ending in <c>_copy</c>, so the copy does not start as a duplicate-key error.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDuplicate))]
    private void Duplicate()
    {
        if (SelectedTreeNode is not { Parent: { } parent } node || DuplicateTarget(node) is not { } list)
            return;

        // The copy goes through the same serializer as a save, so it holds exactly what the file would.
        var copy = XmlCodec.Deserialize(XmlCodec.Serialize(node.Value), node.Value.GetType())
            ?? throw new InvalidOperationException($"{node.Value.GetType().Name} could not be copied.");
        PrepareDuplicate(copy, list);
        list.Insert(DefineCollections.IndexOf(list, node.Value) + 1, copy);

        var copyNode = Builder.Build(copy);
        copyNode.IsExpanded = false;
        parent.Children.Insert(parent.Children.IndexOf(node) + 1, copyNode);
        SelectedTreeNode = copyNode;
        MarkChanged();
        StatusText = L("Status_Duplicated", copyNode.Label);
    }

    private bool CanDuplicate() => SelectedTreeNode is { } node && DuplicateTarget(node) is not null;

    // Only an object the document could delete is copied: the others (a root, a fixed settings section) are
    // singletons, not items of a list.
    private IList? DuplicateTarget(ObjectTreeNode node) =>
        !node.IsFolder && GetDeleteAction(node) is not null ? DefineCollections.OwningList(node) : null;

    /// <summary>
    /// Makes <paramref name="copy"/> fit into <paramref name="list"/> before it is inserted. The default gives a
    /// <see cref="KeyCollectionItem"/> a key that no item of the list uses.
    /// </summary>
    protected virtual void PrepareDuplicate(object copy, IList list)
    {
        if (copy is KeyCollectionItem item)
            item.Key = UniqueKey(list.OfType<KeyCollectionItem>().Select(i => i.Key), item.Key + "_copy");
    }

    /// <summary>Moves the selected object before its previous sibling in the same collection.</summary>
    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp() => Move(-1);

    private bool CanMoveUp() => MoveTarget(-1) is not null;

    /// <summary>Moves the selected object after its next sibling in the same collection.</summary>
    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown() => Move(1);

    private bool CanMoveDown() => MoveTarget(1) is not null;

    private void Move(int step)
    {
        if (SelectedTreeNode is not { Parent: { } parent } node || MoveTarget(step) is not { } target)
            return;

        DragDropHandler.Drop(node, target, step < 0 ? TreeNodeDropPosition.Before : TreeNodeDropPosition.After);
        parent.Children.Move(parent.Children.IndexOf(node), parent.Children.IndexOf(target));
        // The tree may drop its selection while the item moves; select the moved node again.
        SelectedTreeNode = node;
        RefreshToolbar();
    }

    // The sibling the selected node swaps places with: the nearest one before or after it that lives in the same
    // collection. Nodes the editor adds itself (a field's Relation group) are not in the collection and are skipped.
    private ObjectTreeNode? MoveTarget(int step)
    {
        if (SelectedTreeNode is not { IsFolder: false, Parent: { } parent } node
            || DefineCollections.OwningList(node) is not { } list)
            return null;

        var siblings = parent.Children.Where(c => !c.IsFolder && DefineCollections.IndexOf(list, c.Value) >= 0).ToList();
        var index = siblings.IndexOf(node) + step;
        return index >= 0 && index < siblings.Count ? siblings[index] : null;
    }

    private void NotifyArrangeCommands()
    {
        DuplicateCommand.NotifyCanExecuteChanged();
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }
}
