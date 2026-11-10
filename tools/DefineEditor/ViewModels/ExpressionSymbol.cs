namespace Polhem.DefineEditor.ViewModels;

/// <summary>A name an expression can use, listed beside the expression editor.</summary>
/// <param name="Name">The name as listed.</param>
/// <param name="InsertText">The text put at the caret when the name is chosen.</param>
/// <param name="Detail">The type or signature shown after the name.</param>
public sealed record ExpressionSymbol(string Name, string InsertText, string Detail);
