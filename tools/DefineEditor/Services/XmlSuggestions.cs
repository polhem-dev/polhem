namespace Polhem.DefineEditor.Services;

/// <summary>The suggestions at the caret, and where the text they replace starts.</summary>
/// <param name="Items">What can go at the caret; empty when nothing can.</param>
/// <param name="ReplaceFrom">The offset where the typed prefix, which a suggestion replaces, starts.</param>
public sealed record XmlSuggestions(IReadOnlyList<XmlSuggestion> Items, int ReplaceFrom)
{
    /// <summary>No suggestion.</summary>
    public static XmlSuggestions None(int offset) => new([], offset);
}
