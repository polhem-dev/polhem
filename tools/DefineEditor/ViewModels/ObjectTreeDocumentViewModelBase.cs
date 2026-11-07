using System.Collections.ObjectModel;
using System.ComponentModel;
using Polhem.Core;
using Polhem.Core.Serialization;
using Polhem.Definition.ObjectTree;
using Polhem.DefineEditor.Models;
using Polhem.DefineEditor.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Common shell for tree editors of a single define file. Holds Title / FilePath /
/// Roots / Issues / IsDirty / StatusText, the Save command (XmlCodec round-trip),
/// the Validate command (delegates to <see cref="PerformValidation"/>), and a
/// generic Delete command driven by <see cref="GetDeleteAction"/>. The tree is built
/// by the framework's <see cref="ObjectTreeBuilder"/> from the <c>[TreeNode]</c>
/// annotations, and its context menu comes from an <see cref="ITreeNodeCommandProvider"/>.
/// A subclass sets its root object, calls <see cref="InitializeTree"/>, and supplies
/// the icon of each node, its commands and its Add / Delete actions.
/// </summary>
public abstract partial class ObjectTreeDocumentViewModelBase : DocumentViewModelBase
{
    public override string Title { get; }

    public override string DocumentKey => FilePath;

    public string FilePath { get; }

    public ObservableCollection<ObjectTreeNode> Roots { get; } = new();

