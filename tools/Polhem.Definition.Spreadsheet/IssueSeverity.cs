namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// How serious a <see cref="DefinitionIssue"/> is.
/// </summary>
public enum IssueSeverity
{
    /// <summary>The definition can still be written, but something looks wrong.</summary>
    Warning,

    /// <summary>The definition cannot be written until this is fixed.</summary>
    Error,
}
