using System.Runtime.CompilerServices;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.AI;
using NonBeta = Anthropic.Models.Messages;

namespace Polhem.AI.Claude
{
    /// <summary>
    /// Wraps the Anthropic SDK's chat client with the request settings and the response checks Polhem relies on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every request streams, even through <see cref="GetResponseAsync"/>, so a long answer never runs into a request
    /// timeout. Each request carries adaptive thinking, the configured effort, server-side refusal fallbacks, strict
    /// tools and automatic prompt caching, plus a cache breakpoint at the end of the first user message, which holds
    /// the documents.
    /// </para>
    /// <para>
    /// The SDK adapter drops the <c>fallback</c> block that marks where a fallback model took over, and the API rejects
    /// a later request that echoes thinking from both sides of that point without it. This client keeps the block in
    /// the response and removes the declined attempt's thinking and tool calls before it, as the API asks.
    /// </para>
    /// </remarks>
    internal sealed class ClaudeChatClient : DelegatingChatClient
    {
        internal const string FallbackBeta = "server-side-fallback-2026-07-01";

        private readonly ClaudeChatOptions _settings;
        private readonly AnthropicClient? _ownedClient;

        public ClaudeChatClient(IChatClient innerClient, ClaudeChatOptions settings, AnthropicClient? ownedClient)
            : base(innerClient)
        {
            _settings = settings;
            _ownedClient = ownedClient;
        }

        public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var updates = new List<ChatResponseUpdate>();
            string? refusalCategory = null;
            await foreach (var update in GetStreamingResponseAsync(messages, options, cancellationToken).ConfigureAwait(false))
            {
                updates.Add(update);
                if (update.RawRepresentation is BetaRawMessageStreamEvent raw
                    && raw.TryPickDelta(out var delta)
                    && delta.Delta.StopDetails is { } details)
                {
                    refusalCategory = details.Category?.Raw();
                }
            }

            var response = updates.ToChatResponse();
            if (response.FinishReason == ChatFinishReason.ContentFilter)
            {
                throw new ClaudeChatException(ClaudeFailureKind.Refused,
                    refusalCategory is null
                        ? "Claude declined the request."
                        : $"Claude declined the request (category: {refusalCategory}).",
                    refusalCategory);
            }
            if (response.FinishReason == ChatFinishReason.Length)
            {
                // A tool call cut off here has partial arguments; running it would act on input the model never finished.
                throw new ClaudeChatException(ClaudeFailureKind.OutputLimitReached,
                    "Claude's answer reached the output token limit and was cut short.");
            }

            foreach (var message in response.Messages)
                DropDeclinedAttempt(message);
            return response;
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(messages);
            var request = WithDocumentBreakpoint(messages);
            var requestOptions = Configure(options);

            await using var enumerator = base.GetStreamingResponseAsync(request, requestOptions, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);
            while (true)
            {
                ChatResponseUpdate update;
                try
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                        yield break;
                    update = enumerator.Current;
                }
                catch (AnthropicUnauthorizedException ex)
                {
                    throw new ClaudeChatException(ClaudeFailureKind.NotAuthenticated,
                        "The Claude API did not accept the credentials. Run `ant auth login` again, "
                        + "or check the ANTHROPIC_API_KEY environment variable.",
                        innerException: ex);
                }

                if (update.RawRepresentation is BetaRawMessageStreamEvent raw
                    && raw.TryPickContentBlockStart(out var start)
                    && start.ContentBlock.TryPickBetaFallback(out var fallback))
                {
                    update.Contents.Add(FallbackMarker(fallback));
                }
                yield return update;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _ownedClient?.Dispose();
            base.Dispose(disposing);
        }

        internal ChatOptions Configure(ChatOptions? options)
        {
            var configured = options?.Clone() ?? new ChatOptions();
            var model = configured.ModelId ?? _settings.Model;
            var maxTokens = configured.MaxOutputTokens ?? _settings.MaxOutputTokens;
            var callerFactory = configured.RawRepresentationFactory;
            configured.RawRepresentationFactory = client => BuildParams(
                callerFactory?.Invoke(client) as MessageCreateParams, model, maxTokens);

            if (configured.Tools is { Count: > 0 } tools)
            {
                configured.Tools = [.. tools.Select(tool => tool is AIFunctionDeclaration declaration
                    ? new StrictToolDeclaration(declaration)
                    : tool)];
            }
            return configured;
        }

