namespace Polhem.DefineEditor.Services;

/// <summary>Something the expression editor's completion offers at the caret.</summary>
/// <param name="Kind">What it names.</param>
/// <param name="Label">What the list shows, and what typing filters by.</param>
/// <param name="InsertText">What replaces the typed prefix.</param>
/// <param name="Detail">A type or a signature for the list's tooltip; empty when there is nothing to say.</param>
public sealed record ExpressionSuggestion(ExpressionSuggestionKind Kind, string Label, string InsertText, string Detail);
