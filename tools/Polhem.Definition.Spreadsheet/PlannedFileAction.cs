namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// What an import does to one definition file.
/// </summary>
public enum PlannedFileAction
{
    /// <summary>The file does not exist and is created.</summary>
    Create,

    /// <summary>The file is replaced with content generated from the workbook.</summary>
    Overwrite,

    /// <summary>Entries are added to or changed in the file; the rest of it, comments included, stays.</summary>
    Update,

    /// <summary>The file already holds what the workbook describes.</summary>
    Unchanged,

    /// <summary>The file exists and is deliberately left as it is.</summary>
    Skip,
}
