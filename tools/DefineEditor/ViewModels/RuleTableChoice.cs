using Polhem.Definition.Forms;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>A table a rule can check, as the rule dialog offers it.</summary>
/// <param name="TargetTable">The value of <see cref="FormRule.TargetTable"/>: empty for the master table.</param>
/// <param name="Table">The table; <c>null</c> when the rule names a table the form does not have.</param>
/// <param name="Label">What the list shows.</param>
public sealed record RuleTableChoice(string TargetTable, FormTable? Table, string Label)
{
    /// <inheritdoc/>
    public override string ToString() => Label;
}
