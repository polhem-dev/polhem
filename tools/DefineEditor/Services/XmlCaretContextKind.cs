namespace Polhem.DefineEditor.Services;

/// <summary>Where the caret is in an XML text, as far as completion is concerned.</summary>
public enum XmlCaretContextKind
{
    /// <summary>Between tags, or somewhere nothing can be suggested (a comment, a closing tag's tail).</summary>
    Content,

    /// <summary>In the name of a start tag, right after its <c>&lt;</c>.</summary>
    ElementName,

    /// <summary>In the name of an end tag, right after its <c>&lt;/</c>.</summary>
    EndTagName,

    /// <summary>In a start tag where an attribute name goes.</summary>
    AttributeName,

    /// <summary>Inside the quotes of an attribute's value.</summary>
    AttributeValue,
}
