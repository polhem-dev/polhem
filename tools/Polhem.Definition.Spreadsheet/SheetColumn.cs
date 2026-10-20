namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// One column of a tabular sheet (or one row of the Form sheet): its key, how it is shown, and how a value
/// moves between a definition object and a cell.
/// </summary>
/// <typeparam name="TRow">The row object the column reads from and writes to.</typeparam>
internal sealed class SheetColumn<TRow> : ISheetColumn
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public string Hint { get; init; } = string.Empty;

    public string? ListName { get; init; }

    public bool Advanced { get; init; }

    public double Width { get; init; } = 14;

    public bool MapsProperty { get; init; } = true;

    /// <summary>Gets the function that produces the cell value: a <see cref="string"/> or an <see cref="int"/>.</summary>
    public required Func<TRow, object> Export { get; init; }

    /// <summary>
    /// Gets the function that applies a trimmed cell text to the row. It returns an error message, or
    /// <see langword="null"/> when the text was accepted.
    /// </summary>
    public required Func<TRow, string, string?> Import { get; init; }
}
