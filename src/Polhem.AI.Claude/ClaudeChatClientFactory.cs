using Anthropic;
using Microsoft.Extensions.AI;

namespace Polhem.AI.Claude
{
    /// <summary>
    /// Creates chat clients that talk to Claude through the official Anthropic SDK.
    /// </summary>
    public static class ClaudeChatClientFactory
    {
        /// <summary>
        /// Creates a chat client with the credentials the Anthropic SDK finds on this machine.
        /// </summary>
        /// <param name="options">The settings; <see langword="null"/> uses the defaults.</param>
        /// <returns>A chat client. Disposing it releases the SDK client it created.</returns>
        /// <remarks>
        /// No credential is read here, and none is kept by this package: the SDK resolves one on the first request, in
        /// the order <see cref="ClaudeCredentials.Describe()"/> reports. Call that method to show the user which
        /// credential will be used before anything is sent.
        /// </remarks>
        public static IChatClient Create(ClaudeChatOptions? options)
            => Create(new AnthropicClient(), options, ownsClient: true);

        /// <summary>
        /// Creates a chat client over an Anthropic SDK client the caller configured, such as one with its own HTTP client
        /// or base URL.
        /// </summary>
        /// <param name="client">The SDK client. It is not disposed with the chat client.</param>
        /// <param name="options">The settings; <see langword="null"/> uses the defaults.</param>
        /// <returns>A chat client.</returns>
        public static IChatClient Create(AnthropicClient client, ClaudeChatOptions? options)
            => Create(client, options, ownsClient: false);

        private static ClaudeChatClient Create(AnthropicClient client, ClaudeChatOptions? options, bool ownsClient)
        {
            ArgumentNullException.ThrowIfNull(client);
            options ??= new ClaudeChatOptions();
            ArgumentException.ThrowIfNullOrWhiteSpace(options.Model, nameof(options));
            ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxOutputTokens, 1, nameof(options));
            if (!Enum.IsDefined(options.Effort))
                throw new ArgumentOutOfRangeException(nameof(options), options.Effort, "Unknown effort.");

            var settings = new ClaudeChatOptions
            {
                Model = options.Model,
                Effort = options.Effort,
                MaxOutputTokens = options.MaxOutputTokens,
                EnableFallbacks = options.EnableFallbacks,
            };
            var inner = client.Beta.AsIChatClient(settings.Model, settings.MaxOutputTokens);
            return new ClaudeChatClient(inner, settings, ownsClient ? client : null);
        }
    }
}
