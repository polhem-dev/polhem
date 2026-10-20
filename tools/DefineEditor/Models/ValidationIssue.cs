namespace Polhem.DefineEditor.Models;

public enum ValidationSeverity
{
    Warning,
    Error,
}

/// <summary>
/// A single validation finding shown in a document's issue list.
/// </summary>
public sealed record ValidationIssue(ValidationSeverity Severity, string Path, string Message)
{
    public string Icon => Severity switch
    {
        ValidationSeverity.Error => "❌",
        _ => "⚠️",
    };
}
