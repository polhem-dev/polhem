using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Collections;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.ObjectTree;
using Polhem.Definition.Spreadsheet;
using Polhem.Expressions;
using Avalonia.Platform.Storage;
using Polhem.DefineEditor.Models;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.Views;
using CommunityToolkit.Mvvm.Input;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// FormSchema editor. Loads the schema via <see cref="XmlCodec"/>, shows it as an
/// <see cref="ObjectTreeBuilder"/> tree (Tables and Rules folders, fields directly under
/// each table) with the selected object in the property grid, and offers Add commands for
/// tables, fields, rules and mappings. Each field with a relation also gets a Relation group node, which the
/// annotations cannot express; see <see cref="AddFieldGroups"/>. A stored field's relation is set up in
/// <see cref="RelationDialog"/>, opened beside its RelationProgId or its RelationFieldMappings in the grid. A field's
/// list items are edited in the grid's collection dialog.
/// </summary>
public sealed partial class FormSchemaDocumentViewModel : ObjectTreeDocumentViewModelBase
{
    private const string RelationLabel = "Relation";

    // NOTE: The category FormField declares on RelationProgId, RelationFieldMappings and DisplayFields.
    private const string RelationCategory = "Relation";

    public override string TabIcon => "DefFormSchema";

    public FormSchema Schema { get; }

    public SolutionContext Solution { get; }

    /// <summary>Backing object handed to <see cref="XmlCodec.SerializeToFile"/> by base's Save.</summary>
    protected override object RootObject => Schema;

    public override ITreeNodeCommandProvider CommandProvider { get; }

    /// <summary>
    /// The object the property grid shows: a Relation group shows the field that owns it,
    /// narrowed to its Relation category by <see cref="SelectedPropertyFilter"/>; any other node shows
    /// its own object.
    /// </summary>
    public override object? SelectedEditorContext => SelectedTreeNode switch
    {
        null => null,
        var node when IsRelationGroup(node) => OwningField(node),
        _ => base.SelectedEditorContext,
    };

    /// <inheritdoc/>
    public override Func<PropertyDescriptor, bool>? SelectedPropertyFilter =>
        SelectedTreeNode is { } node && IsRelationGroup(node)
            ? static p => string.Equals(p.Category, RelationCategory, StringComparison.Ordinal)
            : null;

    /// <inheritdoc/>
    public override Func<PropertyDescriptor, object, IReadOnlyList<string>?>? ValueSuggestionProvider => Suggest;

    /// <inheritdoc/>
    public override Func<CollectionEditContext, Task<bool>?>? CollectionEditorProvider => EditMappingsInTree;

    /// <inheritdoc/>
    public override Func<PropertyDescriptor, object, Func<Task<bool>>?>? ValueEditorProvider => ProvideRelationEditor;

    private FormSchemaDocumentViewModel(string filePath, FormSchema schema, SolutionContext solution)
        // The root, its folders and the tables start expanded; fields start collapsed.
        : base(filePath, "FormSchema", schema.ProgId, new ObjectTreeOptions { ExpandDepth = 3, NodeBuilt = AddFieldGroups })
    {
        Schema = schema;
        Solution = solution;
        CommandProvider = new FormSchemaCommandProvider(this);
        InitializeTree(schema);
    }

    public static FormSchemaDocumentViewModel Load(string filePath, SolutionContext solution)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException("FormSchema file not found.", filePath);

