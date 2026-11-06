using CommunityToolkit.Mvvm.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Database;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// One source field the relation dialog lists: whether it is mapped, where it goes, and how the destination is created
/// when the table lacks it.
/// </summary>
public sealed partial class RelationSetupRow : ObservableObject
{
    /// <summary>
    /// Initializes a row for <paramref name="sourceField"/>.
    /// </summary>
    /// <param name="sourceField">The field of the related form's master table.</param>
    /// <param name="joinDestination">The destination a regular mapping gets by default.</param>
    /// <param name="lookupDestination">The destination a lookup-only mapping gets by default.</param>
    public RelationSetupRow(string sourceField, string joinDestination, string lookupDestination)
    {
        SourceField = sourceField;
        JoinDestination = joinDestination;
        LookupDestination = lookupDestination;
        _destinationField = joinDestination;
    }

    /// <summary>Gets the source field name.</summary>
    public string SourceField { get; }

    /// <summary>Gets the source field's caption; empty when the related form no longer has the field.</summary>
    public string SourceCaption { get; init; } = string.Empty;

    /// <summary>Gets the source field's data type, or <c>null</c> when the related form no longer has the field.</summary>
    public FieldDbType? SourceDbType { get; init; }

    /// <summary>
    /// Gets whether the source is a relation field of the related form, which its own relation fills rather than a
    /// column of its table.
    /// </summary>
    public bool IsSourceRelationField { get; init; }

    /// <summary>Gets the source field's maximum length.</summary>
    public int SourceMaxLength { get; init; }

    /// <summary>Gets whether the row stands for an existing mapping whose source field the related form no longer has.</summary>
    public bool IsSourceMissing { get; init; }

    /// <summary>Gets the source data type as the dialog shows it, such as <c>String(20)</c>.</summary>
    public string SourceTypeText => SourceDbType switch
    {
        null => string.Empty,
        FieldDbType.String when SourceMaxLength > 0 => $"String({SourceMaxLength})",
        { } type => type.ToString(),
    };

    /// <summary>Gets the destination a regular mapping gets by default.</summary>
    public string JoinDestination { get; }

    /// <summary>Gets the destination a lookup-only mapping gets by default.</summary>
    public string LookupDestination { get; }

    /// <summary>Gets or sets whether applying maps this source field.</summary>
    [ObservableProperty]
    private bool _isIncluded;

    /// <summary>Gets or sets the field of this table the source value is written to.</summary>
    [ObservableProperty]
    private string _destinationField;

    /// <summary>Gets or sets whether the mapping is written back on a lookup pick only, leaving the query join out.</summary>
    [ObservableProperty]
    private bool _lookupOnly;

    /// <summary>Gets or sets the type a destination field that does not exist yet is created with.</summary>
    [ObservableProperty]
    private FieldType _newFieldType = FieldType.RelationField;

    /// <summary>Gets or sets what applying the row would do, as <see cref="RelationSetup.Evaluate"/> last decided.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsError))]
    [NotifyPropertyChangedFor(nameof(CreatesField))]
    private RelationSetupRowStatus _status;

    /// <summary>Gets whether applying adds the destination field, so <see cref="NewFieldType"/> matters.</summary>
    public bool CreatesField => Status == RelationSetupRowStatus.New;

    /// <summary>Gets whether <see cref="Status"/> refuses applying.</summary>
    public bool IsError => RelationSetup.IsError(Status);

    // A regular mapping fills a relation field. A lookup-only one copies into a field of the same name, which is stored
    // when the source is a column, and is a relation field when the source is one: the form fills it through its own
    // relation, as an order fills its department name from its own department key (docs/en/definitions/
    // formschema-data-access.md §3.4). The destination follows the switch until the user types one of their own.
    partial void OnLookupOnlyChanged(bool value)
    {
        NewFieldType = value && !IsSourceRelationField ? FieldType.DbField : FieldType.RelationField;
        var previous = value ? JoinDestination : LookupDestination;
        if (string.Equals(DestinationField, previous, StringComparison.OrdinalIgnoreCase))
            DestinationField = value ? LookupDestination : JoinDestination;
    }
}
