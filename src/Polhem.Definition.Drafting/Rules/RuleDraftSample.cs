namespace Polhem.Definition.Drafting.Rules
{
    /// <summary>
    /// One row of sample values for a rule, with what the expression engine computed for it.
    /// </summary>
    /// <param name="Description">What the sample shows, in the language the user reads.</param>
    /// <param name="Values">The values by field name, coerced to each field's type; a field without one is empty.</param>
    /// <param name="DetailRows">For a master rule that reads detail tables, the sample's detail rows by table name, each
    /// row its values by field name; empty otherwise.</param>
    /// <param name="Outcome">What the rule does with the row, computed by the expression engine, not by the model.</param>
    public sealed record RuleDraftSample(string Description, IReadOnlyDictionary<string, object?> Values,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> DetailRows, RuleSampleOutcome Outcome);
}
