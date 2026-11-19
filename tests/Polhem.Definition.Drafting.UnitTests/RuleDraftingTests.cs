using System.ComponentModel;
using System.Globalization;
using Microsoft.Extensions.AI;
using Polhem.Core.Data;
using Polhem.Definition.Drafting.Rules;
using Polhem.Definition.Forms;
using Polhem.Expressions;
using static Polhem.Definition.Drafting.UnitTests.ScriptedChatClient;

namespace Polhem.Definition.Drafting.UnitTests
{
    /// <summary>
    /// <see cref="RuleDrafting"/> accepts a rule only when its expressions pass the untrusted-author checks and the
    /// expression engine computes the outcome the model claimed for every sample; otherwise the reasons go back to the
    /// model. The model is scripted, never called.
    /// </summary>
    public class RuleDraftingTests
    {
        private const string ValidRule = """
            {"ruleId":"amount_needs_approver","targetTable":"","when":"amount > 100000","condition":"!IsBlank(approver_id)",
             "message":"An amount over 100,000 needs an approver.","messageTranslations":[{"language":"zh-TW","text":"金額超過 10 萬時必須填核准人。"}],
             "explanation":"金額超過 10 萬的訂單必須填核准人。",
             "samples":[
               {"description":"金額 20 萬、沒有核准人","values":[{"field":"amount","value":"200000"},{"field":"approver_id","value":""}],"detailRows":[],"expected":"Blocked"},
               {"description":"金額 20 萬、有核准人","values":[{"field":"amount","value":"200000"},{"field":"approver_id","value":"E01"}],"detailRows":[],"expected":"Passes"},
               {"description":"金額 5 萬","values":[{"field":"amount","value":"50000"},{"field":"approver_id","value":""}],"detailRows":[],"expected":"Skipped"}]}
            """;

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var master = schema.Tables!.Add("Order", "Order");
            master.Fields!.Add(new FormField("amount", "Amount", FieldDbType.Currency));
            master.Fields!.Add(new FormField("approver_id", "Approver", FieldDbType.String));
            master.Fields!.Add(new FormField("order_date", "Order date", FieldDbType.Date));
            var detail = schema.Tables!.Add("OrderLine", "Order line");
            detail.Fields!.Add(new FormField("qty", "Qty", FieldDbType.Integer));
            schema.Rules!.Add(new FormRule("existing_rule", "amount >= 0", "Amount cannot be negative."));
            return schema;
        }

        private static RuleDrafting Create(ScriptedChatClient client, FormSchema? schema = null, int maxRounds = 6) =>
            new(client, schema ?? BuildSchema(), new FormExpressionCalculator(new DynamicExpressoEvaluator()), new RuleDraftingOptions
            {
                MessageLanguages = ["zh-TW"],
                ReplyCulture = CultureInfo.GetCultureInfo("zh-TW"),
                MaxToolRounds = maxRounds,
                Functions = [new RuleDraftFunction("IsBlank", "(object value) → bool", "Whether a value is not filled in.", "IsBlank(x)")],
            });

        private static string Result(ScriptedChatClient client) => client.LastResults().Single().Result?.ToString() ?? string.Empty;

        [Fact]
        [DisplayName("A rule whose samples the engine confirms is accepted, with the engine's outcomes and the model's reply")]
        public async Task DraftAsync_ValidRule_Accepted()
        {
            var client = new ScriptedChatClient()
                .Then(Call("c1", "submit_rule", ValidRule))
                .Then(Text("已建立規則。"));

            var result = await Create(client).DraftAsync("金額超過 10 萬要有核准人");

            var draft = Assert.IsType<RuleDraft>(result.Draft);
            Assert.Equal("amount_needs_approver", draft.RuleId);
            Assert.Equal("amount > 100000", draft.When);
            Assert.Equal("金額超過 10 萬時必須填核准人。", draft.MessageTranslations["zh-TW"]);
            Assert.Equal([RuleSampleOutcome.Blocked, RuleSampleOutcome.Passes, RuleSampleOutcome.Skipped], draft.Samples.Select(s => s.Outcome));
            Assert.Equal("已建立規則。", result.Reply);
            Assert.Equal(ChatToolMode.None, client.Requests[^1].Options!.ToolMode);
            // The reply request keeps the tool list: Claude binds the model's reasoning to the tools it saw.
            Assert.Equal(client.Requests[0].Options!.Tools!.Select(t => t.Name), client.Requests[^1].Options!.Tools!.Select(t => t.Name));
        }

