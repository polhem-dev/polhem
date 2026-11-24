using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.AI;
using Polhem.Chat;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;
using Polhem.Definition.Drafting;
using Polhem.Definition.Drafting.UnitTests;
using Polhem.Tests.Shared;
using static Polhem.Definition.Drafting.UnitTests.ScriptedChatClient;

namespace Polhem.DefineEditor.UnitTests;

/// <summary>
/// The conversation of the drafting window: what the session reports, fed into the conversation the chat view shows, and
/// the cards that let the user answer the model's questions by clicking. The model is scripted; nothing is called.
/// </summary>
public sealed class FormDraftingChatTests
{
    private const string OneQuestion = """
        {"questions":[{"question":"Which ProgId should the form use?","options":[{"label":"LeaveRequest","description":""},{"label":"LeaveApplication","description":""}],"recommended":"LeaveRequest"}]}
        """;

    private const string TwoQuestions = """
        {"questions":[
          {"question":"Which ProgId?","options":[{"label":"LeaveRequest","description":""},{"label":"LeaveApplication","description":""}],"recommended":"LeaveApplication"},
          {"question":"Link the department?","options":[{"label":"Yes","description":""},{"label":"No","description":""}],"recommended":""}
        ]}
        """;

    private static readonly Regex s_placeholder = new(@"\{(\d+)[^}]*\}", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static FormDraftingViewModel CreateViewModel(ScriptedChatClient client, out FormDraftSession session)
    {
        var host = new FakeDraftingHost();
        session = new FormDraftSession(client, host);
        return new FormDraftingViewModel(session, host, Path.GetTempPath(), "leave.pdf", "test", _ => Task.FromResult(false));
    }

    [Fact]
    [DisplayName("A turn's tool calls show in the conversation before the reply, with the editor's titles; a refused one fails with the reason")]
    public async Task StartAsync_ToolCalls_ShowBeforeTheReply()
    {
        var client = new ScriptedChatClient()
            .Then(Call("1", "upsert_field", """{"table":"master","fieldName":"leave_type","caption":"Type"}"""),
                Call("2", "remove_field", """{"table":"master","fieldName":"missing"}"""))
            .Then(Text("Drafted."));
        var viewModel = CreateViewModel(client, out _);

        await viewModel.StartAsync([Text("a scanned leave request")]);

        Assert.Collection(viewModel.Conversation.Entries,
            entry => Assert.Contains("leave.pdf", Assert.IsType<UserPromptEntry>(entry).Text, StringComparison.Ordinal),
            entry =>
            {
                var call = Assert.IsType<ToolCallEntry>(entry);
                Assert.Equal(FormDraftingViewModel.DescribeTool("upsert_field", "leave_type"), call.Title);
                Assert.Equal(ToolCallStatus.Completed, call.Status);
            },
            entry =>
            {
                var call = Assert.IsType<ToolCallEntry>(entry);
                Assert.Equal(ToolCallStatus.Failed, call.Status);
                Assert.False(string.IsNullOrEmpty(call.ErrorMessage));
            },
            entry => Assert.Equal("Drafted.", Assert.IsType<AssistantReplyEntry>(entry).Text),
            entry =>
            {
                var summary = Assert.IsType<TurnSummaryEntry>(entry);
                Assert.Equal(TurnOutcome.Completed, summary.Outcome);
                Assert.Equal(2, summary.Calls);
            });
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    [DisplayName("A turn without tool calls shows no tool call")]
    public async Task StartAsync_ReplyOnly_ShowsNoToolCall()
    {
        var viewModel = CreateViewModel(new ScriptedChatClient().Then(Text("Hello.")), out _);

        await viewModel.StartAsync([Text("a scanned leave request")]);

        Assert.DoesNotContain(viewModel.Conversation.Entries, entry => entry is ToolCallEntry);
        Assert.Equal("Hello.", Assert.Single(viewModel.Conversation.Entries.OfType<AssistantReplyEntry>()).Text);
    }

    [Fact]
    [DisplayName("The questions the model asked show after its reply; clicking an option of a single question sends it and closes the card")]
    public async Task QuestionCard_SingleQuestion_ClickSendsTheAnswer()
    {
        var client = new ScriptedChatClient()
            .Then(Call("1", "ask_user", OneQuestion))
            .Then(Text("Please choose."))
            .Then(Text("Thanks."));
        var viewModel = CreateViewModel(client, out _);
        await viewModel.StartAsync([Text("a scanned leave request")]);

        var entries = viewModel.Conversation.Entries;
        var card = Assert.IsType<DraftQuestionCard>(entries[^2]);
        Assert.IsType<AssistantReplyEntry>(entries[^3]);
        Assert.IsType<TurnSummaryEntry>(entries[^1]);
        var question = Assert.Single(card.Questions);
        Assert.True(question.Choices[0].IsRecommended);
        question.Choices[1].SelectCommand.Execute(null);

        Assert.False(card.IsOpen);
        Assert.False(question.Choices[0].IsOpen);
        Assert.Equal("LeaveApplication", client.Requests[^1].Messages[^1].Text);
        Assert.Contains(viewModel.Conversation.Entries, entry => entry is UserPromptEntry { Text: "LeaveApplication" });
    }

    [Fact]
    [DisplayName("Several questions are answered together; the suggestions fill only the unanswered ones")]
    public async Task QuestionCard_SeveralQuestions_SendsAllAnswersTogether()
    {
        var client = new ScriptedChatClient()
            .Then(Call("1", "ask_user", TwoQuestions))
            .Then(Text("Please choose."))
            .Then(Text("Thanks."));
        var viewModel = CreateViewModel(client, out _);
        await viewModel.StartAsync([Text("a scanned leave request")]);
        var card = Assert.Single(viewModel.Conversation.Entries.OfType<DraftQuestionCard>());
        var requests = client.Requests.Count;

        card.Questions[0].Choices[0].SelectCommand.Execute(null);
        Assert.Equal(requests, client.Requests.Count);
        card.UseRecommendationsCommand.Execute(null);
        Assert.True(card.Questions[0].Choices[0].IsSelected);
        card.Questions[1].OtherText = "Only for managers";
        card.SubmitCommand.Execute(null);

        var sent = client.Requests[^1].Messages[^1].Text;
        Assert.Contains("1. Which ProgId? → LeaveRequest", sent, StringComparison.Ordinal);
        Assert.Contains("2. Link the department? → Only for managers", sent, StringComparison.Ordinal);
        Assert.False(card.IsOpen);
    }

    [Fact]
    [DisplayName("An unanswered question is marked as such in the answers")]
    public void ComposeAnswers_UnansweredQuestion_SaysSo()
    {
        var questions = new[]
        {
            new DraftQuestion("A?", [new DraftQuestionOption("Yes", ""), new DraftQuestionOption("No", "")], 0),
            new DraftQuestion("B?", [new DraftQuestionOption("Yes", ""), new DraftQuestionOption("No", "")], -1),
        };
        var card = new DraftQuestionCard(questions, _ => { });

        card.Questions[0].Choices[1].SelectCommand.Execute(null);

        Assert.EndsWith("2. B? → " + LocalizationService.Current["Drafting_NotAnswered"], card.ComposeAnswers(), StringComparison.Ordinal);
        Assert.True(card.SubmitCommand.CanExecute(null));
    }

    [Fact]
    [DisplayName("Typing a reply instead of clicking closes the open question card")]
    public async Task SendAsync_OpenQuestions_ClosesTheCard()
    {
        var client = new ScriptedChatClient()
            .Then(Call("1", "ask_user", OneQuestion))
            .Then(Text("Please choose."))
            .Then(Text("OK."));
        var viewModel = CreateViewModel(client, out _);
        await viewModel.StartAsync([Text("a scanned leave request")]);
        var card = Assert.Single(viewModel.Conversation.Entries.OfType<DraftQuestionCard>());

        await viewModel.SendAsync("Use LeaveForm instead.");

        Assert.False(card.IsOpen);
        Assert.Equal("Use LeaveForm instead.", client.Requests[^1].Messages[^1].Text);
    }

    [Fact]
    [DisplayName("A blank message, or one sent while a turn runs, sends nothing")]
    public async Task SendAsync_BlankOrBusy_SendsNothing()
    {
        var client = new ScriptedChatClient().Then(Text("Hello."));
        var viewModel = CreateViewModel(client, out _);

        await viewModel.SendAsync("   ");

        Assert.Empty(client.Requests);
        Assert.Empty(viewModel.Conversation.Entries);
    }

    [Fact]
    [DisplayName("Questions asked in a turn that then fails are not offered, and the failure is reported")]
    public async Task StartAsync_TurnFailsAfterQuestions_ShowsNoQuestionCard()
    {
        var client = new ScriptedChatClient()
            .Then(Call("1", "ask_user", OneQuestion))
            .Then((_, _) => throw new HttpRequestException("network down"));
        var viewModel = CreateViewModel(client, out _);

        await viewModel.StartAsync([Text("a scanned leave request")]);

        var entries = viewModel.Conversation.Entries;
        Assert.DoesNotContain(entries, entry => entry is DraftQuestionCard);
        Assert.Contains("network down", Assert.Single(entries.OfType<ErrorEntry>()).Message, StringComparison.Ordinal);
        Assert.Equal(TurnOutcome.Failed, Assert.IsType<TurnSummaryEntry>(entries[^1]).Outcome);
        Assert.Equal(ToolCallStatus.Completed, Assert.Single(entries.OfType<ToolCallEntry>()).Status);
    }

    [Fact]
    [DisplayName("Every tool of the drafting session has a title of its own, which takes the subject")]
    public void DescribeTool_EveryTool_HasItsOwnText()
    {
        var tools = new[]
        {
            "set_form_info", "upsert_table", "remove_table", "upsert_field", "remove_field", "search_progids",
            "get_progid_fields", "set_relation", "get_draft", "validate_draft", "mark_ready", "ask_user",
        };
        var other = FormDraftingViewModel.DescribeTool("no_such_tool", "x");

        Assert.Contains("no_such_tool", other, StringComparison.Ordinal);
        Assert.All(tools, tool => Assert.NotEqual(LocalizationService.Current["DraftingStep_Other"].Replace("{0}", tool, StringComparison.Ordinal),
            FormDraftingViewModel.DescribeTool(tool, "x")));
    }

    [Fact]
    [DisplayName("A streamed reply shows as one reply, which holds the whole text when the response ends")]
    public async Task StartAsync_StreamedReply_ShowsOneReply()
    {
        var client = new ScriptedChatClient { TextPieceLength = 5 }.Then(Text("Drafted **three** fields."));
        var viewModel = CreateViewModel(client, out _);

        await viewModel.StartAsync([Text("a scanned leave request")]);

        var reply = Assert.Single(viewModel.Conversation.Entries.OfType<AssistantReplyEntry>());
        Assert.Equal("Drafted **three** fields.", reply.Text);
        Assert.False(reply.IsStreaming);
    }

    [Fact]
    [DisplayName("A reply that breaks off when the turn fails keeps what arrived, and the failure is reported after it")]
    public async Task SendAsync_StreamBreaksOff_KeepsWhatArrivedAndReportsTheFailure()
    {
        var client = new ScriptedChatClient { TextPieceLength = 4 }
            .Then(Text("Reading."))
            .Then(Text("Removing the table"), FailHere("The connection dropped."));
        var viewModel = CreateViewModel(client, out _);
        await viewModel.StartAsync([Text("a scanned leave request")]);

        await viewModel.SendAsync("Remove the table.");

        var entries = viewModel.Conversation.Entries;
        var reply = entries.OfType<AssistantReplyEntry>().Last();
        Assert.StartsWith("Remo", reply.Text, StringComparison.Ordinal);
        Assert.False(reply.IsStreaming);
        Assert.Contains("The connection dropped.", Assert.IsType<ErrorEntry>(entries[^2]).Message, StringComparison.Ordinal);
        Assert.Equal(TurnOutcome.Failed, Assert.IsType<TurnSummaryEntry>(entries[^1]).Outcome);
    }

    [Fact]
    [DisplayName("A response's streamed reasoning shows once, before its reply, and the summary the session reports afterwards adds nothing")]
    public async Task StartAsync_StreamedReasoning_ShowsOnceBeforeTheReply()
    {
        var client = new ScriptedChatClient { TextPieceLength = 6 }
            .Then(new TextReasoningContent("Check the leave types first."), Text("Drafted."));
        var viewModel = CreateViewModel(client, out _);

        await viewModel.StartAsync([Text("a scanned leave request")]);

        Assert.Collection(viewModel.Conversation.Entries,
            entry => Assert.IsType<UserPromptEntry>(entry),
            entry =>
            {
                var reasoning = Assert.IsType<ReasoningEntry>(entry);
                Assert.Equal("Check the leave types first.", reasoning.Text);
                Assert.False(reasoning.IsStreaming);
            },
            entry => Assert.Equal("Drafted.", Assert.IsType<AssistantReplyEntry>(entry).Text),
            entry => Assert.IsType<TurnSummaryEntry>(entry));
    }

    [Fact]
    [DisplayName("A provider that streams nothing still shows the reasoning and the reply the session reports")]
    public async Task StartAsync_NothingStreamed_ShowsTheReportedReasoningAndReply()
    {
        var client = new ScriptedChatClient().Then((_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant,
            [new TextReasoningContent("Read the form."), new TextContent("Drafted.")])));
        var viewModel = CreateViewModel(client, out _);

        await viewModel.StartAsync([Text("a scanned leave request")]);

        Assert.Equal("Read the form.", Assert.Single(viewModel.Conversation.Entries.OfType<ReasoningEntry>()).Text);
        Assert.Equal("Drafted.", Assert.Single(viewModel.Conversation.Entries.OfType<AssistantReplyEntry>()).Text);
    }

    [Fact]
    [DisplayName("The turn's summary carries its input, cached and output tokens and the requests the session sent")]
    public async Task StartAsync_Turn_SummaryCarriesItsTokens()
    {
        var client = new ScriptedChatClient().Then((_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, "Drafted."))
        {
            Usage = new UsageDetails { InputTokenCount = 18240, OutputTokenCount = 2105, CachedInputTokenCount = 16000 },
        });
        var viewModel = CreateViewModel(client, out var session);

        await viewModel.StartAsync([Text("a scanned leave request")]);

        var usage = Assert.IsType<TurnSummaryEntry>(viewModel.Conversation.Entries[^1]).Usage!;
        Assert.Equal(18240, usage.InputTokenCount);
        Assert.Equal(2105, usage.OutputTokenCount);
        Assert.Equal(16000, usage.CachedInputTokenCount);
        Assert.Equal(1, session.LastTurnUsage.Calls);
        Assert.Equal(1, Assert.IsType<TurnSummaryEntry>(viewModel.Conversation.Entries[^1]).Calls);
    }

