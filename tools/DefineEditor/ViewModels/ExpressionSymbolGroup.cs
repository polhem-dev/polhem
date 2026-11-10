namespace Polhem.DefineEditor.ViewModels;

/// <summary>A heading in the expression editor's symbol tree, with what is listed under it.</summary>
/// <param name="Title">The heading shown.</param>
/// <param name="Items">The names under the heading: <see cref="ExpressionSymbol"/>s, or further headings.</param>
/// <param name="IsExpanded">Whether the heading starts open.</param>
public sealed record ExpressionSymbolGroup(string Title, IReadOnlyList<object> Items, bool IsExpanded = false);
