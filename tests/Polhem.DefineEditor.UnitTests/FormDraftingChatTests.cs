using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Xml.Linq;
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
