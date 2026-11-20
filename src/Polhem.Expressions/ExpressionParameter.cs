namespace Polhem.Expressions
{
    /// <summary>
    /// Describes one parameter of an <see cref="ExpressionFunction"/>.
    /// </summary>
    /// <param name="Name">The parameter's name, as a definition editor shows it in the function's signature.</param>
    /// <param name="Type">The type an expression passes for the parameter.</param>
    public sealed record ExpressionParameter(string Name, Type Type);
}
