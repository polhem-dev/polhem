using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.UnitTests
{
    public class CallLogChatClientTests
    {
        private static readonly DateTimeOffset s_started = new(2026, 11, 24, 10, 15, 3, 123, TimeSpan.FromHours(8));
        private static readonly ChatMessage[] s_question = [new(ChatRole.User, "Draft the form.")];

        [Fact]
        [DisplayName("A streamed call writes one line with its reasoning, text, tool calls and tokens, and none of the conversation")]
        public async Task GetStreamingResponseAsync_CompletedCall_WritesCountsOnly()
        {
            var lines = new List<string>();
            using var client = new CallLogChatClient(new StreamingClient(
                Update(new TextReasoningContent("Read ")),
                Update(new TextReasoningContent("the form.")),
                Update(new TextReasoningContent(string.Empty) { ProtectedData = "sig" }),
                Update(new TextContent("Secret reply.")),
                Update(new FunctionCallContent("1", "upsert_table")),
                new ChatResponseUpdate
                {
                    Role = ChatRole.Assistant,
                    ModelId = "claude-test",
                    FinishReason = ChatFinishReason.ToolCalls,
                    Contents = [new UsageContent(new UsageDetails { InputTokenCount = 1200, CachedInputTokenCount = 1000, OutputTokenCount = 300 })],
                }), "form-drafting", lines.Add, () => s_started);

            await Drain(client);

            var line = Assert.Single(lines);
            Assert.StartsWith("2026-11-24T10:15:03.123+08:00 form-drafting elapsed=", line);
            Assert.Contains(" model=claude-test finish=tool_calls reasoning=1 reasoningChars=14 emptyReasoning=0 firstReasoning=", line);
            Assert.EndsWith(" textChars=13 toolCalls=1 in=1200 cached=1000 out=300", line);
            Assert.DoesNotContain("Secret", line);
            Assert.DoesNotContain("Read", line);
        }

        [Fact]
        [DisplayName("A call whose reasoning block came back without text is told apart from a call that did not reason")]
        public async Task GetStreamingResponseAsync_ReasoningWithoutText_CountsAnEmptyBlock()
        {
            var lines = new List<string>();
            using var signedOnly = new CallLogChatClient(new StreamingClient(
                Update(new TextReasoningContent(string.Empty) { ProtectedData = "sig" }),
                Update(new TextContent("Done."))), "rule-assistant", lines.Add, () => s_started);
            using var noReasoning = new CallLogChatClient(new StreamingClient(
                Update(new TextContent("Done."))), "rule-assistant", lines.Add, () => s_started);

            await Drain(signedOnly);
            await Drain(noReasoning);

            Assert.Contains(" reasoning=1 reasoningChars=0 emptyReasoning=1 firstReasoning=- ", lines[0]);
            Assert.Contains(" reasoning=0 reasoningChars=0 emptyReasoning=0 firstReasoning=- ", lines[1]);
        }

        [Fact]
        [DisplayName("A call that fails while it streams still writes its line, marked as failed, and the failure reaches the caller")]
        public async Task GetStreamingResponseAsync_Failure_WritesFailedLine()
        {
            var lines = new List<string>();
            using var client = new CallLogChatClient(new StreamingClient(Update(new TextContent("Partly")))
            {
                FailAfterUpdates = true,
            }, "form-drafting", lines.Add, () => s_started);

            await Assert.ThrowsAsync<InvalidOperationException>(() => Drain(client));

            Assert.EndsWith(" outcome=failed", Assert.Single(lines));
        }

        [Fact]
        [DisplayName("A call that is not streamed writes the same line without the time to the first reasoning")]
        public async Task GetResponseAsync_CompletedCall_WritesLine()
        {
            var lines = new List<string>();
            using var client = new CallLogChatClient(new StreamingClient(
                Update(new TextReasoningContent("Think.")), Update(new TextContent("Done."))), "rule-assistant", lines.Add, () => s_started);

            var response = await client.GetResponseAsync(s_question);

            Assert.Equal("Done.", response.Text);
            Assert.Contains(" reasoning=1 reasoningChars=6 emptyReasoning=0 firstReasoning=- textChars=5 toolCalls=0", Assert.Single(lines));
        }

        [Fact]
        [DisplayName("The log file starts over once it passes its size limit, keeping the previous one")]
        public void Append_PastLimit_RotatesTheFile()
        {
            var directory = Path.Combine(Path.GetTempPath(), "polhem-call-log-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "ai-calls.log");
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(path, new string('x', (int)CallLogFile.MaxBytes));

                CallLogFile.Append(path, "next");

                Assert.Equal("next" + Environment.NewLine, File.ReadAllText(path));
                Assert.Equal(CallLogFile.MaxBytes, new FileInfo(Path.Combine(directory, "ai-calls.old.log")).Length);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static ChatResponseUpdate Update(AIContent content) => new(ChatRole.Assistant, [content]);

        private static async Task Drain(IChatClient client)
        {
            await foreach (var _ in client.GetStreamingResponseAsync(s_question))
            {
                // Only the line the wrapper writes is under test.
            }
        }

        private sealed class StreamingClient(params ChatResponseUpdate[] updates) : IChatClient
        {
            public bool FailAfterUpdates { get; init; }

            public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
                CancellationToken cancellationToken = default)
            {
                var collected = new List<ChatResponseUpdate>();
                await foreach (var update in GetStreamingResponseAsync(messages, options, cancellationToken))
                    collected.Add(update);
                return collected.ToChatResponse();
            }

            public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
                ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                foreach (var update in updates)
                {
                    await Task.Yield();
                    yield return update;
                }
                if (FailAfterUpdates) throw new InvalidOperationException("The model went away.");
            }

            public object? GetService(Type serviceType, object? serviceKey = null) => null;

            public void Dispose()
            {
                // Nothing to release.
            }
        }
    }
}
