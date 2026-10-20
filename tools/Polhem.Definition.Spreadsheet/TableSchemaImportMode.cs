namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// What an import does with a TableSchema file that already exists.
/// </summary>
/// <remarks>
/// A TableSchema often carries more than its FormSchema can derive: column lengths, columns that are not on the form
/// (the organization columns of <c>st_employee</c>, for example) and hand-made indexes. Replacing it from the form
/// would drop them.
/// </remarks>
public enum TableSchemaImportMode
{
    /// <summary>Adds the columns and indexes the form has and the file lacks; changes and removes nothing.</summary>
    Merge,

    /// <summary>Leaves the file as it is.</summary>
    Keep,

    /// <summary>Replaces the file with the TableSchema generated from the form.</summary>
    Overwrite,
}
