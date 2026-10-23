namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// Options for <see cref="FormImporter.Plan"/>.
/// </summary>
public sealed class FormImportOptions
{
    /// <summary>Gets the definition folder the files are written to (the folder that holds <c>FormSchema/</c>).</summary>
    public required string DefinePath { get; init; }

    /// <summary>
    /// Gets what happens to a FormLayout file that already exists. The default, <see cref="FormLayoutImportMode.Keep"/>,
    /// leaves it, because it is usually adjusted by hand after it was first generated.
    /// </summary>
    public FormLayoutImportMode LayoutMode { get; init; } = FormLayoutImportMode.Keep;

    /// <summary>
    /// Gets what happens to a TableSchema file that already exists. The default, <see cref="TableSchemaImportMode.Merge"/>,
    /// only adds what the form brings.
    /// </summary>
    public TableSchemaImportMode TableSchemaMode { get; init; } = TableSchemaImportMode.Merge;
}
