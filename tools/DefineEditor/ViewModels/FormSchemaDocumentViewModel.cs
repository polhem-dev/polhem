using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Collections;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.ObjectTree;
using Polhem.DefineEditor.Models;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.Views;
using CommunityToolkit.Mvvm.Input;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// FormSchema editor. Loads the schema via <see cref="XmlCodec"/>, shows it as an
/// <see cref="ObjectTreeBuilder"/> tree (Tables and Rules folders, fields directly under
/// each table), lets the right-pane DataTemplates two-way bind to the underlying
/// Polhem.Definition object, and offers Add commands for tables / fields / mappings /
/// list items. Each field also gets Relation / Lookup / ListItems group nodes, which the
/// annotations cannot express; see <see cref="AddFieldGroups"/>.
/// </summary>
public sealed partial class FormSchemaDocumentViewModel : ObjectTreeDocumentViewModelBase
{
    private const string RelationLabel = "Relation";
    private const string LookupLabel = "Lookup";
    private const string ListItemsLabel = "ListItems";

    public override string TabIcon => "DefFormSchema";

    public FormSchema Schema { get; }

    public SolutionContext Solution { get; }

    /// <summary>Backing object handed to <see cref="XmlCodec.SerializeToFile"/> by base's Save.</summary>
    protected override object RootObject => Schema;

    public override ITreeNodeCommandProvider CommandProvider { get; }

    /// <summary>
    /// Content shown in the right-pane <see cref="Avalonia.Controls.ContentControl"/>.
    /// The Relation / Lookup groups yield a <see cref="MappingGroupEditor"/>, the ListItems
    /// group yields its field, and everything else yields the node's object so the
    /// FormSchema / FormTable / FormField / FieldMapping / ListItem templates apply.
    /// </summary>
    public override object? SelectedEditorContext => SelectedTreeNode switch
    {
        null => null,
        var node when IsRelationGroup(node) => new MappingGroupEditor(OwningField(node)!, isRelation: true, Solution.AvailableProgIds),
        var node when IsLookupGroup(node) => new MappingGroupEditor(OwningField(node)!, isRelation: false, Solution.AvailableProgIds),
        var node when IsListItemsGroup(node) => OwningField(node),
        _ => base.SelectedEditorContext,
    };

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
        => FormSchemaValidator.Validate(Schema, Solution);

    /// <summary>
    /// Adds the Relation, Lookup and ListItems groups under a field node, each when the field
    /// uses it. <see cref="FieldMappingCollection"/> and <see cref="ListItemCollection"/> carry no
    /// <c>[TreeNode]</c>, and a field holds two mapping collections of the same type, which a
    /// class-level annotation could not label apart.
    /// </summary>
    private static void AddFieldGroups(ObjectTreeNode node, ObjectTreeBuilder builder)
    {
        if (node.Value is not FormField field) return;
        if (!string.IsNullOrEmpty(field.RelationProgId) || field.RelationFieldMappings is { Count: > 0 })
            node.Children.Add(CreateGroup(builder, field.RelationFieldMappings!, RelationLabel));
        if (!string.IsNullOrEmpty(field.LookupProgId) || field.LookupFieldMappings is { Count: > 0 })
            node.Children.Add(CreateGroup(builder, field.LookupFieldMappings!, LookupLabel));
        if (field.ListItems is { Count: > 0 } || !string.IsNullOrEmpty(field.LangEnumName))
            node.Children.Add(CreateGroup(builder, field.ListItems!, ListItemsLabel));
    }

    private static ObjectTreeNode CreateGroup(ObjectTreeBuilder builder, System.Collections.IEnumerable items, string label)
    {
        var group = new ObjectTreeNode(items, label, isFolder: true);
        foreach (var item in items)
            group.Children.Add(builder.Build(item));
        return group;
    }

    private static FormField? OwningField(ObjectTreeNode node) => node.Parent?.Value as FormField;

    public static bool IsRelationGroup(ObjectTreeNode node) =>
        node.IsFolder && OwningField(node) is { } f && ReferenceEquals(node.Value, f.RelationFieldMappings);

    public static bool IsLookupGroup(ObjectTreeNode node) =>
        node.IsFolder && OwningField(node) is { } f && ReferenceEquals(node.Value, f.LookupFieldMappings);

    public static bool IsListItemsGroup(ObjectTreeNode node) =>
        node.IsFolder && OwningField(node) is { } f && ReferenceEquals(node.Value, f.ListItems);

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
        _ when IsLookupGroup(node) => "IconLookup",
        _ when IsListItemsGroup(node) => "IconList",
        { Value: FormRuleCollection } => "IconList",
        { Value: FieldMapping } => "IconArrowRight",
        { Value: ListItem or FormRule } => "IconDot",
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

    [RelayCommand(CanExecute = nameof(CanAddMapping))]
    private void AddRelationMapping() => AddMapping(isRelation: true);

    [RelayCommand(CanExecute = nameof(CanAddMapping))]
    private void AddLookupMapping() => AddMapping(isRelation: false);

    private void AddMapping(bool isRelation)
    {
        var fieldNode = FindAncestor<FormField>(SelectedTreeNode);
        if (fieldNode?.Value is not FormField field) return;

        var mapping = new FieldMapping(string.Empty, string.Empty);
        var mappings = isRelation ? field.RelationFieldMappings! : field.LookupFieldMappings!;
        mappings.Add(mapping);

        var group = EnsureGroup(fieldNode, mappings, isRelation ? RelationLabel : LookupLabel);
        var selected = SelectedTreeNode;
        var node = AddNode(group, mapping);
        fieldNode.IsExpanded = true;

        // Sitting on the group, the mapping editor in the right pane lists the new mapping, so
        // keep the group selected and refresh the editor; otherwise the new mapping is selected.
        if (ReferenceEquals(selected, group))
        {
            SelectedTreeNode = group;
            OnPropertyChanged(nameof(SelectedEditorContext));
        }
        StatusText = L("Status_AddedNamed", isRelation ? "Relation FieldMapping" : "Lookup FieldMapping", "");
    }

    private bool CanAddMapping() => FindAncestor<FormField>(SelectedTreeNode) is not null;

    [RelayCommand(CanExecute = nameof(CanAddListItem))]
    private void AddListItem()
    {
        var fieldNode = FindAncestor<FormField>(SelectedTreeNode);
        if (fieldNode?.Value is not FormField field) return;

        var key = UniqueKey(field.ListItems!.Select(i => i.Value), "value");
        var item = new ListItem(key, "New option");
        field.ListItems!.Add(item);

        var group = EnsureGroup(fieldNode, field.ListItems!, ListItemsLabel);
        AddNode(group, item);
        fieldNode.IsExpanded = true;
        StatusText = L("Status_AddedNamed", "ListItem", key);
    }

    private bool CanAddListItem() => FindAncestor<FormField>(SelectedTreeNode) is not null;

    private ObjectTreeNode EnsureGroup(ObjectTreeNode fieldNode, System.Collections.IEnumerable items, string label)
    {
        var existing = fieldNode.Children.FirstOrDefault(c => c.IsFolder && ReferenceEquals(c.Value, items));
        if (existing is not null) return existing;
        var group = CreateGroup(Builder, items, label);
        fieldNode.Children.Add(group);
        return group;
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

    private bool CanGenerateFormLayout() =>
        SelectedTreeNode?.Value is FormSchema && !string.IsNullOrWhiteSpace(Schema.ProgId);

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
        ListItem i when node.Parent?.Value is ListItemCollection items => () => items.Remove(i),
        _ => null,
    };
}
