namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// One answer the model offers to a <see cref="DraftQuestion"/>.
    /// </summary>
    public sealed class DraftQuestionOption
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DraftQuestionOption"/> class.
        /// </summary>
        /// <param name="label">The answer, short enough for a button.</param>
        /// <param name="description">What choosing it means; may be empty.</param>
        public DraftQuestionOption(string label, string description)
        {
            Label = label;
            Description = description;
        }

        /// <summary>Gets the answer, short enough for a button.</summary>
        public string Label { get; }

        /// <summary>Gets what choosing the answer means; may be empty.</summary>
        public string Description { get; }
    }
}
