namespace Polhem.AI.Claude
{
    /// <summary>
    /// Settings of a chat client that <see cref="ClaudeChatClientFactory"/> creates.
    /// </summary>
    /// <remarks>
    /// A <c>ModelId</c> or <c>MaxOutputTokens</c> set on the chat options of a request overrides <see cref="Model"/> and
    /// <see cref="MaxOutputTokens"/> for that request.
    /// </remarks>
    public sealed class ClaudeChatOptions
    {
        /// <summary>The model used when <see cref="Model"/> is not changed.</summary>
        public const string DefaultModel = "claude-opus-5-5";

        /// <summary>
        /// Gets or sets the model id. The default is <see cref="DefaultModel"/>.
        /// </summary>
        public string Model { get; set; } = DefaultModel;

        /// <summary>
        /// Gets or sets the effort sent with every request. The default is <see cref="ClaudeEffort.High"/>.
        /// </summary>
        /// <remarks>
        /// The effort is always sent, because the default of a model changes between models: it is medium on Claude Opus
        /// 5.5 and high on Claude Opus 5.
        /// </remarks>
        public ClaudeEffort Effort { get; set; } = ClaudeEffort.High;

        /// <summary>
        /// Gets or sets the most tokens one response may produce, thinking included. The default is 64000.
        /// </summary>
        /// <remarks>
        /// Responses are streamed, so a large limit does not run into a request timeout. A response that reaches the limit
        /// is reported with <see cref="ClaudeChatException"/> rather than returned cut short.
        /// </remarks>
        public int MaxOutputTokens { get; set; } = 64000;

        /// <summary>
        /// Gets or sets whether a request the model's safety classifiers decline is answered by a fallback model the
        /// server chooses. The default is <see langword="true"/>.
        /// </summary>
        public bool EnableFallbacks { get; set; } = true;
    }
}
