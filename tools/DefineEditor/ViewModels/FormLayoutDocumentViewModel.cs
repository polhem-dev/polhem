using Polhem.Core.Serialization;
using Polhem.Definition.Layouts;
using Polhem.Definition.ObjectTree;
using Polhem.DefineEditor.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Editor for <see cref="FormLayout"/>. The tree is built by the framework's
/// <see cref="ObjectTreeBuilder"/> from the <c>[TreeNode]</c> annotations:
/// FormLayout → Sections folder → LayoutSection[] → LayoutField[];
/// FormLayout → Details folder → LayoutGrid[] → LayoutColumn[].
/// </summary>
public sealed partial class FormLayoutDocumentViewModel : ObjectTreeDocumentViewModelBase
{
    public FormLayout Root { get; }
    protected override object RootObject => Root;

    public override string TabIcon => "DefFormLayout";

    public override ITreeNodeCommandProvider CommandProvider { get; }

    /// <summary>Whether the live preview under the property grid is expanded.</summary>
    [ObservableProperty]
    private bool _isPreviewVisible = true;

    /// <summary>Whether the preview shows the form in View mode instead of Edit mode.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewFormMode))]
    private bool _isPreviewViewMode;

    /// <summary>Whether the preview renders the phone-sized (compact) form.</summary>
    [ObservableProperty]
    private bool _isPreviewCompact;

    /// <summary>Whether empty preview fields show their field name as a placeholder.</summary>
    [ObservableProperty]
    private bool _showPreviewFieldNames = true;

    /// <summary>The form mode the preview renders in.</summary>
    public SingleFormMode PreviewFormMode => IsPreviewViewMode ? SingleFormMode.View : SingleFormMode.Edit;

    /// <summary>What the right-hand side shows: the property grid, the preview, or both.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPropertyPaneVisible))]
    [NotifyPropertyChangedFor(nameof(IsPreviewPaneVisible))]
    [NotifyPropertyChangedFor(nameof(IsPropertiesAndPreviewLayout))]
    [NotifyPropertyChangedFor(nameof(IsPreviewOnlyLayout))]
    [NotifyPropertyChangedFor(nameof(IsPropertiesOnlyLayout))]
    private FormLayoutPaneLayout _paneLayout;

    /// <summary>Whether the property grid is on the right-hand side.</summary>
    public bool IsPropertyPaneVisible => PaneLayout != FormLayoutPaneLayout.PreviewOnly;

    /// <summary>Whether the preview is on the right-hand side.</summary>
    public bool IsPreviewPaneVisible => PaneLayout != FormLayoutPaneLayout.PropertiesOnly;

    /// <summary>Whether <see cref="PaneLayout"/> is <see cref="FormLayoutPaneLayout.PropertiesAndPreview"/>.</summary>
    public bool IsPropertiesAndPreviewLayout => PaneLayout == FormLayoutPaneLayout.PropertiesAndPreview;

    /// <summary>Whether <see cref="PaneLayout"/> is <see cref="FormLayoutPaneLayout.PreviewOnly"/>.</summary>
    public bool IsPreviewOnlyLayout => PaneLayout == FormLayoutPaneLayout.PreviewOnly;

    /// <summary>Whether <see cref="PaneLayout"/> is <see cref="FormLayoutPaneLayout.PropertiesOnly"/>.</summary>
    public bool IsPropertiesOnlyLayout => PaneLayout == FormLayoutPaneLayout.PropertiesOnly;

