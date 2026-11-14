using System.ComponentModel;
using System.Net;
using Anthropic;
using Anthropic.Exceptions;
using Microsoft.Extensions.AI;

namespace Polhem.AI.Claude.UnitTests
{
    public class ClaudeChatClientTests
    {
        private static readonly string s_endTurn = Sse.MessageStart() + Sse.Text(0, "Done.") + Sse.MessageEnd("end_turn");

        [Fact]
        [DisplayName("A request carries the model, the effort, adaptive thinking, streaming and automatic caching")]
        public async Task GetResponseAsync_DefaultOptions_SendsClaudeSettings()
        {
            var handler = new FakeAnthropicHandler().ThenStream(s_endTurn);
            using var client = ClaudeChatClientFactory.Create(handler.CreateClient(), null);

            var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]);

            Assert.Equal("Done.", response.Text);
            var body = handler.Requests.Single().Body;
            Assert.Equal("claude-opus-5-5", body.GetProperty("model").GetString());
            Assert.Equal(64000, body.GetProperty("max_tokens").GetInt32());
            Assert.True(body.GetProperty("stream").GetBoolean());
            Assert.Equal("adaptive", body.GetProperty("thinking").GetProperty("type").GetString());
            Assert.Equal("high", body.GetProperty("output_config").GetProperty("effort").GetString());
            Assert.Equal("ephemeral", body.GetProperty("cache_control").GetProperty("type").GetString());
        }

        [Fact]
        [DisplayName("Fallbacks are requested with the server-side fallback beta by default")]
        public async Task GetResponseAsync_DefaultOptions_RequestsServerSideFallbacks()
        {
            var handler = new FakeAnthropicHandler().ThenStream(s_endTurn);
            using var client = ClaudeChatClientFactory.Create(handler.CreateClient(), null);

            await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]);

            var request = handler.Requests.Single();
            Assert.Equal("default", request.Body.GetProperty("fallbacks").GetString());
            Assert.Contains(ClaudeChatClient.FallbackBeta, request.Betas);
        }

        [Fact]
        [DisplayName("Turning fallbacks off sends neither the parameter nor the beta")]
        public async Task GetResponseAsync_FallbacksDisabled_SendsNoFallbacks()
        {
            var handler = new FakeAnthropicHandler().ThenStream(s_endTurn);
            using var client = ClaudeChatClientFactory.Create(handler.CreateClient(),
                new ClaudeChatOptions { EnableFallbacks = false });

            await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]);

            var request = handler.Requests.Single();
            Assert.False(request.Body.TryGetProperty("fallbacks", out _));
            Assert.DoesNotContain(ClaudeChatClient.FallbackBeta, request.Betas);
        }

        [Theory]
        [InlineData(ClaudeEffort.Low, "low")]
        [InlineData(ClaudeEffort.Medium, "medium")]
        [InlineData(ClaudeEffort.ExtraHigh, "xhigh")]
        [InlineData(ClaudeEffort.Max, "max")]
        [DisplayName("Each effort reaches the request as its API value")]
        public async Task GetResponseAsync_Effort_MapsToApiValue(ClaudeEffort effort, string expected)
        {
            var handler = new FakeAnthropicHandler().ThenStream(s_endTurn);
            using var client = ClaudeChatClientFactory.Create(handler.CreateClient(), new ClaudeChatOptions { Effort = effort });

            await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]);

            Assert.Equal(expected, handler.Requests.Single().Body.GetProperty("output_config").GetProperty("effort").GetString());
        }

        [Fact]
        [DisplayName("The model and output limit of a request's chat options override the client's settings")]
        public async Task GetResponseAsync_ChatOptions_OverrideModelAndLimit()
        {
            var handler = new FakeAnthropicHandler().ThenStream(s_endTurn);
            using var client = ClaudeChatClientFactory.Create(handler.CreateClient(), null);

            await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")],
                new ChatOptions { ModelId = "claude-sonnet-5-5", MaxOutputTokens = 2000 });

            var body = handler.Requests.Single().Body;
            Assert.Equal("claude-sonnet-5-5", body.GetProperty("model").GetString());
            Assert.Equal(2000, body.GetProperty("max_tokens").GetInt32());
        }

        [Fact]
        [DisplayName("Tools are sent strict, without eager input streaming, with automatic tool choice")]
        public async Task GetResponseAsync_Tools_AreStrict()
        {
            var handler = new FakeAnthropicHandler().ThenStream(s_endTurn);
            using var client = ClaudeChatClientFactory.Create(handler.CreateClient(), null);
            var tool = AIFunctionFactory.Create((string table, int? maxLength) => "ok", "upsert_field", "Adds a field.");

            await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")],
                new ChatOptions { Tools = [tool.AsDeclarationOnly()], ToolMode = ChatToolMode.Auto });

            var body = handler.Requests.Single().Body;
            var sent = Assert.Single(body.GetProperty("tools").EnumerateArray());
            Assert.Equal("upsert_field", sent.GetProperty("name").GetString());
            Assert.True(sent.GetProperty("strict").GetBoolean());
            Assert.False(sent.GetProperty("input_schema").GetProperty("additionalProperties").GetBoolean());
            Assert.False(sent.TryGetProperty("eager_input_streaming", out _));
            Assert.Equal("auto", body.GetProperty("tool_choice").GetProperty("type").GetString());
        }

        [Fact]
        [DisplayName("A PDF in the first user message becomes a document block that ends with a cache breakpoint")]
        public async Task GetResponseAsync_PdfDocument_IsCachedDocumentBlock()
        {
            var handler = new FakeAnthropicHandler().ThenStream(s_endTurn);
            using var client = ClaudeChatClientFactory.Create(handler.CreateClient(), null);
            var pdf = new DataContent(new byte[] { 0x25, 0x50, 0x44, 0x46 }, "application/pdf") { Name = "leave.pdf" };
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, "Rules."),
                new(ChatRole.User, [new TextContent("Draft a form."), pdf]),
            };

            await client.GetResponseAsync(messages);

            var content = handler.Requests.Single().Body.GetProperty("messages")[0].GetProperty("content");
            var document = content[content.GetArrayLength() - 1];
            Assert.Equal("document", document.GetProperty("type").GetString());
            Assert.Equal("application/pdf", document.GetProperty("source").GetProperty("media_type").GetString());
            Assert.Equal("ephemeral", document.GetProperty("cache_control").GetProperty("type").GetString());
            Assert.False(content[0].TryGetProperty("cache_control", out _));
        }

        [Fact]
        [DisplayName("Marking the cache breakpoint leaves the caller's messages and contents unchanged")]
        public async Task GetResponseAsync_CacheBreakpoint_DoesNotChangeCallerHistory()
        {
            var handler = new FakeAnthropicHandler().ThenStream(s_endTurn);
            using var client = ClaudeChatClientFactory.Create(handler.CreateClient(), null);
            var text = new TextContent("Draft a form.");
            var message = new ChatMessage(ChatRole.User, [text]);

            await client.GetResponseAsync([message]);

            Assert.Same(text, Assert.Single(message.Contents));
            Assert.Null(text.AdditionalProperties);
        }

        [Fact]
        [DisplayName("Thinking with its signature and a tool call come back in the response and are echoed unchanged")]
        public async Task GetResponseAsync_ThinkingAndToolCall_RoundTripIntoNextRequest()
        {
            var handler = new FakeAnthropicHandler()
                .ThenStream(Sse.MessageStart()
                    + Sse.Thinking(0, "Look at the fields.", "sig-1")
                    + Sse.ToolUse(1, "toolu_1", "get_draft", "{\"table\":\"master\"}")
                    + Sse.MessageEnd("tool_use"))
                .ThenStream(s_endTurn);
            using var client = ClaudeChatClientFactory.Create(handler.CreateClient(), null);
            var history = new List<ChatMessage> { new(ChatRole.User, "Draft a form.") };

            var first = await client.GetResponseAsync(history);
            var call = Assert.Single(first.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>());
            Assert.Equal("master", call.Arguments!["table"]!.ToString());
            Assert.Equal(ChatFinishReason.ToolCalls, first.FinishReason);

            history.AddRange(first.Messages);
            history.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent("toolu_1", "{}")]));
            await client.GetResponseAsync(history);

            var echoed = handler.Requests[1].Body.GetProperty("messages")[1].GetProperty("content");
            var thinking = echoed[0];
            Assert.Equal("thinking", thinking.GetProperty("type").GetString());
            Assert.Equal("Look at the fields.", thinking.GetProperty("thinking").GetString());
            Assert.Equal("sig-1", thinking.GetProperty("signature").GetString());
            Assert.Equal("tool_use", echoed[1].GetProperty("type").GetString());
            Assert.Equal("toolu_1", echoed[1].GetProperty("id").GetString());
        }

        [Fact]
        [DisplayName("A refusal throws with the refusal category instead of returning an empty answer")]
        public async Task GetResponseAsync_Refusal_ThrowsRefused()
        {
            var handler = new FakeAnthropicHandler().ThenStream(Sse.MessageStart() + Sse.MessageEnd("refusal", "cyber"));
            using var client = ClaudeChatClientFactory.Create(handler.CreateClient(), null);

            var ex = await Assert.ThrowsAsync<ClaudeChatException>(
                () => client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]));

            Assert.Equal(ClaudeFailureKind.Refused, ex.Kind);
            Assert.Equal("cyber", ex.RefusalCategory);
        }

        [Fact]
        [DisplayName("An answer cut off at the output limit throws instead of returning a partial tool call")]
        public async Task GetResponseAsync_MaxTokens_ThrowsOutputLimitReached()
        {
            var handler = new FakeAnthropicHandler().ThenStream(Sse.MessageStart()
                + Sse.ToolUse(0, "toolu_1", "upsert_field", "{\"table\":\"mas")
                + Sse.MessageEnd("max_tokens"));
            using var client = ClaudeChatClientFactory.Create(handler.CreateClient(), null);

            var ex = await Assert.ThrowsAsync<ClaudeChatException>(
                () => client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]));

            Assert.Equal(ClaudeFailureKind.OutputLimitReached, ex.Kind);
        }

        [Fact]
        [DisplayName("A rejected credential throws NotAuthenticated with a hint to sign in again")]
        public async Task GetResponseAsync_Unauthorized_ThrowsNotAuthenticated()
        {
            var handler = new FakeAnthropicHandler().ThenError(HttpStatusCode.Unauthorized, "authentication_error", "invalid x-api-key");
            using var client = ClaudeChatClientFactory.Create(handler.CreateClient(), null);

            var ex = await Assert.ThrowsAsync<ClaudeChatException>(
                () => client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]));

            Assert.Equal(ClaudeFailureKind.NotAuthenticated, ex.Kind);
            Assert.Contains("ant auth login", ex.Message, StringComparison.Ordinal);
            Assert.IsType<AnthropicUnauthorizedException>(ex.InnerException);
        }

        [Fact]
        [DisplayName("A rate limit that outlasts the SDK's retries reaches the caller as the SDK's exception")]
        public async Task GetResponseAsync_RateLimited_ThrowsSdkException()
        {
            var handler = new FakeAnthropicHandler().ThenError(HttpStatusCode.TooManyRequests, "rate_limit_error", "slow down");
            using var client = ClaudeChatClientFactory.Create(handler.CreateClient(), null);

            await Assert.ThrowsAsync<AnthropicRateLimitException>(
                () => client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]));
        }

        [Fact]
        [DisplayName("After a mid-output fallback the declined thinking and tool calls are dropped and the fallback block is kept")]
        public async Task GetResponseAsync_MidOutputFallback_DropsDeclinedAttempt()
        {
            var handler = new FakeAnthropicHandler()
                .ThenStream(Sse.MessageStart()
                    + Sse.Thinking(0, "Declined reasoning.", "sig-declined")
                    + Sse.Text(1, "Partial ")
                    + Sse.ToolUse(2, "toolu_declined", "get_draft", "{}")
                    + Sse.Fallback(3, "claude-opus-5-5", "claude-opus-5")
                    + Sse.Thinking(4, "Fallback reasoning.", "sig-fallback")
                    + Sse.Text(5, "Answer.")
                    + Sse.MessageEnd("end_turn"))
                .ThenStream(s_endTurn);
            using var client = ClaudeChatClientFactory.Create(handler.CreateClient(), null);
            var history = new List<ChatMessage> { new(ChatRole.User, "Hello") };

            var response = await client.GetResponseAsync(history);

            var contents = response.Messages.SelectMany(m => m.Contents).ToList();
            Assert.DoesNotContain(contents, c => c is FunctionCallContent);
            var reasoning = Assert.Single(contents.OfType<TextReasoningContent>());
            Assert.Equal("sig-fallback", reasoning.ProtectedData);

            history.AddRange(response.Messages);
            history.Add(new ChatMessage(ChatRole.User, "Next."));
            await client.GetResponseAsync(history);

            var echoed = handler.Requests[1].Body.GetProperty("messages")[1].GetProperty("content")
                .EnumerateArray().Select(block => block.GetProperty("type").GetString()).ToList();
            Assert.Equal(["text", "fallback", "thinking", "text"], echoed);
        }

        [Fact]
        [DisplayName("Disposing a chat client over a caller's SDK client leaves that client usable")]
        public async Task Dispose_CallerClient_IsNotDisposed()
        {
            var handler = new FakeAnthropicHandler().ThenStream(s_endTurn).ThenStream(s_endTurn);
            var sdkClient = handler.CreateClient();
            ClaudeChatClientFactory.Create(sdkClient, null).Dispose();

            using var second = ClaudeChatClientFactory.Create(sdkClient, null);
            var response = await second.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]);

            Assert.Equal("Done.", response.Text);
        }

        [Fact]
        [DisplayName("Invalid settings are refused when the client is created")]
        public void Create_InvalidOptions_Throws()
        {
            using var sdkClient = new AnthropicClient { ApiKey = "test-key" };

            Assert.Throws<ArgumentException>(() => ClaudeChatClientFactory.Create(sdkClient, new ClaudeChatOptions { Model = " " }));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ClaudeChatClientFactory.Create(sdkClient, new ClaudeChatOptions { MaxOutputTokens = 0 }));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ClaudeChatClientFactory.Create(sdkClient, new ClaudeChatOptions { Effort = (ClaudeEffort)99 }));
        }
    }
}
