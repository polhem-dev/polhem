using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Drafting;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// One field of the draft in the Fields tab. The user edits the row; <see cref="FormDraftingViewModel"/> hands the
/// edit to the session, which owns the draft, once the row is committed.
/// </summary>
public sealed partial class DraftFieldRow : ObservableObject
{
    private readonly DraftField _original;

    /// <summary>Initializes a new instance of the <see cref="DraftFieldRow"/> class.</summary>
    /// <param name="tableIndex">The index of the field's table in the draft; 0 is the master table.</param>
    /// <param name="fieldIndex">The index of the field in its table.</param>
    /// <param name="tableLabel">The table's name as the user sees it.</param>
    /// <param name="field">The field. Its values are copied; the row never changes it.</param>
    public DraftFieldRow(int tableIndex, int fieldIndex, string tableLabel, DraftField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        TableIndex = tableIndex;
        FieldIndex = fieldIndex;
        TableLabel = tableLabel;
        _original = new DraftField
        {
            FieldName = field.FieldName,
            Caption = field.Caption,
            CaptionZhTw = field.CaptionZhTw,
            DbType = field.DbType,
            MaxLength = field.MaxLength,
            Required = field.Required,
        };
        _fieldName = field.FieldName;
        _caption = field.Caption;
        _captionZhTw = field.CaptionZhTw;
        _dbType = field.DbType;
        _maxLength = field.MaxLength;
        _required = field.Required;
        RelationText = field.Relation is { ProgId.Length: > 0 } relation
            ? string.Format(CultureInfo.InvariantCulture, "{0} ({1:P0})", relation.ProgId, relation.Confidence)
            : string.Empty;
        RelationReason = field.Relation?.Reason ?? string.Empty;
        SourceQuote = field.SourceQuote;
    }

    /// <summary>The types a field can be given in the grid.</summary>
    public static IReadOnlyList<FieldDbType> DbTypes { get; } =
        [.. Enum.GetValues<FieldDbType>().Where(type => type != FieldDbType.Unknown)];

    /// <summary>Gets the index of the field's table in the draft; 0 is the master table.</summary>
    public int TableIndex { get; }

    /// <summary>Gets the index of the field in its table.</summary>
    public int FieldIndex { get; }

    /// <summary>Gets the table's name as the user sees it.</summary>
    public string TableLabel { get; }

    [ObservableProperty]
    private string _fieldName;

    [ObservableProperty]
    private string _caption;

    [ObservableProperty]
    private string _captionZhTw;

    [ObservableProperty]
    private FieldDbType? _dbType;

    [ObservableProperty]
    private int? _maxLength;

    [ObservableProperty]
    private bool _required;

    /// <summary>Gets the form the field picks a record from and how sure the model is, such as <c>Employee (92%)</c>.</summary>
    public string RelationText { get; }

    /// <summary>Gets why the model chose the related form.</summary>
    public string RelationReason { get; }

    /// <summary>Gets the text of the document the field was read from.</summary>
    public string SourceQuote { get; }

    /// <summary>
    /// Describes, in English for the model, what the user changed on this row, such as
    /// <c>Changed the caption of leave_type from "Type" to "Leave type".</c>; empty when nothing changed.
    /// </summary>
    public string DescribeChanges()
    {
        var name = _original.FieldName;
        var changes = new List<string>();
        void Compare<T>(string what, T before, T after)
        {
            if (!EqualityComparer<T>.Default.Equals(before, after))
                changes.Add($"Changed the {what} of the field {name} from {Show(before)} to {Show(after)}.");
        }
        Compare("field name", _original.FieldName, FieldName.Trim());
        Compare("caption", _original.Caption, Caption.Trim());
        Compare("zh-TW caption", _original.CaptionZhTw, CaptionZhTw.Trim());
        Compare("type", _original.DbType, DbType);
        Compare("maximum length", _original.MaxLength, MaxLength);
        Compare("required setting", _original.Required, Required);
        return string.Join(" ", changes);
    }

    /// <summary>Writes the row's values to <paramref name="field"/>.</summary>
    /// <param name="field">The field of the session's draft that this row shows.</param>
    public void ApplyTo(DraftField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        field.FieldName = FieldName.Trim();
        field.Caption = Caption.Trim();
        field.CaptionZhTw = CaptionZhTw.Trim();
        field.DbType = DbType;
        field.MaxLength = MaxLength;
        field.Required = Required;
    }

    private static string Show<T>(T value) => value switch
    {
        null => "(not decided)",
        string text => $"\"{text}\"",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };
}
