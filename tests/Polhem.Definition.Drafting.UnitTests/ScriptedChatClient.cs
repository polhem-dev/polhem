using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Polhem.Definition.Drafting.UnitTests
{
    /// <summary>
    /// A chat client that answers each request with the next scripted response and records what it was sent. It never
    /// calls a real model.
    /// </summary>
    internal sealed class ScriptedChatClient : IChatClient
    {
        private readonly Queue<Func<IReadOnlyList<ChatMessage>, CancellationToken, ChatResponse>> _script = new();

        /// <summary>Gets each request: the messages (a copy of the list, the same message instances) and the options.</summary>
        public List<(IReadOnlyList<ChatMessage> Messages, ChatOptions? Options)> Requests { get; } = [];

        public ScriptedChatClient Then(params AIContent[] contents)
            => Then((_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, [.. contents])));

        public ScriptedChatClient Then(Func<IReadOnlyList<ChatMessage>, CancellationToken, ChatResponse> step)
        {
            _script.Enqueue(step);
            return this;
        }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = messages.ToList();
            Requests.Add((snapshot, options));
            if (_script.Count == 0)
                throw new InvalidOperationException("The script has no more responses.");
            return Task.FromResult(_script.Dequeue()(snapshot, cancellationToken));
        }

        /// <summary>
        /// Gets or sets the length of the pieces a streamed text or reasoning is cut into; 0 streams each in one piece.
        /// </summary>
        public int TextPieceLength { get; set; }

        /// <summary>
        /// Gets or sets what runs after each streamed update, once the caller has taken it and asked for the next one, so a
        /// test can look at what the caller showed for the pieces so far.
        /// </summary>
        public Action<ChatResponseUpdate>? AfterUpdate { get; set; }

        /// <summary>
        /// Streams the next scripted response: each text and reasoning in pieces of <see cref="TextPieceLength"/>, every other content
        /// in an update of its own, then the usage.
        /// </summary>
        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (var message in response.Messages)
            {
                foreach (var content in message.Contents)
                {
                    if (content is FailureContent failure)
                        throw new InvalidOperationException(failure.Message);
                    foreach (var piece in Pieces(content))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var update = new ChatResponseUpdate(message.Role, [piece]) { MessageId = "scripted" };
                        yield return update;
                        AfterUpdate?.Invoke(update);
                    }
                }
            }
            if (response.Usage is { } usage)
                yield return new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(usage)]) { MessageId = "scripted" };
        }

        private IEnumerable<AIContent> Pieces(AIContent content)
        {
            var (whole, protectedData) = content switch
            {
                TextContent text => (text.Text, null),
                TextReasoningContent reasoning => (reasoning.Text, reasoning.ProtectedData),
                _ => ((string?)null, (string?)null),
            };
            if (whole is null || TextPieceLength <= 0 || whole.Length <= TextPieceLength)
            {
                yield return content;
                yield break;
            }
            for (var start = 0; start < whole.Length; start += TextPieceLength)
            {
                var piece = whole.Substring(start, Math.Min(TextPieceLength, whole.Length - start));
                var last = start + TextPieceLength >= whole.Length;
                yield return content is TextReasoningContent
                    ? new TextReasoningContent(piece) { ProtectedData = last ? protectedData : null }
                    : new TextContent(piece);
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
            // Nothing to release.
        }

        /// <summary>
        /// Builds a tool call whose arguments are JSON values, the way a provider hands them over.
        /// </summary>
        public static FunctionCallContent Call(string callId, string name, string argumentsJson = "{}")
        {
            using var document = JsonDocument.Parse(argumentsJson);
            var arguments = document.RootElement.EnumerateObject()
                .ToDictionary(property => property.Name, property => (object?)property.Value.Clone());
            return new FunctionCallContent(callId, name, arguments);
        }

        public static TextContent Text(string text) => new(text);

        /// <summary>
        /// Marks where a streamed response breaks off: the stream throws <see cref="InvalidOperationException"/> with
        /// <paramref name="message"/> once the contents before it have been streamed.
        /// </summary>
        public static AIContent FailHere(string message) => new FailureContent(message);

        private sealed class FailureContent(string message) : AIContent
        {
            public string Message { get; } = message;
        }

        /// <summary>Gets the results the session returned for the tool calls, from the last request.</summary>
        public IReadOnlyList<FunctionResultContent> LastResults()
            => Requests[^1].Messages[^1].Contents.OfType<FunctionResultContent>().ToList();
    }
}
