namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// A single finding about a definition or about the workbook it was read from.
/// </summary>
/// <param name="Severity">How serious the finding is.</param>
/// <param name="Location">
/// Where it is: a cell or row of the workbook (<c>Fields!E12</c>, <c>Fields!12</c>) or a path into the
/// definition (<c>Order.customer_rowid.Relation</c>).
/// </param>
/// <param name="Message">What is wrong.</param>
public sealed record DefinitionIssue(IssueSeverity Severity, string Location, string Message)
{
    /// <inheritdoc/>
    public override string ToString() => $"{Severity.ToString().ToLowerInvariant()}: {Location}: {Message}";
}
