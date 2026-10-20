namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// The presentation of a <see cref="SheetColumn{TRow}"/>, independent of its row type. The template
/// generator and the coverage gate read columns through this view.
/// </summary>
internal interface ISheetColumn
{
    /// <summary>Gets the key written in the hidden key row; for a property column it is the property name.</summary>
    string Key { get; }

    /// <summary>Gets the header text shown to the person filling in the workbook.</summary>
    SheetText Label { get; }

    /// <summary>Gets the explanation shown on the instructions sheet.</summary>
    SheetText Hint { get; }

    /// <summary>Gets the name of the list on the Lists sheet that feeds the column's drop-down, if any.</summary>
    string? ListName { get; }

    /// <summary>Gets a value indicating whether the column is grouped and collapsed by default.</summary>
    bool Advanced { get; }

    /// <summary>Gets the column width in Excel character units.</summary>
    double Width { get; }

    /// <summary>
    /// Gets a value indicating whether <see cref="Key"/> names a serialized property of the definition type.
    /// Columns such as a zh-TW caption carry translations instead, and the coverage gate skips them.
    /// </summary>
    bool MapsProperty { get; }
}
