namespace Polhem.Definition.Drafting.Rules
{
    /// <summary>
    /// What a rule does with one row of sample values, as the expression engine computes it.
    /// </summary>
    public enum RuleSampleOutcome
    {
        /// <summary>The rule applies and its condition holds: the save goes ahead.</summary>
        Passes,
        /// <summary>The rule applies and its condition fails: the save is stopped with the rule's message.</summary>
        Blocked,
        /// <summary>The rule's applicability condition (<see cref="Polhem.Definition.Forms.FormRule.When"/>) is false, so the rule is skipped.</summary>
        Skipped,
    }
}
