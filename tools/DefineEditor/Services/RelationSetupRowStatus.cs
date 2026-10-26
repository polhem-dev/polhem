namespace Polhem.DefineEditor.Services;

/// <summary>
/// What applying a row of the relation dialog would do to its destination field, or why it cannot be applied.
/// </summary>
public enum RelationSetupRowStatus
{
    /// <summary>The row is not included, so applying leaves it out.</summary>
    None,

    /// <summary>The destination field exists in the table and has the source's data type.</summary>
    Existing,

    /// <summary>The destination field does not exist yet; applying adds it to the table.</summary>
    New,

    /// <summary>The destination field exists, but its data type differs from the source's. Applying still maps it.</summary>
    TypeMismatch,

    /// <summary>The row has no destination field name. Applying is refused.</summary>
    MissingDestination,

    /// <summary>The destination is the foreign key field itself, which holds the picked row's id. Applying is refused.</summary>
    DestinationIsForeignKey,

    /// <summary>Another included row writes the same destination field. Applying is refused.</summary>
    DuplicateDestination,
}
