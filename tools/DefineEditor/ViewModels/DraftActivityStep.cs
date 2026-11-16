namespace Polhem.DefineEditor.ViewModels;

/// <summary>One step in a <see cref="DraftActivityCard"/>: a tool the model ran, or a summary of its reasoning.</summary>
/// <param name="Text">What the step did, in the editor's language.</param>
/// <param name="Detail">The reason a tool was refused or the reasoning summary, in the model's words; may be empty.</param>
/// <param name="IsError">Whether the tool was refused.</param>
/// <param name="IsReasoning">Whether the step is the model's reasoning rather than a tool.</param>
public sealed record DraftActivityStep(string Text, string Detail, bool IsError, bool IsReasoning)
{
    /// <summary>Gets whether there is a detail to show under the step.</summary>
    public bool HasDetail => Detail.Length > 0;
}
