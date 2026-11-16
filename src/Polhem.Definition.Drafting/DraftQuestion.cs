namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// A decision the model asks the user to make, with the answers it offers.
    /// </summary>
    /// <remarks>
    /// The user is not limited to the options. The host sends the answers back as an ordinary message with
    /// <see cref="FormDraftSession.SendAsync"/>, so an answer the user types in their own words works the same way.
    /// </remarks>
    public sealed class DraftQuestion
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DraftQuestion"/> class.
        /// </summary>
        /// <param name="text">The question.</param>
        /// <param name="options">The answers the model offers.</param>
        /// <param name="recommendedIndex">The index in <paramref name="options"/> of the answer the model suggests, or -1.</param>
        public DraftQuestion(string text, IReadOnlyList<DraftQuestionOption> options, int recommendedIndex)
        {
            ArgumentNullException.ThrowIfNull(options);
            if (recommendedIndex < -1 || recommendedIndex >= options.Count)
                throw new ArgumentOutOfRangeException(nameof(recommendedIndex), recommendedIndex, "Not an index of the options, nor -1.");
            Text = text;
            Options = options;
            RecommendedIndex = recommendedIndex;
        }

        /// <summary>Gets the question.</summary>
        public string Text { get; }

        /// <summary>Gets the answers the model offers.</summary>
        public IReadOnlyList<DraftQuestionOption> Options { get; }

        /// <summary>Gets the index in <see cref="Options"/> of the answer the model suggests, or -1 when it suggests none.</summary>
        public int RecommendedIndex { get; }
    }
}
