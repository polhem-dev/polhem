using System.ComponentModel;
using Microsoft.Extensions.AI;
using static Polhem.Definition.Drafting.UnitTests.ScriptedChatClient;

namespace Polhem.Definition.Drafting.UnitTests
{
    public class FormDraftSessionActivityTests
    {
        private static readonly AIContent[] s_document = [new DataContent(new byte[] { 1, 2, 3 }, "application/pdf")];

        private const string TwoQuestions = """
            {"questions":[
              {"question":"Which ProgId should the form use?","options":[{"label":"LeaveRequest","description":""},{"label":"LeaveApplication","description":"Matches the title"}],"recommended":"LeaveRequest"},
              {"question":"Should the department pick a record from Department?","options":[{"label":"Yes","description":""},{"label":"No","description":""}],"recommended":""}
            ]}
            """;

        [Fact]
        [DisplayName("Activity reports thinking before each request and each tool's start and end with its subject")]
        public async Task StartAsync_ToolCalls_RaiseActivityInOrder()
        {
            var client = new ScriptedChatClient()
                .Then(Call("1", "upsert_table", """{"table":"LeaveDetail","displayName":"Days"}"""),
                    Call("2", "upsert_field", """{"table":"master","fieldName":"leave_type","caption":"Type"}"""),
                    Call("3", "upsert_field", """{"table":"LeaveDetail","fieldName":"day","caption":"Day"}"""),
                    Call("4", "search_progids", """{"query":"employee 員工"}"""))
                .Then(Text("Done."));
            var session = new FormDraftSession(client, new FakeDraftingHost());
            var steps = new List<DraftActivityEventArgs>();
            session.Activity += (_, e) => steps.Add(e);

            await session.StartAsync(s_document);

            Assert.Equal(
            [
                (DraftActivityKind.Thinking, "", ""),
                (DraftActivityKind.ToolStarted, "upsert_table", "LeaveDetail"),
                (DraftActivityKind.ToolCompleted, "upsert_table", "LeaveDetail"),
                (DraftActivityKind.ToolStarted, "upsert_field", "leave_type"),
                (DraftActivityKind.ToolCompleted, "upsert_field", "leave_type"),
                (DraftActivityKind.ToolStarted, "upsert_field", "LeaveDetail.day"),
                (DraftActivityKind.ToolCompleted, "upsert_field", "LeaveDetail.day"),
                (DraftActivityKind.ToolStarted, "search_progids", "employee 員工"),
                (DraftActivityKind.ToolCompleted, "search_progids", "employee 員工"),
                (DraftActivityKind.Thinking, "", ""),
            ], steps.Select(step => (step.Kind, step.ToolName, step.Subject)));
        }

