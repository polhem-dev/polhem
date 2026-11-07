namespace Polhem.DefineEditor.Services;

/// <summary>An open element on the way from the root to the caret, and where it stands among its parent's children.</summary>
/// <param name="Name">The element's name.</param>
/// <param name="Index">Its 0-based position among all its parent's child elements.</param>
/// <param name="NameIndex">Its 0-based position among its parent's child elements of the same name.</param>
public sealed record XmlElementStep(string Name, int Index, int NameIndex);
