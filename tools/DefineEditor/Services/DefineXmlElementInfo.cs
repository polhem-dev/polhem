namespace Polhem.DefineEditor.Services;

/// <summary>An element a definition element may contain.</summary>
/// <param name="Name">The element's name.</param>
/// <param name="IsRepeated">Whether it may occur more than once, as the items of a collection do.</param>
public sealed record DefineXmlElementInfo(string Name, bool IsRepeated);
