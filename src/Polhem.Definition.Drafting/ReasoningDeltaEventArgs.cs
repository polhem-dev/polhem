namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// A piece of the reasoning summary the model writes before it answers, raised as it arrives, for example by
    /// <see cref="FormDraftSession.ReasoningDelta"/> and <see cref="Rules.RuleDrafting.ReasoningDelta"/>.
    /// </summary>
    public sealed class ReasoningDeltaEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReasoningDeltaEventArgs"/> class.
        /// </summary>
        /// <param name="text">The reasoning that arrived since the previous piece.</param>
        public ReasoningDeltaEventArgs(string text)
        {
            Text = text;
        }

        /// <summary>Gets the reasoning that arrived since the previous piece; it may be only whitespace.</summary>
        public string Text { get; }
    }
}
