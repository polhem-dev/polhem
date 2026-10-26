using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// The relation dialog of a foreign key field: the user picks the related form from the solution's ProgramSettings,
/// ticks the master fields to carry back and names where each goes, and applies the result to the field.
/// </summary>
/// <remarks>
/// Nothing is written while the dialog is open; <see cref="RelationSetup.Apply"/> runs only after the user confirms.
/// </remarks>
public sealed partial class RelationDialogViewModel : ViewModelBase
{
    private readonly FormSchema _schema;
    private readonly SolutionContext _solution;
    private FormSchema? _sourceSchema;
    private bool _isRebuilding;
    private bool _isUnknownText;
    private bool _isLoaded;

    public RelationDialogViewModel(FormSchema schema, FormField field, SolutionContext solution)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(solution);
        _schema = schema;
        Field = field;
        _solution = solution;
        var settings = solution.ReadProgramSettings();
        IsProgramSettingsMissing = settings is null;
        Choices = RelationProgramChoice.Build(settings, solution.FormSchemaPaths.Keys.ToList(), field.RelationProgId);
        _filteredChoices = Choices;
        _searchText = field.RelationProgId;
        LoadForm(field.RelationProgId);
    }

    /// <summary>The types a destination field the table lacks can be created with.</summary>
    public static IReadOnlyList<FieldType> NewFieldTypes { get; } = [FieldType.RelationField, FieldType.DbField];

    /// <summary>The foreign key field the dialog edits.</summary>
    public FormField Field { get; }

    public string Title => L("Relation_Title", $"{Field.Table?.TableName}.{Field.FieldName}");

    /// <summary>Every form the dialog can relate the field to, in ProgramSettings order.</summary>
    public IReadOnlyList<RelationProgramChoice> Choices { get; }

    /// <summary>Whether the solution has no readable ProgramSettings, so the forms are listed by their FormSchema files.</summary>
    public bool IsProgramSettingsMissing { get; }

    /// <summary>The text typed into the form picker; the list below it shows <see cref="FilteredChoices"/>.</summary>
    [ObservableProperty]
    private string _searchText;

    /// <summary>The forms that match <see cref="SearchText"/>, those whose ProgId starts with it first.</summary>
    [ObservableProperty]
    private IReadOnlyList<RelationProgramChoice> _filteredChoices;

    /// <summary>The form the picker selected.</summary>
    [ObservableProperty]
    private RelationProgramChoice? _selectedChoice;

    /// <summary>The related form the rows are listed for; empty when none is picked.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedName))]
    [NotifyPropertyChangedFor(nameof(IsRemoving))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private string _progId = string.Empty;

    /// <summary>The source fields of the related form's master table, with what applying would do to each.</summary>
    public ObservableCollection<RelationSetupRow> Rows { get; } = [];

    /// <summary>The relation fields applying would leave without a mapping.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOrphans))]
    [NotifyPropertyChangedFor(nameof(OrphansText))]
    private IReadOnlyList<string> _orphans = [];

    /// <summary>Whether applying also removes <see cref="Orphans"/>.</summary>
    [ObservableProperty]
    private bool _removeOrphans;

    /// <summary>What the user should know before applying: problems the result would have, and changes beyond the mappings.</summary>
    [ObservableProperty]
    private IReadOnlyList<string> _messages = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private bool _hasErrors;

    public bool HasOrphans => Orphans.Count > 0;

    public string OrphansText => L("Relation_RemoveOrphans", string.Join(", ", Orphans));

    /// <summary>The selected form's display name, shown beside the picker.</summary>
    public string SelectedName => Choices.FirstOrDefault(c => IsProgId(c.ProgId, ProgId))?.DisplayName ?? string.Empty;

    /// <summary>Whether applying would remove the relation, because the picker was cleared.</summary>
    public bool IsRemoving => string.IsNullOrWhiteSpace(ProgId) && !string.IsNullOrEmpty(Field.RelationProgId);

    /// <summary>Raised when the user applies the dialog.</summary>
    public event EventHandler? Applied;

    partial void OnSearchTextChanged(string value)
    {
        FilteredChoices = RelationProgramChoice.Filter(Choices, value);
        // Typed text names a form only when it is one of the listed ProgIds, so a typo never becomes a relation.
        var typed = value?.Trim() ?? string.Empty;
        var match = Choices.FirstOrDefault(c => IsProgId(c.ProgId, typed));
        if (match is not null || typed.Length == 0)
            LoadForm(match?.ProgId ?? string.Empty);
        else
            LoadForm(null);
    }

    partial void OnSelectedChoiceChanged(RelationProgramChoice? value)
    {
        if (value is not null) LoadForm(value.ProgId);
    }

    /// <summary>
    /// Lists the rows for <paramref name="progId"/>: an empty id lists none (applying removes the relation), and
    /// <c>null</c> stands for typed text that names no listed form, which cannot be applied.
    /// </summary>
    private void LoadForm(string? progId)
    {
        // The picker raises both a selection and a text change for one pick; the second must not rebuild the rows.
        if (_isLoaded && progId is not null && !_isUnknownText && IsProgId(progId, ProgId)) return;
        _isLoaded = true;
        foreach (var row in Rows) row.PropertyChanged -= OnRowChanged;
        Rows.Clear();
        ProgId = progId ?? string.Empty;
        _sourceSchema = string.IsNullOrEmpty(progId) ? null : _solution.LoadFormSchema(progId);
        _isUnknownText = progId is null;
        if (_sourceSchema?.MasterTable is { } master)
        {
            _isRebuilding = true;
            try
            {
                foreach (var row in RelationSetup.CreateRows(Field, master, IsProgId(progId, Field.RelationProgId)))
                {
                    row.PropertyChanged += OnRowChanged;
                    Rows.Add(row);
                }
            }
            finally
            {
                _isRebuilding = false;
            }
        }
        Recompute();
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Status is what Recompute writes; reacting to it would loop.
        if (_isRebuilding || e.PropertyName == nameof(RelationSetupRow.Status)) return;
        Recompute();
    }

    private void Recompute()
    {
        _isRebuilding = true;
        try
        {
            var rows = Rows.ToList();
            var choice = Choices.FirstOrDefault(c => IsProgId(c.ProgId, ProgId));
            var messages = new List<string>();
            var blocked = _isUnknownText;
            if (IsProgramSettingsMissing) messages.Add(L("Relation_ProgramSettingsMissing"));
            if (_isUnknownText) messages.Add(L("Relation_UnknownProgId", SearchText.Trim()));
            if (choice is { IsRegistered: false } && !IsProgramSettingsMissing)
                messages.Add(L("Relation_Unregistered", choice.ProgId));
            if (ProgId.Length > 0 && _sourceSchema?.MasterTable is null)
            {
                messages.Add(L("Relation_NoFormSchema", ProgId));
                blocked = true;
            }

            var rowErrors = RelationSetup.Evaluate(Field, rows);
            Orphans = _isUnknownText ? [] : RelationSetup.OrphanedFields(Field, ProgId.Length == 0 ? [] : rows);
            if (ProgId.Length > 0 && !rowErrors && !blocked)
            {
                if (Field.DbType != Polhem.Core.Data.FieldDbType.Guid)
                    messages.Add(L("Relation_ForeignKeyRetyped", Field.FieldName));
                foreach (var source in RelationSetup.SourcesMissingFromLookup(_sourceSchema, rows))
                    messages.Add(L("Relation_NotInLookupFields", source, ProgId));
                messages.AddRange(RelationSetup.NewIssues(_schema, Field, ProgId, rows, _solution.AvailableProgIds));
            }
            if (IsRemoving && !_isUnknownText) messages.Add(L("Relation_Removing", Field.FieldName));
            Messages = messages;
            HasErrors = rowErrors || blocked;
        }
        finally
        {
            _isRebuilding = false;
        }
    }

    private bool CanApply() => !HasErrors && (ProgId.Length > 0 || IsRemoving);

    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply() => Applied?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Writes the dialog's result to <see cref="Field"/> and its table.
    /// </summary>
    public RelationSetupChanges ApplyTo() => RelationSetup.Apply(Field, ProgId, Rows.ToList(), RemoveOrphans);

    private static bool IsProgId(string? a, string? b) => string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.OrdinalIgnoreCase);
}
