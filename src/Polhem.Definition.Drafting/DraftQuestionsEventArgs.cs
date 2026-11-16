namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// The questions the model asked the user, raised by <see cref="FormDraftSession.QuestionsAsked"/>.
    /// </summary>
    public sealed class DraftQuestionsEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DraftQuestionsEventArgs"/> class.
        /// </summary>
        /// <param name="questions">The questions, in the order the model asked them.</param>
        public DraftQuestionsEventArgs(IReadOnlyList<DraftQuestion> questions)
        {
            ArgumentNullException.ThrowIfNull(questions);
            Questions = questions;
        }

        /// <summary>Gets the questions, in the order the model asked them.</summary>
        public IReadOnlyList<DraftQuestion> Questions { get; }
    }
}
