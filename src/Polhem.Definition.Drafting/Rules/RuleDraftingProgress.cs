namespace Polhem.Definition.Drafting.Rules
{
    /// <summary>
    /// A step of a request of <see cref="RuleDrafting"/>, raised before each call to the model and after it returns.
    /// </summary>
    /// <param name="Usage">What the request has cost so far.</param>
    /// <param name="WaitingForModel"><see langword="true"/> while a call to the model is in progress; <see langword="false"/>
    /// once it returned, while the submission is checked.</param>
    public sealed record RuleDraftingProgress(RuleDraftingUsage Usage, bool WaitingForModel);
}