        var schema = XmlCodec.DeserializeFromFile<FormSchema>(filePath)
            ?? throw new InvalidOperationException($"FormSchema deserialized to null: {filePath}");
        return new FormSchemaDocumentViewModel(filePath, schema, solution);
    }

    protected override IReadOnlyList<ValidationIssue> PerformValidation()
        => FormSchemaValidator.Validate(Schema, Solution.AvailableProgIds)
            .Select(issue => new ValidationIssue(
                issue.Severity == IssueSeverity.Error ? ValidationSeverity.Error : ValidationSeverity.Warning,
                issue.Location, issue.Message))
            .Concat(s_expressionCalculator.ValidateExpressions(Schema).Select(ToValidationIssue))
            .ToList();

    /// <summary>
    /// Parses the schema's expressions with the evaluator the runtime uses. An expression that does not parse
    /// fails the server's save, and on the client it silently stops the live computation, so it is an error here.
    /// </summary>
    /// <remarks>Shared across documents: the evaluator is thread-safe and building its interpreter is not free.</remarks>
    private static readonly FormExpressionCalculator s_expressionCalculator = new(new DynamicExpressoEvaluator());

    private static ValidationIssue ToValidationIssue(FormExpressionIssue issue)
    {
        var owner = issue.FieldName is not null
            ? $"{issue.TableName}.{issue.FieldName}"
            : $"Rules.{issue.RuleId}";
        return new ValidationIssue(ValidationSeverity.Error, $"{owner}.{issue.PropertyName}", issue.Message);
    }

    /// <summary>
    /// Adds the Relation group under a field node when the field has a relation.
    /// <see cref="FieldMappingCollection"/> carries no <c>[TreeNode]</c>, so the group label comes from here.
    /// </summary>
    private static void AddFieldGroups(ObjectTreeNode node, ObjectTreeBuilder builder)
    {
        if (node.Value is not FormField field) return;
        if (!string.IsNullOrEmpty(field.RelationProgId) || field.RelationFieldMappings is { Count: > 0 })
            node.Children.Add(CreateGroup(builder, field.RelationFieldMappings!, RelationLabel));
    }

    private static ObjectTreeNode CreateGroup(ObjectTreeBuilder builder, System.Collections.IEnumerable items, string label)
    {
        var group = new ObjectTreeNode(items, _ => TreeLabels.Translate(label), isFolder: true);
        foreach (var item in items)
            group.Children.Add(builder.Build(item));
        return group;
    }

    private static FormField? OwningField(ObjectTreeNode node) => node.Parent?.Value as FormField;

    public static bool IsRelationGroup(ObjectTreeNode node) =>
        node.IsFolder && OwningField(node) is { } f && ReferenceEquals(node.Value, f.RelationFieldMappings);

    public override string IconKeyFor(ObjectTreeNode node) => node switch
    {
        { Value: FormSchema } => "DefFormSchema",
        { Value: FormTableCollection or FormTable } => "IconTable",
        { Value: FormField field } => field.Type switch
        {
            FieldType.DbField => "IconColumn",
            FieldType.RelationField => "IconLink",
            _ => "IconText",
        },
        _ when IsRelationGroup(node) => "IconLink",
        { Value: FormRuleCollection } => "IconList",
        { Value: FieldMapping } => "IconArrowRight",
        { Value: FormRule } => "IconDot",
        _ => "DefUnknown",
    };

    [RelayCommand(CanExecute = nameof(CanAddTable))]
    private void AddTable()
    {
        var folder = FindAncestor<FormTableCollection>(SelectedTreeNode) ?? FolderOf<FormTableCollection>();
        if (folder is null) return;

        var name = UniqueKey(Schema.Tables!.Select(t => t.TableName), "NewTable");
        var table = new FormTable(name, "New table");
        Schema.Tables!.Add(table);
        AddNode(folder, table);
        StatusText = L("Status_AddedNamed", "FormTable", name);
    }

    private bool CanAddTable() => SelectedTreeNode is not null;

    [RelayCommand(CanExecute = nameof(CanAddField))]
    private void AddField()
    {
        var tableNode = FindAncestor<FormTable>(SelectedTreeNode);
        if (tableNode?.Value is not FormTable table) return;

        var name = UniqueKey(table.Fields!.Select(f => f.FieldName), "new_field");
        var field = new FormField(name, "New field", FieldDbType.String);
        table.Fields!.Add(field);
        AddNode(tableNode, field);
        StatusText = L("Status_AddedNamed", "FormField", name);
    }

    private bool CanAddField() => FindAncestor<FormTable>(SelectedTreeNode) is not null;

    [RelayCommand(CanExecute = nameof(CanAddRule))]
    private void AddRule()
    {
        var folder = FindAncestor<FormRuleCollection>(SelectedTreeNode) ?? FolderOf<FormRuleCollection>();
        if (folder is null) return;

        var name = UniqueKey(Schema.Rules!.Select(r => r.RuleId), "new_rule");
        // `true` holds for every record, so the new rule refuses nothing until its condition is written.
        var rule = new FormRule(name, "true", "New rule");
        Schema.Rules!.Add(rule);
        AddNode(folder, rule);
        StatusText = L("Status_AddedNamed", "FormRule", name);
    }

    private bool CanAddRule() => SelectedTreeNode is not null;

    [RelayCommand(CanExecute = nameof(CanAddMapping))]
    private void AddRelationMapping()
    {
        var fieldNode = FindAncestor<FormField>(SelectedTreeNode);
        if (fieldNode?.Value is not FormField field) return;

        var mapping = new FieldMapping(string.Empty, string.Empty);
        field.RelationFieldMappings!.Add(mapping);

        var group = EnsureRelationGroup(fieldNode, field);
        AddNode(group, mapping);
        fieldNode.IsExpanded = true;
        StatusText = L("Status_AddedNamed", "Relation FieldMapping", "");
    }

    private bool CanAddMapping() => FindAncestor<FormField>(SelectedTreeNode) is not null;

    /// <summary>
    /// Returns the Relation group of <paramref name="fieldNode"/>, adding it when the field has none yet.
    /// </summary>
    private ObjectTreeNode EnsureRelationGroup(ObjectTreeNode fieldNode, FormField field)
    {
        var mappings = field.RelationFieldMappings!;
        var existing = fieldNode.Children.FirstOrDefault(c => c.IsFolder && ReferenceEquals(c.Value, mappings));
        if (existing is not null) return existing;
        var group = CreateGroup(Builder, mappings, RelationLabel);
        fieldNode.Children.Insert(0, group);
        return group;
    }

    /// <summary>
    /// After the grid set a field's RelationProgId, adds the Relation group, so its mappings can be added from the tree.
    /// </summary>
    protected override void OnSelectedObjectEdited(ObjectTreeNode node)
    {
        var fieldNode = FindAncestor<FormField>(node);
        if (fieldNode?.Value is not FormField field) return;
        if (!string.IsNullOrEmpty(field.RelationProgId))
            EnsureRelationGroup(fieldNode, field);
    }

    /// <summary>
    /// The names a field's LangEnumName can take: the enums of this form's own language namespace by their bare name
    /// first, since a bare name resolves against the schema's ProgId, then every enum of the solution's Language
    /// files by its full <c>Namespace.Name</c>.
    /// </summary>
    private IReadOnlyList<string> LangEnumNames()
    {
        var enums = Solution.LanguageEnums();
        var own = enums.Where(e => string.Equals(e.Namespace, Schema.ProgId, StringComparison.Ordinal)).Select(e => e.Name);
        var qualified = enums.Select(e => $"{e.Namespace}.{e.Name}");
        return own.Concat(qualified).Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// The values the grid offers: the solution's form ids for a field's RelationProgId, the
    /// language enums for its LangEnumName; for a
    /// mapping, the fields of its own table as the destination and the fields of the related form's master table as
    /// the source.
    /// </summary>
    public IReadOnlyList<string>? Suggest(PropertyDescriptor property, object component)
    {
        switch (component)
        {
            case FormField when property.Name == nameof(FormField.RelationProgId):
                return Solution.AvailableProgIds.Count > 0 ? Solution.AvailableProgIds : null;
            case FormField when property.Name == nameof(FormField.LangEnumName):
                return LangEnumNames() is { Count: > 0 } enumNames ? enumNames : null;
            case FieldMapping:
                var groupNode = SelectedTreeNode?.Parent;
                if (groupNode is null || OwningField(groupNode) is not { } field) return null;
                if (property.Name == nameof(FieldMapping.DestinationField))
                {
                    return field.Table?.Fields?.Select(f => f.FieldName).Where(n => !string.IsNullOrEmpty(n)).ToArray();
                }
                if (property.Name == nameof(FieldMapping.SourceField))
                {
                    return Solution.MasterFieldNames(field.RelationProgId) is { Count: > 0 } names ? names : null;
                }
                return null;
            default:
                return null;
        }
    }

    /// <summary>
    /// Opens a field's relation mappings where they are edited: a stored field's in <see cref="RelationDialog"/>; any
    /// other field's by selecting its Relation group node, adding the group when the field has none yet. Every other
    /// collection opens in the grid's dialog.
    /// </summary>
    public Task<bool>? EditMappingsInTree(CollectionEditContext context)
    {
        if (context.Component is not FormField field
            || context.Property.Name != nameof(FormField.RelationFieldMappings))
        {
            return null;
        }
        if (CanSetUpRelation(field)) return EditRelationAsync(field);
        var fieldNode = FindAncestor<FormField>(SelectedTreeNode);
        if (fieldNode is null || !ReferenceEquals(fieldNode.Value, field)) return Task.FromResult(false);
        var group = EnsureRelationGroup(fieldNode, field);
        fieldNode.IsExpanded = true;
        SelectedTreeNode = group;
        // Nothing in the collection changed; the grid must not report an edit.
        return Task.FromResult(false);
    }

    /// <summary>
    /// The relation dialog beside a stored field's RelationProgId; no editor for any other property.
    /// </summary>
    public Func<Task<bool>>? ProvideRelationEditor(PropertyDescriptor property, object component) =>
        component is FormField field && property.Name == nameof(FormField.RelationProgId) && CanSetUpRelation(field)
            ? () => EditRelationAsync(field)
            : null;

    // A foreign key holds the picked row's id, so only a stored field can carry a relation the dialog sets up.
    private static bool CanSetUpRelation(FormField field) => field.Type == FieldType.DbField && field.Table is not null;

    /// <summary>
    /// Shows <see cref="RelationDialog"/> for <paramref name="field"/> and applies what the user confirms; returns
    /// whether the field changed.
    /// </summary>
    private async Task<bool> EditRelationAsync(FormField field)
    {
        var owner = GetOwnerWindow();
        if (owner is null) return false; // smoke / headless
        var dialog = new RelationDialogViewModel(Schema, field, Solution);
        if (!await RelationDialog.ShowAsync(owner, dialog)) return false;
        if (dialog.IsRemoving && !await ConfirmationDialog.ShowAsync(owner,
                L("Confirm_RemoveRelationTitle"),
                L("Confirm_RemoveRelationMessage", field.FieldName),
                confirmLabel: L("Action_RemoveRelation"),
                cancelLabel: L("Action_Cancel")))
        {
            return false;
        }
        ApplyRelationSetup(dialog);
        return true;
    }

    /// <summary>
    /// Writes the result of <paramref name="dialog"/> to its field, then brings the tree in line: the field's Relation
    /// group is rebuilt from its new mappings, and the fields the setup added or removed gain or lose their nodes.
    /// </summary>
    public RelationSetupChanges ApplyRelationSetup(RelationDialogViewModel dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        var field = dialog.Field;
        var table = field.Table!;
        var wasGroupSelected = SelectedTreeNode is { } selected && IsRelationGroup(selected)
            && ReferenceEquals(OwningField(selected), field);
        var changes = dialog.ApplyTo();

        var tableNode = FindNode(RootNode, table);
        if (tableNode is not null)
        {
            foreach (var removed in changes.RemovedFields)
            {
                if (tableNode.Children.FirstOrDefault(c => ReferenceEquals(c.Value, removed)) is { } node)
                    tableNode.Children.Remove(node);
            }
            foreach (var added in changes.AddedFields)
            {
                var index = table.Fields!.IndexOf(added);
                var previous = index > 0 ? table.Fields![index - 1] : null;
                var after = previous is null ? -1 : tableNode.Children.ToList().FindIndex(c => ReferenceEquals(c.Value, previous));
                var node = Builder.Build(added);
                node.IsExpanded = false;
                tableNode.Children.Insert(after + 1, node);
            }
        }

        var fieldNode = tableNode?.Children.FirstOrDefault(c => ReferenceEquals(c.Value, field));
        if (fieldNode is not null)
        {
            var mappings = field.RelationFieldMappings!;
            if (fieldNode.Children.FirstOrDefault(c => c.IsFolder && ReferenceEquals(c.Value, mappings)) is { } stale)
                fieldNode.Children.Remove(stale);
            if (!string.IsNullOrEmpty(field.RelationProgId) || mappings.Count > 0)
            {
                var group = EnsureRelationGroup(fieldNode, field);
                if (wasGroupSelected) SelectedTreeNode = group;
            }
            else if (wasGroupSelected)
            {
                SelectedTreeNode = fieldNode;
            }
            fieldNode.Refresh();
        }

        MarkChanged();
        StatusText = changes.AddedFields.Count > 0
            ? L("Status_RelationAppliedWithFields", field.FieldName, string.Join(", ", changes.AddedFields.Select(f => f.FieldName)))
            : L("Status_RelationApplied", field.FieldName);
        return changes;
    }

    private static ObjectTreeNode? FindNode(ObjectTreeNode node, object value)
    {
        if (ReferenceEquals(node.Value, value)) return node;
        foreach (var child in node.Children)
        {
            if (FindNode(child, value) is { } found) return found;
        }
        return null;
    }

    /// <summary>
    /// Generates the <see cref="FormLayout"/> definition for this schema and writes it to
    /// <c>{DefinePath}/FormLayout/{ProgId}.FormLayout.xml</c>.
    /// </summary>
    /// <remarks>
    /// This is where a layout comes from: the run time renders the stored definition and never
    /// generates one, so every FormSchema needs this run once (POLHEM2005 reports the ones that
    /// have not had it). Regenerating discards any hand-tuning of the existing file, which is why
    /// an existing target is confirmed first — the only destructive thing this command does.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanGenerateFormLayout))]
    private async Task GenerateFormLayoutAsync()
    {
        string progId = Schema.ProgId;
        if (string.IsNullOrWhiteSpace(progId)) return;

        // {DefinePath}/FormSchema/{progId}.FormSchema.xml -> {DefinePath}
        string? schemaDir = Path.GetDirectoryName(FilePath);
        string? definePath = schemaDir is null ? null : Path.GetDirectoryName(schemaDir);
        if (definePath is null) return;

        string targetDir = Path.Combine(definePath, "FormLayout");
        string targetPath = Path.Combine(targetDir, $"{progId}.FormLayout.xml");

        if (File.Exists(targetPath) && !await ConfirmOverwriteFormLayoutAsync(progId))
            return;

        Directory.CreateDirectory(targetDir);
        XmlCodec.SerializeToFile(FormLayoutGenerator.Generate(Schema, progId), targetPath);

        StatusText = L("Status_FormLayoutGenerated", $"{progId}.FormLayout.xml");
        OnDefineFileGenerated(targetPath);
    }

    /// <summary>
    /// Raised when the user asks for a command that the main window carries out on this form: syncing its derived
    /// files or upgrading its tables.
    /// </summary>
    public event EventHandler<FormSchemaAction>? ActionRequested;

    [RelayCommand(CanExecute = nameof(CanActOnForm))]
    private void SyncDefinitions() => ActionRequested?.Invoke(this, FormSchemaAction.SyncDefinitions);

    [RelayCommand(CanExecute = nameof(CanActOnForm))]
    private void UpgradeTables() => ActionRequested?.Invoke(this, FormSchemaAction.UpgradeTables);

    [RelayCommand(CanExecute = nameof(CanActOnForm))]
    private void PreviewUpgradeTables() => ActionRequested?.Invoke(this, FormSchemaAction.PreviewUpgradeTables);

    /// <summary>
    /// Writes the form, as it is in the editor, to an Excel workbook together with its zh-TW texts and menu
    /// placement from the solution.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanGenerateFormLayout))]
    private async Task ExportWorkbookAsync()
    {
        string progId = Schema.ProgId;
        string? schemaDir = Path.GetDirectoryName(FilePath);
        string? definePath = schemaDir is null ? null : Path.GetDirectoryName(schemaDir);
        var owner = GetOwnerWindow();
        if (definePath is null || owner is null) return;

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = L("Dialog_SaveFormWorkbook"),
            SuggestedFileName = $"{progId}.xlsx",
            DefaultExtension = "xlsx",
            FileTypeChoices = [MainWindow.ExcelWorkbookFileType],
        });
        if (file is null) return;

        try
        {
            // The texts and the menu come from the files; the schema is the one being edited, saved or not.
            var saved = File.Exists(FilePath) ? FormExporter.Load(definePath, progId) : null;
            var content = new FormWorkbookContent(Schema);
            if (saved is not null)
            {
                foreach (var (key, text) in saved.ZhTw) content.ZhTw[key] = text;
                content.Menu.FolderId = saved.Menu.FolderId;
                content.Menu.FolderCaption = saved.Menu.FolderCaption;
                content.Menu.FolderCaptionZhTw = saved.Menu.FolderCaptionZhTw;
                content.Menu.EntryCaption = saved.Menu.EntryCaption;
                content.Menu.EntryCaptionZhTw = saved.Menu.EntryCaptionZhTw;
            }

            // The headers and instructions follow the editor's language; the values are the same in either.
            var language = FormWorkbookLanguages.FromCulture(LocalizationService.Current.Culture);
            await using var stream = await file.OpenWriteAsync();
            FormWorkbookWriter.Write(content, stream, language);
            StatusText = L("Status_FormWorkbookExported", file.Name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            StatusText = L("Status_FormWorkbookExportFailed", ex.Message);
        }
    }

    private bool CanGenerateFormLayout() =>
        SelectedTreeNode?.Value is FormSchema && !string.IsNullOrWhiteSpace(Schema.ProgId);

    // The sync and upgrade commands act on the whole form, so they do not depend on the selected node: the File menu
    // runs them whatever node is selected, while the tree's context menu offers them on the schema node only.
    private bool CanActOnForm() => !string.IsNullOrWhiteSpace(Schema.ProgId);

    private static async Task<bool> ConfirmOverwriteFormLayoutAsync(string progId)
    {
        var owner = GetOwnerWindow();
        if (owner is null) return true; // smoke / headless
        return await ConfirmationDialog.ShowAsync(
            owner,
            L("Confirm_OverwriteFormLayoutTitle"),
            L("Confirm_OverwriteFormLayoutMessage", $"{progId}.FormLayout.xml"),
            confirmLabel: L("Action_Overwrite"),
            cancelLabel: L("Action_Cancel"));
    }

    protected override Action? GetDeleteAction(ObjectTreeNode node) => node.Value switch
    {
        FormTable t => () => Schema.Tables!.Remove(t),
        FormField f when node.Parent?.Value is FormTable t => () => t.Fields!.Remove(f),
        FieldMapping m when node.Parent?.Value is FieldMappingCollection mappings => () => mappings.Remove(m),
        FormRule r => () => Schema.Rules!.Remove(r),
        _ => null,
    };
}
