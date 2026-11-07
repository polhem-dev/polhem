namespace Polhem.DefineEditor.Services;

/// <summary>
/// Where the caret is in an XML text: in what kind of token, inside which elements, and what has been typed of the
/// token so far.
/// </summary>
/// <param name="Kind">The kind of token the caret is in.</param>
/// <param name="Path">
/// The names of the open elements from the root down. For an attribute name or value it ends with the element the
/// attribute belongs to; otherwise it ends with the element the caret is inside.
/// </param>
/// <param name="Prefix">What has been typed of the token before the caret.</param>
/// <param name="AttributeName">The attribute whose value the caret is in, for <see cref="XmlCaretContextKind.AttributeValue"/>.</param>
/// <param name="PresentAttributes">The attributes the start tag already has, for an attribute name or value.</param>
/// <param name="JustClosedElement">
/// The element whose start tag the character before the caret closed (its <c>&gt;</c>), or <c>null</c>; an end tag
/// for it can be put after the caret.
/// </param>
public sealed record XmlCaretContext(
    XmlCaretContextKind Kind,
    IReadOnlyList<string> Path,
    string Prefix,
    string? AttributeName,
    IReadOnlyList<string> PresentAttributes,
    string? JustClosedElement)
{
    /// <summary>Finds the context of <paramref name="offset"/> in <paramref name="text"/>.</summary>
    /// <remarks>
    /// Only the text before the caret is read, and it need not be well formed: the point is to help while it is
    /// being typed. An end tag closes the nearest open element of its name.
    /// </remarks>
    public static XmlCaretContext At(string text, int offset) => new XmlCaretScanner(text, Math.Clamp(offset, 0, text.Length)).Scan();
}
