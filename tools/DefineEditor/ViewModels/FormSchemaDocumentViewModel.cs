using System.ComponentModel;
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
using Polhem.UI.Avalonia.Controls;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// FormSchema editor. Loads the schema via <see cref="XmlCodec"/>, shows it as an
/// <see cref="ObjectTreeBuilder"/> tree (Tables and Rules folders, fields directly under
/// each table) with the selected object in the property grid, and offers Add commands for
/// tables, fields and mappings. Each field also gets Relation / Lookup group nodes, which the
/// annotations cannot express; see <see cref="AddFieldGroups"/>. A field's list items are
/// edited in the grid's collection dialog.
/// </summary>
public sealed partial class FormSchemaDocumentViewModel : ObjectTreeDocumentViewModelBase
{
    private const string RelationLabel = "Relation";
    private const string LookupLabel = "Lookup";

    // NOTE: The category FormField declares on RelationProgId, LookupProgId, their mappings and DisplayFields.
    private const string RelationCategory = "Relation";

    public override string TabIcon => "DefFormSchema";

    public FormSchema Schema { get; }

    public SolutionContext Solution { get; }

    /// <summary>Backing object handed to <see cref="XmlCodec.SerializeToFile"/> by base's Save.</summary>
    protected override object RootObject => Schema;

    public override ITreeNodeCommandProvider CommandProvider { get; }

    /// <summary>
    /// The object the property grid shows: a Relation or Lookup group shows the field that owns it,
    /// narrowed to its Relation category by <see cref="SelectedPropertyFilter"/>; any other node shows
    /// its own object.
    /// </summary>
    public override object? SelectedEditorContext => SelectedTreeNode switch
    {
        null => null,
        var node when IsMappingGroup(node) => OwningField(node),
        _ => base.SelectedEditorContext,
    };

    /// <inheritdoc/>
    public override Func<PropertyDescriptor, bool>? SelectedPropertyFilter =>
        SelectedTreeNode is { } node && IsMappingGroup(node)
            ? static p => string.Equals(p.Category, RelationCategory, StringComparison.Ordinal)
            : null;

    /// <inheritdoc/>
    public override Func<PropertyDescriptor, object, IReadOnlyList<string>?>? ValueSuggestionProvider => Suggest;

    /// <inheritdoc/>
    public override Func<CollectionEditContext, Task<bool>?>? CollectionEditorProvider => EditMappingsInTree;

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
    /// Adds the Relation and Lookup groups under a field node, each when the field uses it.
    /// <see cref="FieldMappingCollection"/> carries no <c>[TreeNode]</c>, and a field holds two
    /// mapping collections of the same type, which a class-level annotation could not label apart.
    /// </summary>
    private static void AddFieldGroups(ObjectTreeNode node, ObjectTreeBuilder builder)
    {
        if (node.Value is not FormField field) return;
        if (!string.IsNullOrEmpty(field.RelationProgId) || field.RelationFieldMappings is { Count: > 0 })
            node.Children.Add(CreateGroup(builder, field.RelationFieldMappings!, RelationLabel));
        if (!string.IsNullOrEmpty(field.LookupProgId) || field.LookupFieldMappings is { Count: > 0 })
            node.Children.Add(CreateGroup(builder, field.LookupFieldMappings!, LookupLabel));
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

    public static bool IsLookupGroup(ObjectTreeNode node) =>
        node.IsFolder && OwningField(node) is { } f && ReferenceEquals(node.Value, f.LookupFieldMappings);

    private static bool IsMappingGroup(ObjectTreeNode node) => IsRelationGroup(node) || IsLookupGroup(node);

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

        var group = EnsureGroup(fieldNode, mappings, isRelation);
        AddNode(group, mapping);
        fieldNode.IsExpanded = true;
        StatusText = L("Status_AddedNamed", isRelation ? "Relation FieldMapping" : "Lookup FieldMapping", "");
    }

