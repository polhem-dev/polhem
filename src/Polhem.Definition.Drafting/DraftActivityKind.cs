namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// What a <see cref="FormDraftSession"/> is doing, as <see cref="FormDraftSession.Activity"/> reports it.
    /// </summary>
    public enum DraftActivityKind
    {
        /// <summary>A request is about to go to the model; it is thinking until the response arrives.</summary>
        Thinking,

        /// <summary>The model's response carried a summary of its reasoning, in <see cref="DraftActivityEventArgs.Detail"/>.</summary>
        Reasoning,

        /// <summary>A tool the model called is about to run.</summary>
        ToolStarted,

        /// <summary>A tool ran.</summary>
        ToolCompleted,

        /// <summary>A tool was not run or refused its input; the reason is in <see cref="DraftActivityEventArgs.Detail"/>.</summary>
        ToolRefused,
    }
}
