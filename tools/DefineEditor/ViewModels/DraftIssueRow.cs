using Polhem.Definition.Drafting;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>One problem of the draft, as the Questions tab lists it.</summary>
/// <param name="IsError">Whether it keeps the draft from being imported.</param>
/// <param name="Location">Where it is, such as <c>LeaveRequest.leave_type</c>.</param>
/// <param name="Message">What is wrong, in English.</param>
public sealed record DraftIssueRow(bool IsError, string Location, string Message)
{
    /// <summary>Creates a row for <paramref name="issue"/>.</summary>
    public static DraftIssueRow From(DraftIssue issue)
        => new(issue.Severity == DraftIssueSeverity.Error, issue.Location, issue.Message);
}
