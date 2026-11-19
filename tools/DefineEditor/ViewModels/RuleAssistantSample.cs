using Polhem.Definition.Drafting.Rules;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>One sample row of a drafted or explained rule, as the rule assistant lists it.</summary>
/// <param name="Description">What the sample shows.</param>
/// <param name="Values">The sample's values, and its detail rows, in one line.</param>
/// <param name="Outcome">What the expression engine computed for the row.</param>
/// <param name="OutcomeText">The outcome in the interface language.</param>
public sealed record RuleAssistantSample(string Description, string Values, RuleSampleOutcome Outcome, string OutcomeText)
{
    /// <summary>Whether the rule lets the row be saved.</summary>
    public bool Passes => Outcome == RuleSampleOutcome.Passes;

    /// <summary>Whether the rule stops the row from being saved.</summary>
    public bool IsBlocked => Outcome == RuleSampleOutcome.Blocked;

    /// <summary>Whether the rule does not apply to the row.</summary>
    public bool IsSkipped => Outcome == RuleSampleOutcome.Skipped;
}
