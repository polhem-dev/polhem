namespace Polhem.DefineEditor.Services;

/// <summary>A position in an XML text: the 1-based line and column of an element's name, and the name's length.</summary>
/// <param name="Line">The 1-based line.</param>
/// <param name="Column">The 1-based column of the element's name, after its <c>&lt;</c>.</param>
/// <param name="Length">The length of the element's name.</param>
public sealed record XmlTextPosition(int Line, int Column, int Length);
