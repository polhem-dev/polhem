namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// Options for <see cref="FormImporter.Plan"/>.
/// </summary>
public sealed class FormImportOptions
{
    /// <summary>Gets the definition folder the files are written to (the folder that holds <c>FormSchema/</c>).</summary>
    public required string DefinePath { get; init; }

    /// <summary>
    /// Gets a value indicating whether an existing FormLayout is replaced. By default it is kept, because it is
    /// usually adjusted by hand after it was first generated.
    /// </summary>
    public bool RegenerateLayout { get; init; }

    /// <summary>
    /// Gets what happens to a TableSchema file that already exists. The default, <see cref="TableSchemaImportMode.Merge"/>,
    /// only adds what the form brings.
    /// </summary>
    public TableSchemaImportMode TableSchemaMode { get; init; } = TableSchemaImportMode.Merge;
}
