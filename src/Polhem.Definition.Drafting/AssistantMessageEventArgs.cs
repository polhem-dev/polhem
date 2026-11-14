namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// The text of a reply the model wrote to the user, raised by <see cref="FormDraftSession.AssistantMessage"/>.
    /// </summary>
    public sealed class AssistantMessageEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AssistantMessageEventArgs"/> class.
        /// </summary>
        /// <param name="text">The text of the reply.</param>
        public AssistantMessageEventArgs(string text)
        {
            Text = text;
        }

        /// <summary>Gets the text of the reply.</summary>
        public string Text { get; }
    }
}
