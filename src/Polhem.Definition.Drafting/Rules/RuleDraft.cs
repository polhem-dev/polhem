using Polhem.Definition.Forms;

namespace Polhem.Definition.Drafting.Rules
{
    /// <summary>
    /// A rule a model wrote from the user's words, after it passed the checks an expression from an untrusted author
    /// must pass and every sample was computed by the expression engine.
    /// </summary>
    /// <param name="RuleId">The rule id, unique in the form.</param>
    /// <param name="TargetTable">The table the rule checks; empty for the master table.</param>
    /// <param name="When">The applicability condition; empty when the rule always applies.</param>
    /// <param name="Condition">The condition that must hold.</param>
    /// <param name="Message">The English message shown when the condition fails, the base text of the rule.</param>
    /// <param name="MessageTranslations">The message in each language the host asked for, keyed by culture name.</param>
    /// <param name="Explanation">What the rule does, in plain words in the language the user reads.</param>
    /// <param name="Samples">Rows of sample values with what the rule does with each.</param>
    public sealed record RuleDraft(string RuleId, string TargetTable, string When, string Condition, string Message,
        IReadOnlyDictionary<string, string> MessageTranslations, string Explanation, IReadOnlyList<RuleDraftSample> Samples)
    {
        /// <summary>
        /// Builds the <see cref="FormRule"/> to add to the form: a before-save rule, enabled.
        /// </summary>
        /// <returns>A new rule; the translations go to the form's language resources, which the caller writes.</returns>
        public FormRule ToFormRule() => new(RuleId, Condition, Message)
        {
            TargetTable = TargetTable,
            When = When,
        };
    }
}
