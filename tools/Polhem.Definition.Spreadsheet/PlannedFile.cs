namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// One definition file an import touches, and what it does to it.
/// </summary>
/// <param name="Path">The absolute path of the file.</param>
/// <param name="Action">What the import does to the file.</param>
/// <param name="Description">What the file is, and why it gets this action.</param>
/// <param name="Content">The text to write; <see langword="null"/> for <see cref="PlannedFileAction.Unchanged"/> and <see cref="PlannedFileAction.Skip"/>.</param>
public sealed record PlannedFile(string Path, PlannedFileAction Action, string Description, string? Content)
{
    /// <summary>Gets a value indicating whether applying the plan writes this file.</summary>
    public bool Writes => Content is not null;
}
