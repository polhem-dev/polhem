namespace Polhem.DefineEditor.ViewModels;

/// <summary>A name an expression can use, listed beside the expression editor.</summary>
/// <param name="Name">The name as listed.</param>
/// <param name="InsertText">The text put at the caret when the name is chosen.</param>
/// <param name="Detail">What is shown after the name: a type, a signature, or what a function returns.</param>
/// <param name="Description">More for the tooltip, below <paramref name="Detail"/>; empty when there is nothing more.</param>
public sealed record ExpressionSymbol(string Name, string InsertText, string Detail, string Description = "")
{
    /// <summary>The tooltip: the detail, then the description when there is one.</summary>
    public string ToolTipText => Description.Length > 0 ? Detail + Environment.NewLine + Description : Detail;
}