        private MessageCreateParams BuildParams(MessageCreateParams? template, string model, int maxTokens)
        {
            var parameters = template ?? new MessageCreateParams { Model = model, MaxTokens = maxTokens, Messages = [] };
            parameters = parameters with
            {
                Thinking = parameters.Thinking ?? new BetaThinkingConfigAdaptive(),
                OutputConfig = (parameters.OutputConfig ?? new BetaOutputConfig()) with
                {
                    Effort = parameters.OutputConfig?.Effort ?? ToEffort(_settings.Effort),
                },
                CacheControl = parameters.CacheControl ?? new BetaCacheControlEphemeral(),
            };

            if (_settings.EnableFallbacks && parameters.Fallbacks is null)
            {
                var betas = parameters.Betas?.ToList() ?? [];
                if (!betas.Any(beta => string.Equals(beta.Raw(), FallbackBeta, StringComparison.Ordinal)))
                    betas.Add(AnthropicBeta.ServerSideFallback2026_07_01);
                parameters = parameters with { Fallbacks = new Default(), Betas = betas };
            }
            return parameters;
        }

        private static Effort ToEffort(ClaudeEffort effort) => effort switch
        {
            ClaudeEffort.Low => Effort.Low,
            ClaudeEffort.Medium => Effort.Medium,
            ClaudeEffort.High => Effort.High,
            ClaudeEffort.ExtraHigh => Effort.Xhigh,
            ClaudeEffort.Max => Effort.Max,
            _ => throw new ArgumentOutOfRangeException(nameof(effort), effort, "Unknown effort."),
        };

        /// <summary>
        /// Copies the messages and marks the end of the first user message as a cache breakpoint, without changing the
        /// caller's message or content instances.
        /// </summary>
        internal static List<ChatMessage> WithDocumentBreakpoint(IEnumerable<ChatMessage> messages)
        {
            var list = messages.ToList();
            var index = list.FindIndex(message => message.Role == ChatRole.User);
            if (index < 0 || list[index].Contents.Count == 0)
                return list;

            var contents = list[index].Contents.ToList();
            var marked = CopyForBreakpoint(contents[^1]);
            if (marked is null)
                return list;
            contents[^1] = marked.WithCacheControl(new NonBeta::CacheControlEphemeral());

            var copy = list[index].Clone();
            copy.Contents = contents;
            list[index] = copy;
            return list;
        }

        private static AIContent? CopyForBreakpoint(AIContent content) => content switch
        {
            TextContent text => new TextContent(text.Text),
            DataContent data => new DataContent(data.Data, data.MediaType) { Name = data.Name },
            _ => null,
        };

        private static AIContent FallbackMarker(BetaFallbackBlock fallback) => new()
        {
            RawRepresentation = new BetaContentBlockParam(new BetaFallbackBlockParam
            {
                From = new BetaFallbackInfoParam { Model = fallback.From.Model },
                To = new BetaFallbackInfoParam { Model = fallback.To.Model },
            }),
        };

        /// <summary>
        /// Removes the thinking and tool calls of an attempt a fallback model replaced, keeping the fallback block where
        /// it appeared. The declined attempt's text stays: the API passes it to the fallback model as context.
        /// </summary>
        internal static void DropDeclinedAttempt(ChatMessage message)
        {
            var boundary = -1;
            for (var i = 0; i < message.Contents.Count; i++)
            {
                if (IsFallbackMarker(message.Contents[i]))
                    boundary = i;
            }
            if (boundary < 0)
                return;

            var kept = message.Contents
                .Where((content, i) => i >= boundary || content is not (TextReasoningContent or FunctionCallContent))
                .ToList();
            message.Contents = kept;
        }

        internal static bool IsFallbackMarker(AIContent content)
            => content.RawRepresentation is BetaContentBlockParam block && block.Value is BetaFallbackBlockParam;
    }
}