    private FormLayoutDocumentViewModel(string filePath, FormLayout root)
        // The root and the two folders start expanded.
        : base(filePath, "FormLayout", keyText: root.LayoutId, new ObjectTreeOptions { ExpandDepth = 2 })
    {
        Root = root;
        CommandProvider = new FormLayoutCommandProvider(this);
        InitializeTree(root);
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SelectedTreeNode))
                EditPropertiesCommand.NotifyCanExecuteChanged();
        };
    }

    public static FormLayoutDocumentViewModel Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException("FormLayout file not found.", filePath);
        var root = XmlCodec.DeserializeFromFile<FormLayout>(filePath)
            ?? throw new InvalidOperationException($"FormLayout deserialized to null: {filePath}");
        return new FormLayoutDocumentViewModel(filePath, root);
    }

    [RelayCommand]
    private void SetPaneLayout(FormLayoutPaneLayout layout) => PaneLayout = layout;

    // The preview is the only thing on the side, so a collapsed preview would leave it empty.
    partial void OnPaneLayoutChanged(FormLayoutPaneLayout value)
    {
        if (value == FormLayoutPaneLayout.PreviewOnly)
            IsPreviewVisible = true;
    }

    /// <summary>
    /// Opens the selected node's properties in a dialog, for the layouts that leave the property grid off the side.
    /// </summary>
    /// <remarks>
    /// The dialog shows the same <see cref="ObjectTreeDocumentViewModelBase.SelectedEditorContext"/> in the same
    /// grid, so its edits reach the document exactly as the side's do.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanEditProperties))]
    private async Task EditProperties()
    {
        var owner = GetOwnerWindow();
        if (owner is null) return;
        var label = SelectedTreeNode?.Label;
        await Views.NodePropertiesDialog.ShowAsync(owner, this,
            L("NodePropertiesDialog_Title", string.IsNullOrEmpty(label) ? "—" : label));
    }
    private bool CanEditProperties() => SelectedEditorContext is not null;

    public override string IconKeyFor(ObjectTreeNode node) => node.Value switch
    {
        FormLayout => "DefFormLayout",
        LayoutSectionCollection or LayoutSection => "IconSection",
        LayoutGridCollection or LayoutGrid => "IconGrid",
        LayoutField => "IconText",
        LayoutColumn => "IconColumn",
        _ => "DefUnknown",
    };

    [RelayCommand(CanExecute = nameof(CanAddSection))]
    private void AddSection()
    {
        var folder = FolderOf<LayoutSectionCollection>();
        if (folder is null) return;
        var name = UniqueKey(Root.Sections!.Select(s => s.Name), "Section");
        var section = new LayoutSection { Name = name, Caption = "New section" };
        Root.Sections!.Add(section);
        AddNode(folder, section);
        StatusText = L("Status_AddedNamed", "LayoutSection", name);
    }
    private bool CanAddSection() => SelectedTreeNode is not null;

    [RelayCommand(CanExecute = nameof(CanAddLayoutField))]
    private void AddLayoutField()
    {
        var sectionNode = FindAncestor<LayoutSection>(SelectedTreeNode);
        if (sectionNode?.Value is not LayoutSection section) return;
        var name = UniqueKey(section.Fields!.Select(f => f.FieldName), "new_field");
        var field = new LayoutField { FieldName = name, Caption = "New field" };
        section.Fields!.Add(field);
        AddNode(sectionNode, field);
        StatusText = L("Status_AddedNamed", "LayoutField", name);
    }
    private bool CanAddLayoutField() => FindAncestor<LayoutSection>(SelectedTreeNode) is not null;

    [RelayCommand(CanExecute = nameof(CanAddGrid))]
    private void AddGrid()
    {
        var folder = FolderOf<LayoutGridCollection>();
        if (folder is null) return;
        var name = UniqueKey(Root.Details!.Select(g => g.TableName), "DetailTable");
        var grid = new LayoutGrid(name, "New detail grid");
        Root.Details!.Add(grid);
        AddNode(folder, grid);
        StatusText = L("Status_AddedNamed", "LayoutGrid", name);
    }
    private bool CanAddGrid() => SelectedTreeNode is not null;

    [RelayCommand(CanExecute = nameof(CanAddLayoutColumn))]
    private void AddLayoutColumn()
    {
        var gridNode = FindAncestor<LayoutGrid>(SelectedTreeNode);
        if (gridNode?.Value is not LayoutGrid grid) return;
        var name = UniqueKey(grid.Columns!.Select(c => c.FieldName), "new_column");
        var column = new LayoutColumn { FieldName = name, Caption = "New field" };
        grid.Columns!.Add(column);
        AddNode(gridNode, column);
        StatusText = L("Status_AddedNamed", "LayoutColumn", name);
    }
    private bool CanAddLayoutColumn() => FindAncestor<LayoutGrid>(SelectedTreeNode) is not null;

    protected override Action? GetDeleteAction(ObjectTreeNode node) => node.Value switch
    {
        LayoutSection s => () => Root.Sections!.Remove(s),
        LayoutField f when node.Parent?.Value is LayoutSection parentSection
            => () => parentSection.Fields!.Remove(f),
        LayoutGrid g => () => Root.Details!.Remove(g),
        LayoutColumn c when node.Parent?.Value is LayoutGrid parentGrid
            => () => parentGrid.Columns!.Remove(c),
        _ => null,
    };

    protected override IReadOnlyList<ValidationIssue> PerformValidation()
    {
        var issues = new List<ValidationIssue>();
        if (string.IsNullOrWhiteSpace(Root.LayoutId))
            issues.Add(new(ValidationSeverity.Error, "FormLayout", "LayoutId cannot be empty."));
        if (string.IsNullOrWhiteSpace(Root.ProgId))
            issues.Add(new(ValidationSeverity.Error, "FormLayout", "ProgId cannot be empty."));
        if (Root.ColumnCount <= 0)
            issues.Add(new(ValidationSeverity.Error, "FormLayout",
                $"ColumnCount must be greater than 0 (current value: {Root.ColumnCount})."));

        var sectionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in Root.Sections ?? Enumerable.Empty<LayoutSection>())
        {
            var sPath = string.IsNullOrEmpty(section.Name) ? "Sections[?]" : $"Sections.{section.Name}";
            if (string.IsNullOrWhiteSpace(section.Name))
                issues.Add(new(ValidationSeverity.Error, sPath, "LayoutSection.Name cannot be empty."));
            else if (!sectionNames.Add(section.Name))
                issues.Add(new(ValidationSeverity.Error, sPath,
                    $"Section.Name '{section.Name}' is a duplicate."));

            var fieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in section.Fields ?? Enumerable.Empty<LayoutField>())
            {
                var fPath = $"{sPath}.{(string.IsNullOrEmpty(f.FieldName) ? "(unnamed)" : f.FieldName)}";
                if (string.IsNullOrWhiteSpace(f.FieldName))
                    issues.Add(new(ValidationSeverity.Error, fPath, "LayoutField.FieldName cannot be empty."));
                else if (!fieldNames.Add(f.FieldName))
                    issues.Add(new(ValidationSeverity.Error, fPath,
                        $"LayoutField.FieldName '{f.FieldName}' is a duplicate within '{section.Name}'."));
            }
        }

        var gridNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var grid in Root.Details ?? Enumerable.Empty<LayoutGrid>())
        {
            var gPath = string.IsNullOrEmpty(grid.TableName) ? "Details[?]" : $"Details.{grid.TableName}";
            if (string.IsNullOrWhiteSpace(grid.TableName))
                issues.Add(new(ValidationSeverity.Error, gPath, "LayoutGrid.TableName cannot be empty."));
            else if (!gridNames.Add(grid.TableName))
                issues.Add(new(ValidationSeverity.Error, gPath,
                    $"LayoutGrid.TableName '{grid.TableName}' is a duplicate."));

            var colNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in grid.Columns ?? Enumerable.Empty<LayoutColumn>())
            {
                var cPath = $"{gPath}.{(string.IsNullOrEmpty(c.FieldName) ? "(unnamed)" : c.FieldName)}";
                if (string.IsNullOrWhiteSpace(c.FieldName))
                    issues.Add(new(ValidationSeverity.Error, cPath, "LayoutColumn.FieldName cannot be empty."));
                else if (!colNames.Add(c.FieldName))
                    issues.Add(new(ValidationSeverity.Error, cPath,
                        $"LayoutColumn.FieldName '{c.FieldName}' is a duplicate within '{grid.TableName}'."));
            }
        }
        return issues;
    }
}
