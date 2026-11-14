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

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

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

        /// <summary>Gets the results the session returned for the tool calls, from the last request.</summary>
        public IReadOnlyList<FunctionResultContent> LastResults()
            => Requests[^1].Messages[^1].Contents.OfType<FunctionResultContent>().ToList();
    }
}