        [Fact]
        [DisplayName("A refused tool call is reported as ToolRefused with the reason the model gets")]
        public async Task StartAsync_RefusedTool_RaisesToolRefusedWithReason()
        {
            var client = new ScriptedChatClient()
                .Then(Call("1", "remove_field", """{"table":"master","fieldName":"missing"}"""),
                    Call("2", "no_such_tool"))
                .Then(Text("Sorry."));
            var session = new FormDraftSession(client, new FakeDraftingHost());
            var refused = new List<DraftActivityEventArgs>();
            session.Activity += (_, e) =>
            {
                if (e.Kind == DraftActivityKind.ToolRefused) refused.Add(e);
            };

            await session.StartAsync(s_document);

            Assert.Equal(["remove_field", "no_such_tool"], refused.Select(e => e.ToolName));
            Assert.Equal("missing", refused[0].Subject);
            var results = client.Requests[1].Messages[^1].Contents.OfType<FunctionResultContent>().ToList();
            Assert.Equal("Error: " + refused[0].Detail, results[0].Result);
            Assert.Contains("There is no tool named 'no_such_tool'", refused[1].Detail, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A reasoning summary in the response is reported as Reasoning with its text")]
        public async Task StartAsync_ReasoningInResponse_RaisesReasoning()
        {
            var client = new ScriptedChatClient()
                .Then(new TextReasoningContent("  The document has a table of days.  "), new TextReasoningContent(""),
                    Text("Drafted."));
            var session = new FormDraftSession(client, new FakeDraftingHost());
            var reasoning = new List<string>();
            session.Activity += (_, e) =>
            {
                if (e.Kind == DraftActivityKind.Reasoning) reasoning.Add(e.Detail);
            };

            await session.StartAsync(s_document);

            Assert.Equal(["The document has a table of days."], reasoning);
        }

        [Fact]
        [DisplayName("ask_user raises QuestionsAsked with the options and the recommended answer, then the turn ends without tools")]
        public async Task AskUser_Questions_RaiseQuestionsAskedAndEndTheTurn()
        {
            var client = new ScriptedChatClient()
                .Then(Call("1", "ask_user", TwoQuestions))
                .Then(Text("Please answer the questions above."));
            var session = new FormDraftSession(client, new FakeDraftingHost());
            DraftQuestionsEventArgs? asked = null;
            var replies = new List<string>();
            session.QuestionsAsked += (_, e) => asked = e;
            session.AssistantMessage += (_, e) => replies.Add(e.Text);

            await session.StartAsync(s_document);

            Assert.NotNull(asked);
            Assert.Equal(2, asked.Questions.Count);
            var first = asked.Questions[0];
            Assert.Equal("Which ProgId should the form use?", first.Text);
            Assert.Equal(["LeaveRequest", "LeaveApplication"], first.Options.Select(o => o.Label));
            Assert.Equal("Matches the title", first.Options[1].Description);
            Assert.Equal(0, first.RecommendedIndex);
            Assert.Equal(-1, asked.Questions[1].RecommendedIndex);
            Assert.Equal(ChatToolMode.None, client.Requests[1].Options?.ToolMode);
            var result = client.Requests[1].Messages[^1].Contents.OfType<FunctionResultContent>().Single().Result?.ToString();
            Assert.Contains("answers arrive in their next message", result, StringComparison.Ordinal);
            Assert.Contains("says what you changed in this turn", result, StringComparison.Ordinal);
            Assert.Equal(["Please answer the questions above."], replies);
        }

        [Fact]
        [DisplayName("The instructions ask the reply that ends a turn with questions to say first what was changed")]
        public void Instructions_AfterAskUser_ReplySaysWhatChanged()
        {
            var session = new FormDraftSession(new ScriptedChatClient(), new FakeDraftingHost());

            Assert.Contains("first what you changed in this turn", session.History[0].Text, StringComparison.Ordinal);
            Assert.DoesNotContain("end the turn with one short sentence", session.History[0].Text, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A tool the model calls after asking the user is not run")]
        public async Task AskUser_ToolCallAfterQuestions_IsNotRun()
        {
            var client = new ScriptedChatClient()
                .Then(Call("1", "ask_user", TwoQuestions))
                .Then(Call("2", "set_form_info", """{"progId":"LeaveRequest"}"""));
            var session = new FormDraftSession(client, new FakeDraftingHost());

            await session.StartAsync(s_document);

            Assert.Equal("", session.Draft.ProgId);
            var result = Assert.IsType<FunctionResultContent>(session.History[^1].Contents.Single());
            Assert.Contains("you asked the user questions", result.Result?.ToString(), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("""{"questions":[]}""", "at least one question")]
        [InlineData("""{"questions":[{"question":"Q?","options":[{"label":"Yes","description":""}],"recommended":""}]}""", "2 to 5 options")]
        [InlineData("""{"questions":[{"question":"Q?","options":[{"label":"Yes","description":""},{"label":"yes","description":""}],"recommended":""}]}""", "same label")]
        [InlineData("""{"questions":[{"question":"Q?","options":[{"label":"Yes","description":""},{"label":"No","description":""}],"recommended":"Maybe"}]}""", "is not the label")]
        [InlineData("""{"questions":[{"question":" ","options":[{"label":"Yes","description":""},{"label":"No","description":""}],"recommended":""}]}""", "has no text")]
        [DisplayName("ask_user refuses questions that cannot be shown as choices, and raises nothing")]
        public async Task AskUser_InvalidQuestions_AreRefused(string arguments, string expected)
        {
            var client = new ScriptedChatClient()
                .Then(Call("1", "ask_user", arguments))
                .Then(Text("Let me try again."));
            var session = new FormDraftSession(client, new FakeDraftingHost());
            var raised = false;
            session.QuestionsAsked += (_, _) => raised = true;

            await session.StartAsync(s_document);

            Assert.False(raised);
            var result = client.LastResults().Single().Result?.ToString();
            Assert.StartsWith("Error:", result, StringComparison.Ordinal);
            Assert.Contains(expected, result, StringComparison.Ordinal);
            Assert.NotEqual(ChatToolMode.None, client.Requests[1].Options?.ToolMode);
        }

        [Fact]
        [DisplayName("The instructions ask for plain replies unless the host renders Markdown")]
        public void Instructions_MarkdownReplies_ChangeTheReplyFormat()
        {
            var plain = new FormDraftSession(new ScriptedChatClient(), new FakeDraftingHost());
            var markdown = new FormDraftSession(new ScriptedChatClient(), new FakeDraftingHost(),
                new FormDraftingOptions { MarkdownReplies = true });

            Assert.Contains("do not use Markdown symbols", plain.History[0].Text, StringComparison.Ordinal);
            Assert.Contains("renders Markdown", markdown.History[0].Text, StringComparison.Ordinal);
            Assert.DoesNotContain("do not use Markdown symbols", markdown.History[0].Text, StringComparison.Ordinal);
        }
    }
}
