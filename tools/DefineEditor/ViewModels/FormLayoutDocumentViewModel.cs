using System.ComponentModel;
using System.Diagnostics;
using Polhem.Core.Serialization;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.ObjectTree;
using Polhem.DefineEditor.BrowserPreview;
using Polhem.DefineEditor.Models;
using Polhem.DefineEditor.Services;
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
    public FormLayout Root { get; private set; }
    protected override object RootObject => Root;

    protected override void ReplaceRoot(object root) => Root = (FormLayout)root;

    public override string TabIcon => "DefFormLayout";

    public override TreeCommandProviderBase CommandProvider { get; }

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

    /// <summary>What draws the preview. It starts as the editor's own Avalonia rendering, which needs no web server.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWebPreview))]
    private FormLayoutPreviewEngine _previewEngine;

    /// <summary>Whether the preview is drawn by <see cref="FormLayoutPreviewEngine.Web"/>; the switch in the preview's header.</summary>
    public bool IsWebPreview
    {
        get => PreviewEngine == FormLayoutPreviewEngine.Web;
        set => PreviewEngine = value ? FormLayoutPreviewEngine.Web : FormLayoutPreviewEngine.Avalonia;
    }

    /// <summary>The form mode the preview renders in.</summary>
    public SingleFormMode PreviewFormMode => IsPreviewViewMode ? SingleFormMode.View : SingleFormMode.Edit;

    /// <summary>
    /// Captures the layout, unsaved edits included, and the preview settings for the browser preview.
    /// </summary>
    /// <remarks>
    /// The layout goes out as the XML it would be saved as: the browser renders on another thread, so it must never
    /// read <see cref="Root"/>, which the editor changes in place.
    /// </remarks>
    public BrowserPreviewSnapshot CreateBrowserPreviewSnapshot()
    {
        var title = string.IsNullOrEmpty(Root.Caption) ? Root.LayoutId : Root.Caption;
        try
        {
            return new BrowserPreviewSnapshot(title, XmlCodec.Serialize(Root), PreviewFormMode, IsPreviewCompact, ShowPreviewFieldNames);
        }
        catch (InvalidOperationException ex)
        {
            return new BrowserPreviewSnapshot(title, string.Empty, PreviewFormMode, IsPreviewCompact, ShowPreviewFieldNames, ex.Message);
        }
    }

    /// <summary>
    /// Starts <paramref name="host"/> if it is not running, publishes this layout to it and returns the layout's
    /// preview URL, which the default browser and the web preview inside the editor both open.
    /// </summary>
    /// <remarks>
    /// The snapshot goes out once here because the editor publishes on later edits only while the server runs.
    /// Starting fails with <see cref="InvalidOperationException"/> or <see cref="IOException"/>, as
    /// <see cref="BrowserPreviewHost.StartAsync"/> describes.
    /// </remarks>
    /// <param name="host">The server to publish to; the editor passes <see cref="BrowserPreviewHost.Shared"/>.</param>
    public async Task<Uri> PublishBrowserPreviewAsync(BrowserPreviewHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        var documentId = host.Hub.GetDocumentId(DocumentKey);
        var url = await host.GetDocumentUrlAsync(documentId);
        host.Hub.Publish(documentId, CreateBrowserPreviewSnapshot());
        return url;
    }

    /// <summary>
    /// Opens this layout in the default web browser, starting the browser preview server on first use. A server
    /// that cannot start, or a browser that cannot be launched, is reported in <see cref="DocumentViewModelBase.StatusText"/>.
    /// </summary>
    [RelayCommand]
    private async Task OpenInBrowserAsync()
    {
        try
        {
            var url = await PublishBrowserPreviewAsync(BrowserPreviewHost.Shared);
            Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
        }
        catch (InvalidOperationException ex)
        {
            ReportBrowserPreviewFailure(ex);
        }
        catch (IOException ex)
        {
            ReportBrowserPreviewFailure(ex);
        }
        catch (Win32Exception ex)
        {
            ReportBrowserPreviewFailure(ex);
        }
    }

    private void ReportBrowserPreviewFailure(Exception ex)
        => StatusText = string.Format(LocalizationService.Current.Culture,
            LocalizationService.Current["Status_BrowserPreviewFailed"], ex.Message);

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

    private FormLayoutDocumentViewModel(string filePath, FormLayout root, SolutionContext solution)
        // The root and the two folders start expanded.
        : base(filePath, "FormLayout", keyText: root.LayoutId, new ObjectTreeOptions { ExpandDepth = 2 })
    {
        Root = root;
        Solution = solution;
        CommandProvider = new FormLayoutCommandProvider(this);
        InitializeTree(root);
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SelectedTreeNode))
                EditPropertiesCommand.NotifyCanExecuteChanged();
        };
    }

    /// <summary>The solution the layout belongs to, which supplies its form's fields and tables.</summary>
    public SolutionContext Solution { get; }

    public static FormLayoutDocumentViewModel Load(string filePath, SolutionContext? solution = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException("FormLayout file not found.", filePath);
        var root = XmlCodec.DeserializeFromFile<FormLayout>(filePath)
            ?? throw new InvalidOperationException($"FormLayout deserialized to null: {filePath}");
        return new FormLayoutDocumentViewModel(filePath, root, solution ?? SolutionContext.Empty);
    }

    public override Func<PropertyDescriptor, object, IReadOnlyList<string>?>? ValueSuggestionProvider => Suggest;

    /// <summary>
    /// The values the grid offers, read from the layout's FormSchema: the solution's form ids for the layout's ProgId,
    /// the master table's fields for a section field, the detail tables for a grid's TableName, and the grid table's
    /// fields for a column.
    /// </summary>
    /// <remarks>The grid still takes a typed value; nothing is offered when the form's FormSchema cannot be read.</remarks>
    public IReadOnlyList<string>? Suggest(PropertyDescriptor property, object component)
    {
        switch (component)
        {
            case FormLayout when property.Name == nameof(FormLayout.ProgId):
                return Solution.AvailableProgIds.Count > 0 ? Solution.AvailableProgIds : null;
            case LayoutField when property.Name == nameof(LayoutField.FieldName):
                return FieldNames(Solution.LoadFormSchema(Root.ProgId)?.MasterTable);
            case LayoutGrid when property.Name == nameof(LayoutGrid.TableName):
                var schema = Solution.LoadFormSchema(Root.ProgId);
                var names = schema?.Tables?.Where(t => !ReferenceEquals(t, schema.MasterTable))
                    .Select(t => t.TableName).Where(n => !string.IsNullOrEmpty(n)).ToArray();
                return names is { Length: > 0 } ? names : null;
            case LayoutColumn column when property.Name == nameof(LayoutColumn.FieldName):
                return AncestorOf<LayoutGrid>(column) is { } grid
                    ? FieldNames(Solution.LoadFormSchema(Root.ProgId)?.Tables?.GetOrDefault(grid.TableName))
                    : null;
            default:
                return null;
        }
    }

    private static IReadOnlyList<string>? FieldNames(FormTable? table) =>
        table?.Fields?.Select(f => f.FieldName).Where(n => !string.IsNullOrEmpty(n)).ToArray() is { Length: > 0 } names
            ? names
            : null;

    [RelayCommand]
    private void SetPaneLayout(FormLayoutPaneLayout layout) => PaneLayout = layout;

    // The preview is the only thing on the side, so a collapsed preview would leave it empty.
    partial void OnPaneLayoutChanged(FormLayoutPaneLayout value)
    {
        if (value == FormLayoutPaneLayout.PreviewOnly)
            IsPreviewVisible = true;
        LeavePreviewFocusIfChanged();
        // The tree offers Edit Properties only while the property grid is hidden.
        RefreshToolbar();
    }

    partial void OnPreviewEngineChanged(FormLayoutPreviewEngine value) => LeavePreviewFocusIfChanged();

    /// <summary>
    /// Whether the window is arranged for adjusting a layout against its web preview: the tree beside the web preview
    /// only, with the property grid in a dialog and the window's side bar hidden.
    /// </summary>
    [ObservableProperty]
    private bool _isPreviewFocused;

    /// <summary>
    /// Raised with <c>true</c> when <see cref="IsPreviewFocused"/> turns on and <c>false</c> when it turns off, so the
    /// window hides its side bar and brings it back.
    /// </summary>
    public event EventHandler<bool>? PreviewFocusChanged;

    private (FormLayoutPaneLayout Layout, FormLayoutPreviewEngine Engine)? _beforePreviewFocus;

    /// <summary>
    /// Arranges the window for adjusting the layout against its web preview, or puts back what was there before.
    /// </summary>
    [RelayCommand]
    private void TogglePreviewFocus()
    {
        if (_beforePreviewFocus is { } before)
        {
            _beforePreviewFocus = null;
            IsPreviewFocused = false;
            PaneLayout = before.Layout;
            PreviewEngine = before.Engine;
            PreviewFocusChanged?.Invoke(this, false);
            return;
        }

        var saved = (PaneLayout, PreviewEngine);
        PaneLayout = FormLayoutPaneLayout.PreviewOnly;
        PreviewEngine = FormLayoutPreviewEngine.Web;
        _beforePreviewFocus = saved;
        IsPreviewFocused = true;
        PreviewFocusChanged?.Invoke(this, true);
    }

    // Choosing another side layout or preview by hand ends the focus without undoing that choice; the side bar comes
    // back, since what hid it is over.
    private void LeavePreviewFocusIfChanged()
    {
        if (_beforePreviewFocus is null
            || (PaneLayout == FormLayoutPaneLayout.PreviewOnly && PreviewEngine == FormLayoutPreviewEngine.Web))
            return;
        _beforePreviewFocus = null;
        IsPreviewFocused = false;
        PreviewFocusChanged?.Invoke(this, false);
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