    private bool CanAddMapping() => FindAncestor<FormField>(SelectedTreeNode) is not null;

    /// <summary>
    /// Returns the Relation or Lookup group of <paramref name="fieldNode"/>, adding it when the field has none yet. The
    /// Relation group always comes first.
    /// </summary>
    private ObjectTreeNode EnsureGroup(ObjectTreeNode fieldNode, FieldMappingCollection mappings, bool isRelation)
    {
        var existing = fieldNode.Children.FirstOrDefault(c => c.IsFolder && ReferenceEquals(c.Value, mappings));
        if (existing is not null) return existing;
        var group = CreateGroup(Builder, mappings, isRelation ? RelationLabel : LookupLabel);
        if (isRelation)
            fieldNode.Children.Insert(0, group);
        else
            fieldNode.Children.Add(group);
        return group;
    }

    /// <summary>
    /// After the grid set a field's RelationProgId or LookupProgId, adds the group the field now calls for, so its
    /// mappings can be added from the tree.
    /// </summary>
    protected override void OnSelectedObjectEdited(ObjectTreeNode node)
    {
        var fieldNode = FindAncestor<FormField>(node);
        if (fieldNode?.Value is not FormField field) return;
        if (!string.IsNullOrEmpty(field.RelationProgId))
            EnsureGroup(fieldNode, field.RelationFieldMappings!, isRelation: true);
        if (!string.IsNullOrEmpty(field.LookupProgId))
            EnsureGroup(fieldNode, field.LookupFieldMappings!, isRelation: false);
    }

    /// <summary>
    /// The values the grid offers: the solution's form ids for a field's RelationProgId and LookupProgId; for a
    /// mapping, the fields of its own table as the destination and the fields of the related form's master table as
    /// the source.
    /// </summary>
    public IReadOnlyList<string>? Suggest(PropertyDescriptor property, object component)
    {
        switch (component)
        {
            case FormField when property.Name is nameof(FormField.RelationProgId) or nameof(FormField.LookupProgId):
                return Solution.AvailableProgIds.Count > 0 ? Solution.AvailableProgIds : null;
            case FieldMapping:
                var groupNode = SelectedTreeNode?.Parent;
                if (groupNode is null || OwningField(groupNode) is not { } field) return null;
                if (property.Name == nameof(FieldMapping.DestinationField))
                {
                    return field.Table?.Fields?.Select(f => f.FieldName).Where(n => !string.IsNullOrEmpty(n)).ToArray();
                }
                if (property.Name == nameof(FieldMapping.SourceField))
                {
                    var progId = IsRelationGroup(groupNode) ? field.RelationProgId : field.LookupProgId;
                    return Solution.MasterFieldNames(progId) is { Count: > 0 } names ? names : null;
                }
                return null;
            default:
                return null;
        }
    }

    /// <summary>
    /// Keeps a field's Relation and Lookup mappings in the tree: opening either collection from the grid selects its
    /// group node instead, adding the group when the field has none yet. Every other collection opens in the grid's
    /// dialog.
    /// </summary>
    public Task<bool>? EditMappingsInTree(CollectionEditContext context)
    {
        if (context.Component is not FormField field
            || context.Property.Name is not (nameof(FormField.RelationFieldMappings) or nameof(FormField.LookupFieldMappings)))
        {
            return null;
        }
        var fieldNode = FindAncestor<FormField>(SelectedTreeNode);
        if (fieldNode is null || !ReferenceEquals(fieldNode.Value, field)) return Task.FromResult(false);
        var isRelation = context.Property.Name == nameof(FormField.RelationFieldMappings);
        var group = EnsureGroup(fieldNode, isRelation ? field.RelationFieldMappings! : field.LookupFieldMappings!, isRelation);
        fieldNode.IsExpanded = true;
        SelectedTreeNode = group;
        // Nothing in the collection changed; the grid must not report an edit.
        return Task.FromResult(false);
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
        _ => null,
    };
}
