namespace Polhem.Definition.Forms
{
    /// <summary>
    /// Where one aggregate call sat in the original expression and where its variable sits in the rewritten one, so a
    /// position the expression engine reports in the rewritten text can be mapped back to what the author wrote.
    /// </summary>
    /// <param name="OriginalStart">The offset of the call in the original expression.</param>
    /// <param name="OriginalLength">The length of the call, from its name to its closing parenthesis.</param>
    /// <param name="RewrittenStart">The offset of the variable in the rewritten expression.</param>
    /// <param name="VariableName">The variable that replaced the call.</param>
    /// <param name="ConditionStart">The offset of the call's condition in the original expression; -1 when it has none.</param>
    internal sealed record DetailAggregateSpan(int OriginalStart, int OriginalLength, int RewrittenStart, string VariableName,
        int ConditionStart = -1);
}
