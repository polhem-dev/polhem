using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;
using Polhem.Definition.Drafting.Rules;
using Polhem.Definition.Drafting.UnitTests;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Expressions;
using static Polhem.Definition.Drafting.UnitTests.ScriptedChatClient;

namespace Polhem.DefineEditor.UnitTests;

/// <summary>
/// The rule assistant of the expression editor: drafting and explaining a rule over a scripted model, applying the
/// draft to the rule, and writing the message translations into the form's Language files. Everything is written to an
/// isolated temporary folder; no real model is called.
/// </summary>
public sealed class RuleAssistantTests : IDisposable
{
    private const string RuleId = "approver_rule";

    private const string Draft = """
        {"ruleId":"amount_needs_approver","targetTable":"","when":"amount > 100000","condition":"!IsBlank(approver_id)",
         "message":"An amount over 100,000 needs an approver.","messageTranslations":[{"language":"zh-TW","text":"金額超過 10 萬時必須填核准人。"}],
         "explanation":"金額超過 10 萬的訂單必須填核准人。",
         "samples":[
           {"description":"金額 20 萬、沒有核准人","values":[{"field":"amount","value":"200000"},{"field":"approver_id","value":""}],"detailRows":[],"expected":"Blocked"},
           {"description":"金額 20 萬、有核准人","values":[{"field":"amount","value":"200000"},{"field":"approver_id","value":"E01"}],"detailRows":[],"expected":"Passes"},
           {"description":"金額 5 萬","values":[{"field":"amount","value":"50000"},{"field":"approver_id","value":""}],"detailRows":[],"expected":"Skipped"}]}
        """;

    private const string Explanation = """
        {"explanation":"核准人一定要填。",
         "samples":[
           {"description":"沒有核准人","values":[{"field":"approver_id","value":""}],"detailRows":[],"expected":"Blocked"},
           {"description":"有核准人","values":[{"field":"approver_id","value":"E01"}],"detailRows":[],"expected":"Passes"}]}
        """;

