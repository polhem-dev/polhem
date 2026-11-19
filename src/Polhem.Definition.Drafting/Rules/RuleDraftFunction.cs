namespace Polhem.Definition.Drafting.Rules
{
    /// <summary>
    /// A function an expression may call, as the model is told about it: the host fills these from the expression
    /// engine's catalog, so the drafting package does not depend on the engine.
    /// </summary>
    /// <param name="Name">The name an expression calls it by.</param>
    /// <param name="Signature">Its parameters and result, such as <c>(object value) → bool</c>.</param>
    /// <param name="Summary">What it returns, in one English sentence.</param>
    /// <param name="Example">An expression that calls it.</param>
    public sealed record RuleDraftFunction(string Name, string Signature, string Summary, string Example);
}