    [Fact]
    [DisplayName("A turn that fails while the model reasons ends its reasoning and reports the failure")]
    public async Task SendAsync_FailsWhileReasoning_EndsTheReasoning()
    {
        var client = new ScriptedChatClient { TextPieceLength = 4 }
            .Then(Text("Reading."))
            .Then(new TextReasoningContent("Remove the table and"), FailHere("The connection dropped."));
        var viewModel = CreateViewModel(client, out _);
        await viewModel.StartAsync([Text("a scanned leave request")]);

        await viewModel.SendAsync("Remove the table.");

        var entries = viewModel.Conversation.Entries;
        Assert.All(entries.OfType<ReasoningEntry>(), reasoning => Assert.False(reasoning.IsStreaming));
        Assert.Contains("The connection dropped.", Assert.Single(entries.OfType<ErrorEntry>()).Message, StringComparison.Ordinal);
    }

    [Fact]
    [DisplayName("The zh-TW strings have the same keys and placeholders as the English ones")]
    public void Strings_ZhTw_MatchEnglishKeysAndPlaceholders()
    {
        var folder = Path.Combine(RepoRoot.Find(), "tools", "DefineEditor", "Resources");
        var english = Load(Path.Combine(folder, "Strings.resx"));
        var chinese = Load(Path.Combine(folder, "Strings.zh-TW.resx"));

        var problems = english
            .Where(entry => !chinese.TryGetValue(entry.Key, out var zh) || Placeholders(zh) != Placeholders(entry.Value))
            .Select(entry => entry.Key)
            .Concat(chinese.Keys.Where(key => !english.ContainsKey(key)))
            .ToList();

        Assert.True(english.Count > 100, $"Only {english.Count} strings were read.");
        Assert.True(problems.Count == 0, "Keys or placeholders differ: " + string.Join(", ", problems));
    }

    private static Dictionary<string, string> Load(string path)
        => XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(data => (string)data.Attribute("name")!, data => (string?)data.Element("value") ?? string.Empty, StringComparer.Ordinal);

    private static string Placeholders(string text)
        => string.Join(",", s_placeholder.Matches(text).Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
}