    public ObservableCollection<ValidationIssue> Issues { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedEditorContext))]
    [NotifyPropertyChangedFor(nameof(SelectedPropertyFilter))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private ObjectTreeNode? _selectedTreeNode;

    /// <summary>The toolbar's Add buttons for the selected node.</summary>
    [ObservableProperty]
    private IReadOnlyList<TreeToolbarItem> _toolbarAddItems = [];

    /// <summary>
    /// The toolbar's buttons that move, duplicate and delete the selected node, each disabled when it cannot run.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<TreeToolbarItem> _toolbarEditItems = [];

    /// <summary>The selected node's other commands, which the toolbar keeps in its overflow menu.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasToolbarActions))]
    private IReadOnlyList<TreeToolbarItem> _toolbarActionItems = [];

    /// <summary>Whether the toolbar shows its overflow menu.</summary>
    public bool HasToolbarActions => ToolbarActionItems.Count > 0;

    partial void OnSelectedTreeNodeChanged(ObjectTreeNode? value) => RefreshToolbar();

    /// <summary>
    /// Reads the toolbar's commands again, after the selection, the document or anything else a command's enabled
    /// state depends on has changed.
    /// </summary>
    protected void RefreshToolbar()
    {
        NotifyArrangeCommands();
        // The subclass assigns CommandProvider in its constructor, after this class's has run.
        if (CommandProvider is not { } provider || SelectedTreeNode is not { } node)
        {
            ToolbarAddItems = [];
            ToolbarEditItems = [];
            ToolbarActionItems = [];
            return;
        }

        var commands = provider.GetToolbarCommands(node);
        ToolbarAddItems = commands.Add.Select(c => new TreeToolbarItem(c)).ToList();
        ToolbarEditItems = commands.Edit.Select(c => new TreeToolbarItem(c.Command, c.Shortcut)).ToList();
        ToolbarActionItems = commands.Actions.Select(c => new TreeToolbarItem(c)).ToList();
    }

    // IsDirty / StatusText / culture-change refresh are inherited from
    // DocumentViewModelBase.

    /// <summary>
    /// Right-pane content. Defaults to the selected node's object (nothing for a folder);
    /// subclasses override to inject wrapper view-models (e.g. FormSchema's mapping editor).
    /// </summary>
    public virtual object? SelectedEditorContext => SelectedTreeNode is { IsFolder: false } node ? node.Value : null;

    /// <summary>
    /// Which properties of <see cref="SelectedEditorContext"/> the property grid shows; <c>null</c> shows them all.
    /// </summary>
    public virtual Func<PropertyDescriptor, bool>? SelectedPropertyFilter => null;

    /// <summary>The values the property grid offers for string properties; <c>null</c> offers none.</summary>
    public virtual Func<PropertyDescriptor, object, IReadOnlyList<string>?>? ValueSuggestionProvider => null;

    /// <summary>
    /// What the property grid asks before it opens a collection; <c>null</c> opens every collection in the grid's
    /// own dialog.
    /// </summary>
    public virtual Func<CollectionEditContext, Task<bool>?>? CollectionEditorProvider => null;

    /// <summary>
    /// The editors of their own the property grid offers beside a text row, as a button; <c>null</c> offers none.
    /// </summary>
    public virtual Func<PropertyDescriptor, object, Func<Task<bool>>?>? ValueEditorProvider => null;

    /// <summary>Underlying mutable object handed to <see cref="XmlCodec.SerializeToFile"/>.</summary>
    protected abstract object RootObject { get; }

    // Forwarders for the base type's uniform file-level command surface — see
    // DocumentViewModelBase.FileSaveCommand. The generated SaveCommand /
    // ValidateCommand below carry the actual logic; this just exposes them
    // under a name the source generator hasn't taken.
    public override IRelayCommand FileSaveCommand => SaveCommand;
    public override IRelayCommand FileValidateCommand => ValidateCommand;

    protected ObjectTreeDocumentViewModelBase(
        string filePath, string titlePrefix, string keyText, ObjectTreeOptions treeOptions)
    {
        FilePath = filePath;
        Title = string.IsNullOrEmpty(keyText) ? titlePrefix : $"{titlePrefix} — {keyText}";
        treeOptions.LabelTranslator ??= TreeLabels.Translate;
        Builder = new ObjectTreeBuilder(treeOptions);
        // Every builder-made label goes through the translator again on Refresh, so a language
        // switch only needs the labels recomputed.
        RegisterCultureHandler((_, _) =>
        {
            foreach (var root in Roots)
                RefreshNodeLabels(root);
            RefreshToolbar();
            OnPropertyChanged(nameof(UndoToolTip));
            OnPropertyChanged(nameof(RedoToolTip));
            OnPropertyChanged(nameof(TreeFindWatermark));
        });
        DragDropHandler = new SiblingReorderDragDropHandler(this);
        DocumentChanged += (_, _) => OnDocumentChangedForXml();
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task Save()
    {
        FlushXmlEdit();
        if (HasXmlIssues)
        {
            StatusText = L("XmlView_SaveBlocked", XmlIssues.Count);
            return;
        }

        try
        {
            var issues = PerformValidation();
            if (!await ConfirmSaveAfterValidationAsync(issues, Issues))
            {
                var errs = issues.Count(i => i.Severity == ValidationSeverity.Error);
                StatusText = L("Status_SaveCancelled", errs);
                return;
            }

            foreach (var root in Roots)
                RefreshNodeLabels(root);
            if (SaveText is { } text)
            {
                FileUtilities.FileWriteText(FilePath, text);
                if (RootObject is IObjectSerializeFile serializeFile) serializeFile.SetObjectFilePath(FilePath);
            }
            else
            {
                XmlCodec.SerializeToFile(RootObject, FilePath);
            }
            MarkSaved();
            IsDirty = false;
            StatusText = L("Status_Saved", Path.GetFileName(FilePath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = L("Status_SaveFailed", ex.Message);
        }
    }

    [RelayCommand]
    private void Validate()
    {
        FlushXmlEdit();
        Issues.Clear();
        var found = PerformValidation();
        foreach (var issue in found)
            Issues.Add(issue);

        if (Issues.Count == 0)
        {
            StatusText = L("Status_ValidationPassed");
        }
        else
        {
            var errors = Issues.Count(i => i.Severity == ValidationSeverity.Error);
            var warnings = Issues.Count(i => i.Severity == ValidationSeverity.Warning);
            StatusText = L("Status_ValidationCompleted", Issues.Count, errors, warnings);
        }
    }

    /// <summary>Subclasses produce validation findings here. Default: none.</summary>
    protected virtual IReadOnlyList<ValidationIssue> PerformValidation() =>
        Array.Empty<ValidationIssue>();

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void Delete()
    {
        var node = SelectedTreeNode;
        if (node is null) return;
        if (GetDeleteAction(node) is not { } action) return;

        // No confirmation: Undo brings the node back, and the status line says so.
        action();
        var parent = node.Parent;
        parent?.Children.Remove(node);
        SelectedTreeNode = parent;
        MarkChanged();
        StatusText = L("Status_DeletedUndoable", node.Label, TreeShortcuts.Undo);
    }

    private bool CanDelete() => SelectedTreeNode is not null && GetDeleteAction(SelectedTreeNode) is not null;

    /// <summary>
    /// Subclasses return a closure that removes <paramref name="node"/> from its
    /// owning Polhem.Definition collection, or null when the node is not deletable
    /// (e.g. root nodes). The shell then drops the tree-side node.
    /// </summary>
    protected abstract Action? GetDeleteAction(ObjectTreeNode node);

    /// <summary>Builds the tree and the nodes for objects added later, with the subclass's options.</summary>
    protected ObjectTreeBuilder Builder { get; }

    /// <summary>The root of the tree, which the tree view binds.</summary>
    public ObjectTreeNode RootNode => Roots[0];

    /// <summary>The commands of the tree's context menu.</summary>
    public abstract TreeCommandProviderBase CommandProvider { get; }

    /// <summary>
    /// What the tree lets the user drag and where: by default, reordering a node within the define
    /// collection that holds it.
    /// </summary>
    public ITreeNodeDragDropHandler DragDropHandler { get; }

    /// <summary>The resource key of the icon shown before <paramref name="node"/>.</summary>
    public abstract string IconKeyFor(ObjectTreeNode node);

    /// <summary><see cref="IconKeyFor"/> as a delegate, for the tree view's icon selector binding.</summary>
    public Func<ObjectTreeNode, string> IconKeys => IconKeyFor;

    /// <summary>Builds the tree for <paramref name="root"/> and selects its root.</summary>
    protected void InitializeTree(object root)
    {
        Roots.Add(Builder.Build(root));
        SelectedTreeNode = Roots[0];
        StartHistory(root);
    }

    /// <summary>The first folder directly under the root whose collection is a <typeparamref name="TCollection"/>.</summary>
    protected ObjectTreeNode? FolderOf<TCollection>() =>
        RootNode.Children.FirstOrDefault(c => c.IsFolder && c.Value is TCollection);

    /// <summary>Walks up from <paramref name="node"/> to the first node that stands for a <typeparamref name="TValue"/>.</summary>
    protected static ObjectTreeNode? FindAncestor<TValue>(ObjectTreeNode? node)
    {
        for (var cur = node; cur != null; cur = cur.Parent)
            if (cur.Value is TValue) return cur;
        return null;
    }

    /// <summary>
    /// The nearest object above <paramref name="component"/> in the tree that is a <typeparamref name="TValue"/>, or
    /// <c>null</c>. It goes by the tree rather than by the selection, so the XML view, which selects nothing, can ask it.
    /// </summary>
    protected TValue? AncestorOf<TValue>(object component) where TValue : class
    {
        var node = Roots.SelectMany(Enumerate).FirstOrDefault(n => ReferenceEquals(n.Value, component));
        return FindAncestor<TValue>(node?.Parent)?.Value as TValue;
    }

    /// <summary>
    /// Builds a node for <paramref name="value"/>, which the caller has just added to the define
    /// object, attaches it under <paramref name="parent"/>, selects it and marks the document dirty.
    /// </summary>
    protected ObjectTreeNode AddNode(ObjectTreeNode parent, object value)
    {
        var node = Builder.Build(value);
        node.IsExpanded = false;
        parent.Children.Add(node);
        parent.IsExpanded = true;
        SelectedTreeNode = node;
        MarkChanged();
        return node;
    }

    /// <summary>
    /// Called after the property grid has written a property of the selected object: marks the
    /// document dirty and recomputes the labels from the selected node up to the root, since a
    /// label is formatted from the object's properties.
    /// </summary>
    /// <remarks>
    /// The grid's event carries the old and new values, a password among them, so nothing about
    /// the edit reaches <see cref="DocumentViewModelBase.StatusText"/>.
    /// </remarks>
    public void OnPropertyEdited()
    {
        MarkChanged();
        for (var node = SelectedTreeNode; node != null; node = node.Parent)
            node.Refresh();
        if (SelectedTreeNode is { } selected)
            OnSelectedObjectEdited(selected);
    }

    /// <summary>
    /// Occurs after every edit of the define object: a property written in the grid, a node added, deleted or
    /// reordered, or a command that changed the selected object.
    /// </summary>
    /// <remarks>
    /// <see cref="DocumentViewModelBase.IsDirty"/> cannot stand in for it: it raises no change notification
    /// for an edit made while the document is already dirty.
    /// </remarks>
    public event EventHandler? DocumentChanged;

    /// <summary>Marks the document dirty and raises <see cref="DocumentChanged"/>, after an edit of the define object.</summary>
    public void MarkChanged()
    {
        RecordHistory();
        IsDirty = !IsAtSavedState;
        RefreshToolbar();
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Occurs when the selected object changed outside the property grid, so the grid must read it again.
    /// </summary>
    public event EventHandler? EditorRefreshRequested;

    /// <summary>
    /// Marks the document dirty, recomputes the labels from the selected node up to the root, and asks the property
    /// grid to read the selected object again, after a command changed that object.
    /// </summary>
    protected void OnSelectedObjectChangedByCommand()
    {
        MarkChanged();
        for (var node = SelectedTreeNode; node != null; node = node.Parent)
            node.Refresh();
        EditorRefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Lets a subclass bring the tree in line after the grid wrote a property of <paramref name="node"/>'s object,
    /// for example by adding a node the new value calls for.
    /// </summary>
    protected virtual void OnSelectedObjectEdited(ObjectTreeNode node)
    {
    }

    private static void RefreshNodeLabels(ObjectTreeNode node)
    {
        node.Refresh();
        foreach (var child in node.Children)
            RefreshNodeLabels(child);
    }
}
