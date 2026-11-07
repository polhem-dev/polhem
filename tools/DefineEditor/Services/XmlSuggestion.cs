namespace Polhem.DefineEditor.Services;

/// <summary>Something the XML view's completion offers at the caret.</summary>
/// <param name="Kind">What it puts in the text.</param>
/// <param name="Label">What the list shows, and what typing filters by.</param>
/// <param name="InsertText">What replaces the typed prefix.</param>
/// <param name="Detail">A line about it for the list's tooltip: a type, a default; empty when there is nothing to say.</param>
/// <param name="CaretOffset">
/// Where the caret goes, counted back from the end of <paramref name="InsertText"/>: 1 puts it inside the quotes of an
/// attribute.
/// </param>
/// <param name="OffersValues">Whether its values are offered right after it is put in, as for an enum attribute.</param>
public sealed record XmlSuggestion(
    XmlSuggestionKind Kind,
    string Label,
    string InsertText,
    string Detail,
    int CaretOffset,
    bool OffersValues);
