using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.AI;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;
using Polhem.Definition.Drafting;
using Polhem.Definition.Drafting.UnitTests;
using Polhem.Tests.Shared;
using static Polhem.Definition.Drafting.UnitTests.ScriptedChatClient;

namespace Polhem.DefineEditor.UnitTests;

/// <summary>
/// The conversation pane of the drafting window: the cards that show what the model is doing, and the cards that let
/// the user answer its questions by clicking. The model is scripted; nothing is called.
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
    [DisplayName("A turn's tool calls show as one activity card before the reply, collapsed and counted when the turn ends")]
    public async Task StartAsync_ToolCalls_ShowActivityCardBeforeTheReply()
    {
        var client = new ScriptedChatClient()
            .Then(Call("1", "upsert_field", """{"table":"master","fieldName":"leave_type","caption":"Type"}"""),
                Call("2", "remove_field", """{"table":"master","fieldName":"missing"}"""))
            .Then(Text("Drafted."));
        var viewModel = CreateViewModel(client, out _);

        await viewModel.StartAsync([Text("a scanned leave request")]);

        Assert.Collection(viewModel.Messages,
            item => Assert.True(Assert.IsType<DraftChatEntry>(item).IsUser),
            item =>
            {
                var card = Assert.IsType<DraftActivityCard>(item);
                Assert.False(card.IsRunning);
                Assert.False(card.IsExpanded);
                Assert.Equal(LocalizationService.Current["DraftingStep_Done"].Replace("{0}", "2", StringComparison.Ordinal), card.Title);
                Assert.Equal(2, card.Steps.Count);
                Assert.Contains("leave_type", card.Steps[0].Text, StringComparison.Ordinal);
                Assert.True(card.Steps[1].IsError);
                Assert.True(card.Steps[1].HasDetail);
            },
            item => Assert.Equal("Drafted.", Assert.IsType<DraftChatEntry>(item).Text));
    }

    [Fact]
    [DisplayName("A reply of the model renders as Markdown; the user's message and an error show as written")]
    public async Task StartAsync_Reply_RendersAsMarkdown()
    {
        var viewModel = CreateViewModel(new ScriptedChatClient().Then(Text("Drafted **three** fields.")), out _);

        await viewModel.StartAsync([Text("a scanned leave request")]);

        var entries = viewModel.Messages.OfType<DraftChatEntry>().ToList();
        Assert.False(entries[0].RendersMarkdown);
        Assert.True(entries[1].RendersMarkdown);
        Assert.False(new DraftChatEntry(isUser: false, "The request failed.", isError: true).RendersMarkdown);
    }

    [Fact]
    [DisplayName("A turn without tool calls leaves no activity card behind")]
    public async Task StartAsync_ReplyOnly_LeavesNoActivityCard()
    {
        var viewModel = CreateViewModel(new ScriptedChatClient().Then(Text("Hello.")), out _);

        await viewModel.StartAsync([Text("a scanned leave request")]);

        Assert.DoesNotContain(viewModel.Messages, item => item is DraftActivityCard);
    }

    [Fact]
    [DisplayName("Clicking an option of a single question sends it as the user's message and closes the card")]
    public async Task QuestionCard_SingleQuestion_ClickSendsTheAnswer()
    {
        var client = new ScriptedChatClient()
            .Then(Call("1", "ask_user", OneQuestion))
            .Then(Text("Please choose."))
            .Then(Text("Thanks."));
        var viewModel = CreateViewModel(client, out _);
        await viewModel.StartAsync([Text("a scanned leave request")]);

        var card = Assert.IsType<DraftQuestionCard>(viewModel.Messages[^1]);
        var question = Assert.Single(card.Questions);
        Assert.True(question.Choices[0].IsRecommended);
        question.Choices[1].SelectCommand.Execute(null);

        Assert.False(card.IsOpen);
        Assert.False(question.Choices[0].IsOpen);
        Assert.Equal("LeaveApplication", client.Requests[^1].Messages[^1].Text);
        Assert.Contains(viewModel.Messages, item => item is DraftChatEntry { IsUser: true, Text: "LeaveApplication" });
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
        var card = Assert.IsType<DraftQuestionCard>(viewModel.Messages[^1]);
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
    public async Task SendCommand_OpenQuestions_ClosesTheCard()
    {
        var client = new ScriptedChatClient()
            .Then(Call("1", "ask_user", OneQuestion))
            .Then(Text("Please choose."))
            .Then(Text("OK."));
        var viewModel = CreateViewModel(client, out _);
        await viewModel.StartAsync([Text("a scanned leave request")]);
        var card = Assert.IsType<DraftQuestionCard>(viewModel.Messages[^1]);

        viewModel.InputText = "Use LeaveForm instead.";
        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.False(card.IsOpen);
    }

    [Fact]
    [DisplayName("Questions asked in a turn that then fails are not offered")]
    public async Task StartAsync_TurnFailsAfterQuestions_ShowsNoQuestionCard()
    {
        var client = new ScriptedChatClient()
            .Then(Call("1", "ask_user", OneQuestion))
            .Then((_, _) => throw new HttpRequestException("network down"));
        var viewModel = CreateViewModel(client, out _);

        await viewModel.StartAsync([Text("a scanned leave request")]);

        Assert.DoesNotContain(viewModel.Messages, item => item is DraftQuestionCard);
        Assert.True(Assert.IsType<DraftChatEntry>(viewModel.Messages[^1]).IsError);
        var card = Assert.Single(viewModel.Messages.OfType<DraftActivityCard>());
        Assert.Equal(LocalizationService.Current["DraftingStep_StoppedOne"], card.Title);
    }

    [Fact]
    [DisplayName("Every tool of the drafting session has a step description, which takes the subject")]
    public void Describe_EveryTool_HasItsOwnText()
    {
        var tools = new[]
        {
            "set_form_info", "upsert_table", "remove_table", "upsert_field", "remove_field", "search_progids",
            "get_progid_fields", "set_relation", "get_draft", "validate_draft", "mark_ready", "ask_user",
        };
        var other = DraftActivityCard.Describe("no_such_tool", "x");

        Assert.Contains("no_such_tool", other, StringComparison.Ordinal);
        Assert.All(tools, tool => Assert.NotEqual(LocalizationService.Current["DraftingStep_Other"].Replace("{0}", tool, StringComparison.Ordinal),
            DraftActivityCard.Describe(tool, "x")));
    }

    [Fact]
    [DisplayName("A streamed reply shows in one bubble as it arrives, and the whole reply replaces it when the response ends")]
    public async Task StartAsync_StreamedReply_GrowsOneBubbleThenCompletes()
    {
        var client = new ScriptedChatClient { TextPieceLength = 5 }.Then(Text("Drafted **three** fields."));
        var viewModel = CreateViewModel(client, out var session);
        DraftChatEntry? first = null;
        var firstText = string.Empty;
        session.AssistantMessageDelta += (_, _) =>
        {
            if (first is not null) return;
            first = viewModel.Messages.OfType<DraftChatEntry>().Last();
            firstText = first.Text;
        };

        await viewModel.StartAsync([Text("a scanned leave request")]);

        Assert.NotNull(first);
        Assert.Equal("Draft", firstText);
        var reply = Assert.Single(viewModel.Messages.OfType<DraftChatEntry>(), entry => entry.IsAssistant);
        Assert.Same(first, reply);
        Assert.Equal("Drafted **three** fields.", reply.Text);
        Assert.False(reply.IsStreaming);
        Assert.True(reply.CanCopy);
    }

    [Fact]
    [DisplayName("A reply that breaks off when the turn fails is removed, and the failure is reported")]
    public async Task SendAsync_StreamBreaksOff_RemovesTheUnfinishedReply()
    {
        var client = new ScriptedChatClient { TextPieceLength = 4 }
            .Then(Text("Reading."))
            .Then(Text("Removing the table"), FailHere("The connection dropped."));
        var viewModel = CreateViewModel(client, out var session);
        await viewModel.StartAsync([Text("a scanned leave request")]);
        var sawStreaming = false;
        session.AssistantMessageDelta += (_, _) => sawStreaming |= viewModel.Messages.OfType<DraftChatEntry>().Any(e => e.IsStreaming);
        viewModel.InputText = "Remove the table.";

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.True(sawStreaming);
        var entries = viewModel.Messages.OfType<DraftChatEntry>().ToList();
        Assert.DoesNotContain(entries, entry => entry.IsStreaming || entry.Text.StartsWith("Removing", StringComparison.Ordinal));
        Assert.True(entries[^1].IsError);
        Assert.Contains("The connection dropped.", entries[^1].Text, StringComparison.Ordinal);
    }

    [Fact]
    [DisplayName("A response's reasoning goes into the activity card above its streamed reply, not into a new card below it")]
    public async Task StartAsync_ReasoningAndReply_ReasoningStaysInTheCardAbove()
    {
        var client = new ScriptedChatClient { TextPieceLength = 3 }
            .Then(new TextReasoningContent("Look at the form."), Text("Drafted."));
        var viewModel = CreateViewModel(client, out _);

        await viewModel.StartAsync([Text("a scanned leave request")]);

        Assert.Collection(viewModel.Messages,
            item => Assert.True(Assert.IsType<DraftChatEntry>(item).IsUser),
            item => Assert.True(Assert.Single(Assert.IsType<DraftActivityCard>(item).Steps).IsReasoning),
            item => Assert.Equal("Drafted.", Assert.IsType<DraftChatEntry>(item).Text));
    }

    [Fact]
    [DisplayName("After a turn the usage line shows its calls, tokens and seconds")]
    public async Task StartAsync_Turn_ShowsItsUsage()
    {
        var client = new ScriptedChatClient().Then((_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, "Drafted."))
        {
            Usage = new UsageDetails { InputTokenCount = 18240, OutputTokenCount = 2105, CachedInputTokenCount = 16000 },
        });
        var viewModel = CreateViewModel(client, out var session);
        Assert.False(viewModel.HasUsage);

        await viewModel.StartAsync([Text("a scanned leave request")]);

        Assert.True(viewModel.HasUsage);
        Assert.Contains("18,240", viewModel.UsageText, StringComparison.Ordinal);
        Assert.Contains("16,000", viewModel.UsageText, StringComparison.Ordinal);
        Assert.Contains("2,105", viewModel.UsageText, StringComparison.Ordinal);
        Assert.Equal(1, session.LastTurnUsage.Calls);
    }

    [Fact]
    [DisplayName("The usage line leaves out the cache when nothing was cached, and says so when no tokens were reported")]
    public void DescribeUsage_CachedOrNone()
    {
        var cached = FormDraftingViewModel.DescribeUsage(new FormDraftUsage(2, 18240, 2105, 16000, TimeSpan.Zero), TimeSpan.FromSeconds(30));
        var uncached = FormDraftingViewModel.DescribeUsage(new FormDraftUsage(2, 18240, 2105, 0, TimeSpan.Zero), TimeSpan.FromSeconds(30));
        var none = FormDraftingViewModel.DescribeUsage(new FormDraftUsage(3, 0, 0, 0, TimeSpan.Zero), TimeSpan.FromSeconds(5));

        Assert.Contains("16,000", cached, StringComparison.Ordinal);
        Assert.Contains("18,240", uncached, StringComparison.Ordinal);
        Assert.DoesNotContain("16,000", uncached, StringComparison.Ordinal);
        Assert.Equal(LocalizationService.Current["Drafting_UsageNoTokens"].Replace("{0}", "3", StringComparison.Ordinal)
            .Replace("{1}", "5", StringComparison.Ordinal), none);
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
