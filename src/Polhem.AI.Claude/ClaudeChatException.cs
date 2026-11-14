namespace Polhem.AI.Claude
{
    /// <summary>
    /// The exception thrown when a request to Claude produced no usable answer.
    /// </summary>
    /// <remarks>
    /// Rate limits, overloaded servers and network failures are retried by the Anthropic SDK; when the retries run out,
    /// the SDK's own exception reaches the caller. This exception covers the outcomes a retry does not fix.
    /// </remarks>
    public sealed class ClaudeChatException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClaudeChatException"/> class.
        /// </summary>
        /// <param name="kind">Why there is no answer.</param>
        /// <param name="message">The message, written for the person who started the request.</param>
        /// <param name="refusalCategory">The policy category of a refusal, when the API names one.</param>
        /// <param name="innerException">The exception that caused this one, if any.</param>
        public ClaudeChatException(ClaudeFailureKind kind, string message, string? refusalCategory = null,
            Exception? innerException = null)
            : base(message, innerException)
        {
            Kind = kind;
            RefusalCategory = refusalCategory;
        }

        /// <summary>
        /// Gets why there is no answer.
        /// </summary>
        public ClaudeFailureKind Kind { get; }

        /// <summary>
        /// Gets the policy category of a refusal, such as <c>cyber</c>, or <see langword="null"/> when the API names none
        /// or <see cref="Kind"/> is not <see cref="ClaudeFailureKind.Refused"/>.
        /// </summary>
        public string? RefusalCategory { get; }
    }
}