        [Fact]
        [DisplayName("A sample whose claimed outcome the engine does not compute is refused, and the corrected rule is accepted")]
        public async Task DraftAsync_WrongExpectation_RefusedThenAccepted()
        {
            var wrong = ValidRule.Replace("\"expected\":\"Skipped\"", "\"expected\":\"Blocked\"", StringComparison.Ordinal);
            var client = new ScriptedChatClient()
                .Then(Call("c1", "submit_rule", wrong))
                .Then((messages, _) =>
                {
                    Assert.Contains("the engine computes Skipped, not Blocked", messages[^1].Contents.OfType<FunctionResultContent>().Single().Result?.ToString());
                    return new ChatResponse(new ChatMessage(ChatRole.Assistant, [Call("c2", "submit_rule", ValidRule)]));
                })
                .Then(Text("好了。"));

            var result = await Create(client).DraftAsync("金額超過 10 萬要有核准人");

            Assert.NotNull(result.Draft);
        }

        [Theory]
        [DisplayName("A rule is refused for an expression outside the allowlist, a missing translation, one-sided samples or an unknown field")]
        [InlineData("\"condition\":\"!IsBlank(approver_id)\"", "\"condition\":\"approver_id.GetType() != null\"", "GetType")]
        [InlineData("\"messageTranslations\":[{\"language\":\"zh-TW\",\"text\":\"金額超過 10 萬時必須填核准人。\"}]", "\"messageTranslations\":[]", "zh-TW")]
        [InlineData("\"expected\":\"Blocked\"},", "\"expected\":\"Passes\"},", "engine computes")]
        [InlineData("{\"field\":\"approver_id\",\"value\":\"E01\"}", "{\"field\":\"approver\",\"value\":\"E01\"}", "not a field")]
        [InlineData("\"ruleId\":\"amount_needs_approver\"", "\"ruleId\":\"existing_rule\"", "already has a rule")]
        [InlineData("\"targetTable\":\"\"", "\"targetTable\":\"Missing\"", "not a table")]
        public async Task DraftAsync_InvalidSubmission_Refused(string from, string to, string expected)
        {
            var client = new ScriptedChatClient()
                .Then(Call("c1", "submit_rule", ValidRule.Replace(from, to, StringComparison.Ordinal)))
                .Then(Text("我無法完成。"));

            var result = await Create(client).DraftAsync("金額超過 10 萬要有核准人");

            Assert.Null(result.Draft);
            Assert.Contains(expected, Result(client), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A master rule that reads a detail table is computed over the sample's detail rows")]
        public async Task DraftAsync_MasterAggregateRule_UsesSampleDetailRows()
        {
            var rule = """
                {"ruleId":"has_positive_lines","targetTable":"","when":"","condition":"Count(OrderLine) > 0 && All(OrderLine, qty > 0)",
                 "message":"An order needs lines with a quantity.","messageTranslations":[{"language":"zh-TW","text":"訂單至少要有一筆數量大於 0 的明細。"}],
                 "explanation":"訂單至少要有一筆明細，而且每筆數量都要大於 0。",
                 "samples":[
                   {"description":"沒有明細","values":[{"field":"amount","value":"0"}],"detailRows":[],"expected":"Blocked"},
                   {"description":"兩筆數量都大於 0","values":[{"field":"amount","value":"10"}],
                    "detailRows":[{"table":"OrderLine","values":[{"field":"qty","value":"2"}]},{"table":"OrderLine","values":[{"field":"qty","value":"1"}]}],"expected":"Passes"},
                   {"description":"有一筆數量是 0","values":[{"field":"amount","value":"10"}],
                    "detailRows":[{"table":"OrderLine","values":[{"field":"qty","value":"2"}]},{"table":"OrderLine","values":[{"field":"qty","value":"0"}]}],"expected":"Blocked"}]}
                """;
            var client = new ScriptedChatClient().Then(Call("c1", "submit_rule", rule)).Then(Text("好了。"));

            var result = await Create(client).DraftAsync("訂單至少要有一筆明細，數量都要大於 0");

            var draft = Assert.IsType<RuleDraft>(result.Draft);
            Assert.Equal([RuleSampleOutcome.Blocked, RuleSampleOutcome.Passes, RuleSampleOutcome.Blocked], draft.Samples.Select(s => s.Outcome));
            Assert.Equal(2, draft.Samples[1].DetailRows["OrderLine"].Count);
        }

        [Fact]
        [DisplayName("Detail rows on a sample of a detail table's rule are refused")]
        public async Task DraftAsync_DetailRowsOnDetailRule_Refused()
        {
            var rule = """
                {"ruleId":"qty_positive","targetTable":"OrderLine","when":"","condition":"qty > 0",
                 "message":"Quantity must be positive.","messageTranslations":[{"language":"zh-TW","text":"數量必須大於 0。"}],
                 "explanation":"每筆明細數量都要大於 0。",
                 "samples":[
                   {"description":"0","values":[{"field":"qty","value":"0"}],"detailRows":[{"table":"OrderLine","values":[{"field":"qty","value":"1"}]}],"expected":"Blocked"},
                   {"description":"1","values":[{"field":"qty","value":"1"}],"detailRows":[],"expected":"Passes"}]}
                """;
            var client = new ScriptedChatClient().Then(Call("c1", "submit_rule", rule)).Then(Text("…"));

            var result = await Create(client).DraftAsync("數量要大於 0");

            Assert.Null(result.Draft);
            Assert.Contains("only a rule on the master table", Result(client), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A sample that makes the rule fail when it runs is refused, since such a record could not be saved")]
        public async Task DraftAsync_RuleFailsOnSample_Refused()
        {
            var rule = ValidRule.Replace("\"when\":\"amount > 100000\"", "\"when\":\"100 / amount < 1\"", StringComparison.Ordinal)
                .Replace("{\"field\":\"amount\",\"value\":\"50000\"}", "{\"field\":\"amount\",\"value\":\"0\"}", StringComparison.Ordinal);
            var client = new ScriptedChatClient().Then(Call("c1", "submit_rule", rule)).Then(Text("…"));

            var result = await Create(client).DraftAsync("x");

            Assert.Null(result.Draft);
            Assert.Contains("DivideByZeroException", Result(client), StringComparison.Ordinal);
        }

        [Theory]
        [DisplayName("The target table is stored as the form names it, and empty for the master table")]
        [InlineData("\"targetTable\":\"order\"", "")]
        [InlineData("\"targetTable\":\" Order \"", "")]
        public async Task DraftAsync_TargetTableSpelling_StoredCanonical(string targetTable, string expected)
        {
            var client = new ScriptedChatClient()
                .Then(Call("c1", "submit_rule", ValidRule.Replace("\"targetTable\":\"\"", targetTable, StringComparison.Ordinal)))
                .Then(Text("好了。"));

            var result = await Create(client).DraftAsync("x");

            Assert.Equal(expected, result.Draft!.TargetTable);
        }

        [Fact]
        [DisplayName("A null sample is ignored, a message over the length limit is refused, and only one submission per response runs")]
        public async Task DraftAsync_NullSampleLongMessageAndSecondCall()
        {
            var withNull = ValidRule.Replace("\"samples\":[", "\"samples\":[null,", StringComparison.Ordinal);
            var longMessage = ValidRule.Replace("An amount over 100,000 needs an approver.", new string('x', 501), StringComparison.Ordinal);
            var client = new ScriptedChatClient()
                .Then(Call("c1", "submit_rule", longMessage), Call("c2", "submit_rule", withNull))
                .Then((messages, _) =>
                {
                    var results = messages[^1].Contents.OfType<FunctionResultContent>().ToList();
                    Assert.Contains("longer than 500", results[0].Result?.ToString(), StringComparison.Ordinal);
                    Assert.Equal("Not run: submit one at a time.", results[1].Result?.ToString());
                    return new ChatResponse(new ChatMessage(ChatRole.Assistant, [Call("c3", "submit_rule", withNull)]));
                })
                .Then(Text("好了。"));

            var result = await Create(client).DraftAsync("x");

            Assert.Equal(3, result.Draft!.Samples.Count);
        }

        [Fact]
        [DisplayName("A rule outside the allowlist is not explained, and the model is not asked")]
        public async Task ExplainAsync_RuleOutsideAllowlist_NotSent()
        {
            var client = new ScriptedChatClient();
            var schema = BuildSchema();
            var rule = new FormRule("bad", new string('(', 20) + "amount" + new string(')', 20) + " > 0", "Bad.");

            var result = await Create(client, schema).ExplainAsync(rule);

            Assert.Null(result.Explanation);
            Assert.Contains("nests deeper", result.Reply, StringComparison.Ordinal);
            Assert.Empty(client.Requests);
        }

        [Fact]
        [DisplayName("Samples that only pass are refused: the user must see one the rule blocks")]
        public async Task DraftAsync_NoBlockedSample_Refused()
        {
            var onlyPasses = """
                {"ruleId":"amount_positive","targetTable":"","when":"","condition":"amount >= 0","message":"No negative amounts.",
                 "messageTranslations":[{"language":"zh-TW","text":"金額不可為負。"}],"explanation":"金額不可為負數。",
                 "samples":[{"description":"10","values":[{"field":"amount","value":"10"}],"detailRows":[],"expected":"Passes"},
                            {"description":"0","values":[{"field":"amount","value":"0"}],"detailRows":[],"expected":"Passes"}]}
                """;
            var client = new ScriptedChatClient().Then(Call("c1", "submit_rule", onlyPasses)).Then(Text("…"));

            var result = await Create(client).DraftAsync("金額不能是負的");

            Assert.Null(result.Draft);
            Assert.Contains("one that is blocked", Result(client), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A revision continues the conversation and may keep the accepted rule's id")]
        public async Task ReviseAsync_ContinuesConversationAndKeepsRuleId()
        {
            var revised = ValidRule.Replace("amount > 100000", "amount > 50000", StringComparison.Ordinal)
                .Replace("{\"field\":\"amount\",\"value\":\"50000\"}", "{\"field\":\"amount\",\"value\":\"10000\"}", StringComparison.Ordinal);
            var client = new ScriptedChatClient()
                .Then(Call("c1", "submit_rule", ValidRule)).Then(Text("好了。"))
                .Then(Call("c2", "submit_rule", revised)).Then(Text("已改為 5 萬。"));
            var drafting = Create(client);

            await drafting.DraftAsync("金額超過 10 萬要有核准人");
            var result = await drafting.ReviseAsync("改成 5 萬");

            Assert.Equal("amount > 50000", result.Draft!.When);
            Assert.Single(client.Requests[^1].Messages, message => message.Role == ChatRole.System);
            Assert.Equal(2, client.Requests[^1].Messages.Count(message => message.Role == ChatRole.User));
        }

        [Fact]
        [DisplayName("A request that fails leaves the conversation as it was")]
        public async Task ReviseAsync_Failure_RestoresConversation()
        {
            var client = new ScriptedChatClient()
                .Then(Call("c1", "submit_rule", ValidRule)).Then(Text("好了。"))
                .Then((_, _) => throw new HttpRequestException("network down"))
                .Then(Text("請再說一次。"));
            var drafting = Create(client);
            await drafting.DraftAsync("金額超過 10 萬要有核准人");
            var countBefore = client.Requests[^1].Messages.Count;

            await Assert.ThrowsAsync<HttpRequestException>(() => drafting.ReviseAsync("改成 5 萬"));
            await drafting.ReviseAsync("改成 5 萬");

            // The retry sees the earlier conversation, the reply to it, and one copy of the new message.
            Assert.Equal(countBefore + 2, client.Requests[^1].Messages.Count);
        }

        [Fact]
        [DisplayName("When the rounds run out the model is asked to reply, with no tool offered")]
        public async Task DraftAsync_RoundsRunOut_FinalRequestOffersNoTool()
        {
            var bad = ValidRule.Replace("!IsBlank(approver_id)", "approver_id.GetHashCode() > 0", StringComparison.Ordinal);
            var client = new ScriptedChatClient()
                .Then(Call("c1", "submit_rule", bad))
                .Then(Text("我寫不出符合限制的規則。"));

            var result = await Create(client, maxRounds: 1).DraftAsync("x");

            Assert.Null(result.Draft);
            Assert.Equal(2, client.Requests.Count);
            Assert.Equal(ChatToolMode.None, client.Requests[^1].Options!.ToolMode);
            Assert.Equal("我寫不出符合限制的規則。", result.Reply);
        }

        [Fact]
        [DisplayName("An explanation is accepted with the engine's outcomes for the rule being explained")]
        public async Task ExplainAsync_ConfirmedSamples_Accepted()
        {
            var explanation = """
                {"explanation":"金額不可為負數。",
                 "samples":[{"description":"金額 -1","values":[{"field":"amount","value":"-1"}],"detailRows":[],"expected":"Blocked"},
                            {"description":"金額 5","values":[{"field":"amount","value":"5"}],"detailRows":[],"expected":"Passes"}]}
                """;
            var client = new ScriptedChatClient().Then(Call("c1", "submit_explanation", explanation)).Then(Text("說明好了。"));
            var schema = BuildSchema();

            var result = await Create(client, schema).ExplainAsync(schema.Rules!["existing_rule"]);

            Assert.Equal("金額不可為負數。", result.Explanation!.Explanation);
            Assert.Equal([RuleSampleOutcome.Blocked, RuleSampleOutcome.Passes], result.Explanation.Samples.Select(s => s.Outcome));
            Assert.Contains("amount >= 0", client.Requests[0].Messages[^1].Text, StringComparison.Ordinal);
        }

        [Theory]
        [DisplayName("The rule tools declare no open-ended object and no reference, which the Claude API rejects")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Tools_Schema_HasNoAdditionalProperties(bool explain)
        {
            var client = new ScriptedChatClient().Then(Text("OK."));
            var schema = BuildSchema();
            var drafting = Create(client, schema);

            if (explain) { await drafting.ExplainAsync(schema.Rules!["existing_rule"]); }
            else { await drafting.DraftAsync("x"); }

            var tool = Assert.Single(client.Requests[0].Options!.Tools!.OfType<AIFunctionDeclaration>());
            // The Claude API refuses "additionalProperties" other than false: measured on 2026-11-19, when the
            // translations and the sample values were first declared as dictionaries.
            Assert.DoesNotContain("\"additionalProperties\":{", tool.JsonSchema.GetRawText().Replace(" ", "", StringComparison.Ordinal), StringComparison.Ordinal);
            Assert.DoesNotContain("\"additionalProperties\":true", tool.JsonSchema.GetRawText().Replace(" ", "", StringComparison.Ordinal), StringComparison.Ordinal);
            // A type used twice becomes a "$ref" to its first place, which the Claude API refuses (measured 2026-11-19).
            Assert.DoesNotContain("$ref", tool.JsonSchema.GetRawText(), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The instructions name the form's fields, the functions, the message languages and the reply language")]
        public async Task DraftAsync_Prompt_DescribesFormAndLanguages()
        {
            var client = new ScriptedChatClient().Then(Text("請問門檻是多少？"));

            var result = await Create(client).DraftAsync("金額太大要有人核准");

            var system = client.Requests[0].Messages[0].Text;
            Assert.Contains("- amount: Currency, \"Amount\"", system, StringComparison.Ordinal);
            Assert.Contains("OrderLine (detail table)", system, StringComparison.Ordinal);
            Assert.Contains("IsBlank(object value) → bool", system, StringComparison.Ordinal);
            Assert.Contains("zh-TW", system, StringComparison.Ordinal);
            Assert.Null(result.Draft);
            Assert.Equal("請問門檻是多少？", result.Reply);
        }
    }
}
