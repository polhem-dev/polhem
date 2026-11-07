namespace Polhem.DefineEditor.Services;

/// <summary>What an <see cref="XmlSuggestion"/> puts in the text.</summary>
public enum XmlSuggestionKind
{
    /// <summary>A start tag's name.</summary>
    Element,

    /// <summary>The end tag of the element the caret is inside.</summary>
    EndTag,

    /// <summary>An attribute with empty quotes, the caret between them.</summary>
    Attribute,

    /// <summary>An attribute's value.</summary>
    Value,
}
