namespace Polhem.DefineEditor.ViewModels;

/// <summary>A heading in the expression editor's symbol tree, with the names under it.</summary>
/// <param name="Title">The heading shown.</param>
/// <param name="Items">The names listed under the heading.</param>
public sealed record ExpressionSymbolGroup(string Title, IReadOnlyList<ExpressionSymbol> Items);