    private static readonly DraftingCredential s_signedIn = new(true, "test profile", []);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "polhem-rule-assistant-" + Guid.NewGuid().ToString("N"));
    private readonly string _define;
    private readonly string _zhTwPath;
    private readonly SolutionContext _solution;

    public RuleAssistantTests()
    {
        _define = Path.Combine(_root, "Define");
        var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
        var master = schema.Tables!.Add("Order", "Order");
        master.Fields!.Add(new FormField("amount", "Amount", FieldDbType.Currency));
        master.Fields!.Add(new FormField("approver_id", "Approver", FieldDbType.String));
        schema.Rules!.Add(new FormRule(RuleId, "true", "Old message."));
        var formSchemaPath = Path.Combine(_define, "FormSchema", "Order.FormSchema.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(formSchemaPath)!);
        XmlCodec.SerializeToFile(schema, formSchemaPath);

        _zhTwPath = WriteLanguage(Path.Combine(_define, "Language", "zh-TW"), "Order", "zh-TW");
        var paths = new[]
        {
            _zhTwPath,
            WriteLanguage(Path.Combine(_define, "Language", "en-US"), "Order", "en-US"),
            WriteLanguage(Path.Combine(_define, "Language", "ja-JP"), "Customer", "ja-JP"),
            WriteLanguage(Path.Combine(_root, "Customize", "demo", "Language", "ja-JP"), "Order", "ja-JP"),
        };
        _solution = new SolutionContext(["Order"])
        {
            DefinePath = _define,
            FormSchemaPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Order"] = formSchemaPath },
            LanguagePaths = paths,
        };
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    private static string WriteLanguage(string folder, string progId, string language)
    {
        Directory.CreateDirectory(folder);
        var resource = new LanguageResource { Namespace = progId, Lang = language };
        resource.Items.Add(FormSchemaLocalizer.SchemaDisplayNameKey, progId);
        var path = Path.Combine(folder, progId + ".Language.xml");
        File.WriteAllText(path, "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<!-- Kept on every write. -->\n"
            + XmlCodec.Serialize(resource).Split('\n', 2)[1] + "\n");
        return path;
    }

    private FormSchemaDocumentViewModel Load(ScriptedChatClient client, DraftingCredential? credential = null)
    {
        var document = FormSchemaDocumentViewModel.Load(_solution.FormSchemaPaths["Order"], _solution);
        document.RuleDraftingChatClientFactory = () => client;
        document.RuleDraftingCredentialProvider = () => credential ?? s_signedIn;
        return document;
    }

    private static PropertyDescriptor Property(object component, string name) => TypeDescriptor.GetProperties(component)[name]!;

    private static (ExpressionEditorViewModel Editor, RuleAssistantViewModel Assistant, PropertyDescriptor Property) Open(
        FormSchemaDocumentViewModel document)
    {
        var rule = document.Schema.Rules![RuleId];
        var property = Property(rule, nameof(FormRule.Condition));
        var editor = document.CreateExpressionEditor(property, rule)!;
        var assistant = document.CreateRuleAssistant(editor, rule, property);
        editor.RuleAssistant = assistant;
        return (editor, assistant, property);
    }

    [Fact]
    [DisplayName("The message is translated into the form's packaged Language files other than English, never a customization's")]
    public void Find_PackagedNonEnglishFilesOfTheForm()
    {
        var files = RuleMessageFiles.Find(_solution, "Order");

        var file = Assert.Single(files);
        Assert.Equal("zh-TW", file.Language);
        Assert.Equal(_zhTwPath, file.Path);
        Assert.Empty(RuleMessageFiles.Find(_solution with { DefinePath = string.Empty }, "Order"));
    }

    [Fact]
    [DisplayName("Writing a rule message adds, replaces and with an empty text removes the key, and keeps the file's comments")]
    public void Write_AddsReplacesRemoves_KeepsComments()
    {
        var file = new RuleMessageFile("zh-TW", _zhTwPath);

        RuleMessageFiles.Write(file, RuleId, "第一版");
        RuleMessageFiles.Write(file, RuleId, "第二版");

        Assert.Equal("第二版", RuleMessageFiles.Read(file, RuleId));
        var text = File.ReadAllText(_zhTwPath);
        Assert.Contains("Kept on every write.", text, StringComparison.Ordinal);
        Assert.Contains("Rule.approver_rule.Message", text, StringComparison.Ordinal);
        Assert.Equal(1, text.Split("Rule.approver_rule.Message").Length - 1);

        RuleMessageFiles.Write(file, RuleId, string.Empty);
        Assert.Equal(string.Empty, RuleMessageFiles.Read(file, RuleId));
        Assert.Equal("Order", XmlCodec.DeserializeFromFile<LanguageResource>(_zhTwPath)!.GetText(FormSchemaLocalizer.SchemaDisplayNameKey));
    }

    [Fact]
    [DisplayName("A draft shows the engine's samples and its translations, and applying it rewrites the rule under its own id")]
    public async Task Draft_Apply_RewritesRule()
    {
        var client = new ScriptedChatClient().Then(Call("c1", "submit_rule", Draft)).Then(Text("好了。"));
        var document = Load(client);
        var (editor, assistant, property) = Open(document);
        var applied = 0;
        assistant.Applied += (_, _) => applied++;

        assistant.Request = "金額超過 10 萬要有核准人";
        await assistant.DraftCommand.ExecuteAsync(null);

        Assert.True(assistant.HasDraft);
        Assert.Empty(assistant.Reply);
        Assert.StartsWith(RuleAssistantViewModel.DescribeUsage(new RuleDraftingUsage(1, 0, 0, TimeSpan.Zero), TimeSpan.Zero)[..4],
            assistant.UsageText, StringComparison.Ordinal);
        Assert.Equal([RuleSampleOutcome.Blocked, RuleSampleOutcome.Passes, RuleSampleOutcome.Skipped],
            assistant.Samples.Select(s => s.Outcome));
        Assert.Contains("amount = 200000", assistant.Samples[0].Values, StringComparison.Ordinal);
        Assert.Equal("金額超過 10 萬時必須填核准人。", Assert.Single(assistant.Translations).Text);
        Assert.Contains("When: amount > 100000", assistant.RuleText, StringComparison.Ordinal);

        assistant.Message = "Needs an approver.";
        assistant.ApplyCommand.Execute(null);
        var rule = document.Schema.Rules![RuleId];
        var changed = FormSchemaDocumentViewModel.ApplyExpressionEdit(editor, property, rule);

        Assert.Equal(1, applied);
        Assert.True(changed);
        Assert.Equal(RuleId, assistant.AppliedRule!.RuleId);
        Assert.Equal("amount > 100000", rule.When);
        Assert.Equal("!IsBlank(approver_id)", rule.Condition);
        Assert.Equal("Needs an approver.", rule.Message);
        Assert.Null(document.Schema.Rules!.GetOrDefault("amount_needs_approver"));
    }

    [Fact]
    [DisplayName("The rule being edited is left out of the form the model drafts against, so a draft may reuse its id")]
    public async Task Draft_SameIdAsEditedRule_Accepted()
    {
        var client = new ScriptedChatClient()
            .Then(Call("c1", "submit_rule", Draft.Replace("amount_needs_approver", RuleId, StringComparison.Ordinal)))
            .Then(Text("好了。"));
        var (_, assistant, _) = Open(Load(client));

        assistant.Request = "金額超過 10 萬要有核准人";
        await assistant.DraftCommand.ExecuteAsync(null);

        Assert.True(assistant.HasDraft);
    }

    [Fact]
    [DisplayName("An explanation reads the condition as it is typed in the editor, and offers nothing to apply")]
    public async Task Explain_UsesEditorText()
    {
        var client = new ScriptedChatClient().Then(Call("c1", "submit_explanation", Explanation)).Then(Text("說明好了。"));
        var (editor, assistant, _) = Open(Load(client));

        editor.Expression = "!IsBlank(approver_id)";
        await assistant.ExplainCommand.ExecuteAsync(null);

        Assert.Contains("condition: !IsBlank(approver_id)", client.Requests[0].Messages[^1].Text, StringComparison.Ordinal);
        Assert.Equal("核准人一定要填。", assistant.Explanation);
        Assert.Equal(2, assistant.Samples.Count);
        Assert.False(assistant.HasDraft);
        Assert.False(assistant.ApplyCommand.CanExecute(null));
    }

    [Fact]
    [DisplayName("A failed request is reported and leaves the assistant ready to retry")]
    public async Task Draft_Failure_Reported()
    {
        var client = new ScriptedChatClient().Then((_, _) => throw new HttpRequestException("network down"));
        var (_, assistant, _) = Open(Load(client));

        assistant.Request = "金額超過 10 萬要有核准人";
        await assistant.DraftCommand.ExecuteAsync(null);

        Assert.True(assistant.IsReplyError);
        Assert.Contains("network down", assistant.Reply, StringComparison.Ordinal);
        Assert.False(assistant.IsBusy);
        Assert.True(assistant.DraftCommand.CanExecute(null));
        Assert.False(assistant.ReviseCommand.CanExecute(null));
    }

    [Fact]
    [DisplayName("Without a credential the assistant asks nothing and never creates a chat client")]
    public void NoCredential_NothingSent()
    {
        var created = 0;
        var document = FormSchemaDocumentViewModel.Load(_solution.FormSchemaPaths["Order"], _solution);
        document.RuleDraftingChatClientFactory = () =>
        {
            created++;
            return new ScriptedChatClient();
        };
        document.RuleDraftingCredentialProvider = () => new DraftingCredential(false, "none", []);
        var (_, assistant, _) = Open(document);

        assistant.Request = "金額超過 10 萬要有核准人";

        Assert.False(assistant.DraftCommand.CanExecute(null));
        Assert.False(assistant.ExplainCommand.CanExecute(null));
        Assert.Equal(0, created);
    }

    [Fact]
    [DisplayName("The model is offered only the helper functions an untrusted author may call")]
    public async Task Draft_FunctionsWithinAllowlist()
    {
        var client = new ScriptedChatClient().Then(Text("請問核准人是哪個欄位？"));
        var (_, assistant, _) = Open(Load(client));

        assistant.Request = "要有核准人";
        await assistant.DraftCommand.ExecuteAsync(null);

        Assert.Equal("請問核准人是哪個欄位？", assistant.Reply);
        Assert.False(assistant.IsReplyError);
        var prompt = client.Requests[0].Messages[0].Text;
        Assert.Contains("- IsBlank(", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("- RegexMatch(", prompt, StringComparison.Ordinal);
        Assert.Contains(DynamicExpressoEvaluator.HelperFunctions, f => f.Name == "RegexMatch");
        Assert.True(assistant.ReviseCommand.CanExecute(null));
    }

    [Fact]
    [DisplayName("The main window writes the translations, but not into a Language file open with unsaved changes")]
    public void WriteRuleMessages_SkipsDirtyOpenFile()
    {
        var file = new RuleMessageFile("zh-TW", _zhTwPath);
        var translation = new RuleMessageTranslation(file) { Text = "必須填核准人。" };
        var window = new MainWindowViewModel();
        var open = LanguageDocumentViewModel.Load(_zhTwPath);
        window.OpenDocuments.Add(open);
        open.IsDirty = true;

        window.WriteRuleMessages(new RuleMessagesAppliedEventArgs(RuleId, [translation]));
        Assert.Equal(string.Empty, RuleMessageFiles.Read(file, RuleId));
        Assert.Contains(open, window.OpenDocuments);

        open.IsDirty = false;
        window.WriteRuleMessages(new RuleMessagesAppliedEventArgs(RuleId, [translation]));
        Assert.Equal("必須填核准人。", RuleMessageFiles.Read(file, RuleId));
        Assert.DoesNotContain(open, window.OpenDocuments);
    }

    [Fact]
    [DisplayName("The usage line names the calls, the tokens with thousands separators and the seconds, or says no tokens were reported")]
    public void DescribeUsage_TokensOrNone()
    {
        var withTokens = RuleAssistantViewModel.DescribeUsage(new RuleDraftingUsage(2, 18240, 2105, TimeSpan.FromSeconds(30)),
            TimeSpan.FromSeconds(31.6));
        var withoutTokens = RuleAssistantViewModel.DescribeUsage(new RuleDraftingUsage(3, 0, 0, TimeSpan.Zero), TimeSpan.FromSeconds(5));

        Assert.Contains("18,240", withTokens, StringComparison.Ordinal);
        Assert.Contains("2,105", withTokens, StringComparison.Ordinal);
        Assert.Contains("32", withTokens, StringComparison.Ordinal);
        Assert.Contains("3", withoutTokens, StringComparison.Ordinal);
        Assert.DoesNotContain("18,240", withoutTokens, StringComparison.Ordinal);
    }
}
