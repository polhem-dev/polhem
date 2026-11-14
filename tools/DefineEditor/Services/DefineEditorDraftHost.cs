using Polhem.Core.Serialization;
using Polhem.Definition.Drafting;
using Polhem.Definition.Language;
using Polhem.Definition.Spreadsheet;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// What the open solution offers a form drafted from documents: its forms, to relate fields to, and the checks of the
/// workbook import, so the model fixes what the import would refuse before the user gets there.
/// </summary>
public sealed class DefineEditorDraftHost : IFormDraftingHost
{
    private const string ZhTw = "zh-TW";

    private readonly SolutionContext _solution;
    private readonly string _definePath;
    private IReadOnlyList<DraftCatalogEntry>? _catalog;

    /// <summary>Initializes a new instance of the <see cref="DefineEditorDraftHost"/> class.</summary>
    /// <param name="solution">The open solution's forms.</param>
    /// <param name="definePath">The definition folder the form will be imported into.</param>
    public DefineEditorDraftHost(SolutionContext solution, string definePath)
    {
        ArgumentNullException.ThrowIfNull(solution);
        ArgumentException.ThrowIfNullOrWhiteSpace(definePath);
        _solution = solution;
        _definePath = definePath;
    }

    /// <summary>
    /// Gets the forms of the solution that have a FormSchema, in the order ProgramSettings declares them. The catalog
    /// is read once: the session asks for it on every search, and the solution does not change during a conversation.
    /// </summary>
    public IReadOnlyList<DraftCatalogEntry> GetCatalog() => _catalog ??= BuildCatalog();

    /// <inheritdoc/>
    public IReadOnlyList<DraftCatalogField> GetFields(string progId)
        => GetCatalog().FirstOrDefault(entry => string.Equals(entry.ProgId, progId?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?.Fields ?? [];

    /// <summary>
    /// Plans the import of the draft without writing anything and returns what the plan found.
    /// </summary>
    public IReadOnlyList<DraftIssue> Validate(FormDraftResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        try
        {
            var plan = FormImporter.Plan(FormDraftImport.ToContent(result), new FormImportOptions { DefinePath = _definePath });
            return FormDraftImport.ToDraftIssues(plan.Issues);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
        {
            // An existing file of the solution cannot be read. The model cannot fix that, but it must not mark the draft
            // ready either, so the problem is reported like any other.
            return [new DraftIssue(DraftIssueSeverity.Error, "Solution", ex.Message)];
        }
    }

    private List<DraftCatalogEntry> BuildCatalog()
    {
        var choices = RelationProgramChoice.Build(_solution.ReadProgramSettings(), _solution.AvailableProgIds, currentProgId: null);
        var zhTwNames = ReadZhTwNames();
        var catalog = new List<DraftCatalogEntry>();
        foreach (var choice in choices.Where(c => c.IsSelectable))
        {
            var schema = _solution.LoadFormSchema(choice.ProgId);
            if (schema is null) continue;
            var fields = schema.MasterTable?.Fields?
                .Where(f => !string.IsNullOrEmpty(f.FieldName))
                .Select(f => new DraftCatalogField(f.FieldName, f.Caption, f.DbType))
                .ToList() ?? [];
            var displayName = string.IsNullOrEmpty(choice.DisplayName) ? schema.DisplayName : choice.DisplayName;
            catalog.Add(new DraftCatalogEntry(choice.ProgId, displayName,
                zhTwNames.GetValueOrDefault(choice.ProgId, string.Empty), fields));
        }
        return catalog;
    }

    /// <summary>The zh-TW display name of each form, read from the solution's Language files.</summary>
    private Dictionary<string, string> ReadZhTwNames()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in _solution.LanguagePaths)
        {
            try
            {
                if (XmlCodec.DeserializeFromFile<LanguageResource>(path) is not { } resource
                    || !string.Equals(resource.Lang, ZhTw, StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrEmpty(resource.Namespace))
                {
                    continue;
                }
                if (resource.GetText(FormSchemaLocalizer.SchemaDisplayNameKey) is { Length: > 0 } name)
                    names.TryAdd(resource.Namespace, name);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                // A Language file that cannot be read only costs the model a name to search by.
            }
        }
        return names;
    }
}
