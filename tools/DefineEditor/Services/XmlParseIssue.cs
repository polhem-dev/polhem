namespace Polhem.DefineEditor.Services;

/// <summary>A problem in the XML typed in a document's XML view, at a 1-based line and column.</summary>
/// <param name="Kind">What is wrong.</param>
/// <param name="Line">The 1-based line.</param>
/// <param name="Column">The 1-based column.</param>
/// <param name="Detail">The unknown name, or the reader's own message for a <see cref="XmlParseIssueKind.Syntax"/> problem.</param>
public sealed record XmlParseIssue(XmlParseIssueKind Kind, int Line, int Column, string Detail);
