namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// A piece of a reply the model is still writing, raised by <see cref="FormDraftSession.AssistantMessageDelta"/> and
    /// <see cref="Rules.RuleDrafting.ReplyDelta"/>.
    /// </summary>
    public sealed class AssistantMessageDeltaEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AssistantMessageDeltaEventArgs"/> class.
        /// </summary>
        /// <param name="text">The text that arrived since the previous piece.</param>
        public AssistantMessageDeltaEventArgs(string text)
        {
            Text = text;
        }

        /// <summary>Gets the text that arrived since the previous piece; it may be only whitespace.</summary>
        public string Text { get; }
    }
}
