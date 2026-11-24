using System.ComponentModel;
using Microsoft.Extensions.AI;
using static Polhem.Definition.Drafting.UnitTests.ScriptedChatClient;

namespace Polhem.Definition.Drafting.UnitTests
{
    public class FormDraftSessionStreamingTests
    {
        private static readonly AIContent[] s_document = [new DataContent(new byte[] { 1, 2, 3 }, "application/pdf")];

        [Fact]
        [DisplayName("A reply arrives in pieces before AssistantMessage, and the pieces of each response join into its reply")]
        public async Task StartAsync_StreamedReply_RaisesPiecesThenTheWholeReply()
        {
            var client = new ScriptedChatClient { TextPieceLength = 4 }
                .Then(Text("Adding a table."), Call("1", "upsert_table", """{"table":"master"}"""))
                .Then(Text("I drafted the form."));
            var session = new FormDraftSession(client, new FakeDraftingHost());
            var events = new List<string>();
            var pieces = new System.Text.StringBuilder();
            session.AssistantMessageDelta += (_, e) => { events.Add("delta"); pieces.Append(e.Text); };
            session.AssistantMessage += (_, e) =>
            {
                events.Add("message");
                Assert.Equal(e.Text, pieces.ToString());
                pieces.Clear();
            };

            await session.StartAsync(s_document);

            Assert.Equal(["delta", "delta", "delta", "delta", "message", "delta", "delta", "delta", "delta", "delta", "message"], events);
        }

        [Fact]
        [DisplayName("Reasoning arrives in pieces after Thinking and before the reply, and joins into the summary Activity reports when the response ends")]
        public async Task StartAsync_StreamedReasoning_RaisesPiecesThenTheWholeSummary()
        {
            var client = new ScriptedChatClient { TextPieceLength = 5 }
                .Then(new TextReasoningContent("Read the form first.") { ProtectedData = "sig" }, Text("Drafted."));
            var session = new FormDraftSession(client, new FakeDraftingHost());
            var events = new List<string>();
            var pieces = new System.Text.StringBuilder();
            string? summary = null;
            session.Activity += (_, e) =>
            {
                events.Add(e.Kind.ToString());
                if (e.Kind == DraftActivityKind.Reasoning) summary = e.Detail;
            };
            session.ReasoningDelta += (_, e) => { events.Add("reasoning"); pieces.Append(e.Text); };
            session.AssistantMessageDelta += (_, _) => events.Add("reply");

            await session.StartAsync(s_document);

            Assert.Equal("Thinking", events[0]);
            Assert.Equal(4, events.Count(e => e == "reasoning"));
            Assert.True(events.LastIndexOf("reasoning") < events.IndexOf("reply"));
            Assert.True(events.IndexOf("reply") < events.IndexOf("Reasoning"));
            Assert.Equal("Read the form first.", pieces.ToString());
            Assert.Equal(pieces.ToString(), summary);
        }

        [Fact]
        [DisplayName("A turn that fails while a reply streams is removed as a whole, after its pieces were raised")]
        public async Task SendAsync_FailureAfterPieces_RestoresSessionState()
        {
            var client = new ScriptedChatClient { TextPieceLength = 3 }
                .Then(Call("1", "upsert_table", """{"table":"master"}"""))
                .Then(Text("Done."))
                .Then(Text("Removing the table now."), Call("2", "remove_table", """{"table":"master"}"""))
                .Then((_, _) => throw new InvalidOperationException("The model went away."));
            var session = new FormDraftSession(client, new FakeDraftingHost());
            await session.StartAsync(s_document);
            var historyBefore = session.History.ToList();
            var pieces = 0;
            var replies = 0;
            session.AssistantMessageDelta += (_, _) => pieces++;
            session.AssistantMessage += (_, _) => replies++;

            await Assert.ThrowsAsync<InvalidOperationException>(() => session.SendAsync("Remove the table."));

            Assert.True(pieces > 0);
            Assert.Equal(1, replies);
            Assert.Equal(historyBefore, session.History);
            Assert.Single(session.Draft.Tables);
        }

        [Fact]
        [DisplayName("UsageChanged counts each call when it begins and adds the tokens it reported when it returns")]
        public async Task StartAsync_ToolRounds_AccumulateUsage()
        {
            var client = new ScriptedChatClient()
                .Then((_, _) => WithUsage(new ChatResponse(new ChatMessage(ChatRole.Assistant, [Call("1", "upsert_table", """{"table":"master"}""")])),
                    input: 1200, output: 30, cached: 1000))
                .Then((_, _) => WithUsage(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Done.")), input: 1300, output: 20, cached: 1100));
            var session = new FormDraftSession(client, new FakeDraftingHost());
            var usages = new List<FormDraftUsage>();
            session.UsageChanged += (_, e) => usages.Add(e.Usage);

            await session.StartAsync(s_document);

            Assert.Equal([1, 1, 2, 2], usages.Select(u => u.Calls));
            Assert.Equal([0L, 1200L, 1200L, 2500L], usages.Select(u => u.InputTokens));
            Assert.Equal([0L, 30L, 30L, 50L], usages.Select(u => u.OutputTokens));
            Assert.Equal([0L, 1000L, 1000L, 2100L], usages.Select(u => u.CachedInputTokens));
            Assert.Equal(new FormDraftUsage(2, 2500, 50, 2100, TimeSpan.Zero), session.LastTurnUsage with { Elapsed = TimeSpan.Zero });
        }

        [Fact]
        [DisplayName("Each turn counts from zero, and a failed turn still reports what it spent")]
        public async Task SendAsync_FailedTurn_KeepsItsUsage()
        {
            var client = new ScriptedChatClient()
                .Then((_, _) => WithUsage(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Reading.")), input: 500, output: 10, cached: 0))
                .Then((_, _) => WithUsage(new ChatResponse(new ChatMessage(ChatRole.Assistant, [Call("1", "upsert_table", """{"table":"master"}""")])),
                    input: 600, output: 15, cached: 400))
                .Then((_, _) => throw new InvalidOperationException("The model went away."));
            var session = new FormDraftSession(client, new FakeDraftingHost());
            Assert.Same(FormDraftUsage.None, session.LastTurnUsage);
            await session.StartAsync(s_document);
            Assert.Equal(500, session.LastTurnUsage.InputTokens);

            await Assert.ThrowsAsync<InvalidOperationException>(() => session.SendAsync("Add a table."));

            Assert.Equal(2, session.LastTurnUsage.Calls);
            Assert.Equal(600, session.LastTurnUsage.InputTokens);
            Assert.Equal(15, session.LastTurnUsage.OutputTokens);
            Assert.Equal(400, session.LastTurnUsage.CachedInputTokens);
        }

        [Fact]
        [DisplayName("A provider that reports no usage leaves the token counts at zero")]
        public async Task StartAsync_NoUsageReported_CountsCallsOnly()
        {
            var client = new ScriptedChatClient().Then(Text("Reading."));
            var session = new FormDraftSession(client, new FakeDraftingHost());

            await session.StartAsync(s_document);

            Assert.Equal(1, session.LastTurnUsage.Calls);
            Assert.Equal(0, session.LastTurnUsage.InputTokens);
            Assert.Equal(0, session.LastTurnUsage.OutputTokens);
        }

        private static ChatResponse WithUsage(ChatResponse response, long input, long output, long cached)
        {
            response.Usage = new UsageDetails { InputTokenCount = input, OutputTokenCount = output, CachedInputTokenCount = cached };
            return response;
        }
    }
}
