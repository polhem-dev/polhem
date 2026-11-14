using System.ComponentModel;
using Microsoft.Extensions.AI;
using Polhem.Core.Data;
using Polhem.Definition.Layouts;
using static Polhem.Definition.Drafting.UnitTests.ScriptedChatClient;

namespace Polhem.Definition.Drafting.UnitTests
{
    public class FormDraftSessionTests
    {
        private static readonly AIContent[] s_document = [new DataContent(new byte[] { 1, 2, 3 }, "application/pdf")];

        [Fact]
        [DisplayName("Tool calls build the draft, DraftChanged is raised after each change, and the reply reaches AssistantMessage")]
        public async Task StartAsync_ToolCalls_BuildDraftAndRaiseEvents()
        {
            var client = new ScriptedChatClient()
                .Then(Call("1", "set_form_info", """{"progId":"leave request","displayName":"Leave Request","displayNameZhTw":"請假單"}"""),
                    Call("2", "upsert_table", """{"table":"master","displayName":"Leave Request"}"""),
                    Call("3", "upsert_field", """{"table":"master","fieldName":"Leave Type","caption":"Leave Type","captionZhTw":"假別","dbType":"String","maxLength":10,"options":[{"Value":"annual","Text":"Annual"},{"Value":"sick","Text":"Sick"}],"sourceQuote":"假別：□特休 □病假"}"""),
                    Call("4", "upsert_field", """{"table":"master","fieldName":"start_date","caption":"Start Date","dbType":"Date","controlType":"DateEdit","required":true}"""))
                .Then(Text("I drafted the form. Use the ProgId LeaveRequest?"));
            var session = new FormDraftSession(client, new FakeDraftingHost());
            var changes = 0;
            var replies = new List<string>();
            session.DraftChanged += (_, _) => changes++;
            session.AssistantMessage += (_, e) => replies.Add(e.Text);

            await session.StartAsync(s_document);

            Assert.Equal(4, changes);
            Assert.Equal(["I drafted the form. Use the ProgId LeaveRequest?"], replies);
            Assert.Equal("LeaveRequest", session.Draft.ProgId);
            Assert.Equal("請假單", session.Draft.DisplayNameZhTw);
            var master = Assert.Single(session.Draft.Tables);
            Assert.Equal(["leave_type", "start_date"], master.Fields.Select(f => f.FieldName));
            var leaveType = master.Fields[0];
            Assert.Equal(["annual", "sick"], leaveType.Options.Select(o => o.Value));
            Assert.Equal("假別：□特休 □病假", leaveType.SourceQuote);
            var startDate = master.Fields[1];
            Assert.Equal(FieldDbType.Date, startDate.DbType);
            Assert.Equal(ControlType.DateEdit, startDate.ControlType);
            Assert.True(startDate.Required);
            Assert.All(client.LastResults(), result => Assert.DoesNotContain("Error", result.Result?.ToString()));
        }

        [Fact]
        [DisplayName("The first message carries the catalog of existing forms and the documents")]
        public async Task StartAsync_FirstMessage_CarriesCatalogAndDocuments()
        {
            var client = new ScriptedChatClient().Then(Text("Reading."));
            var session = new FormDraftSession(client, new FakeDraftingHost());

            await session.StartAsync(s_document);

            var messages = client.Requests[0].Messages;
            Assert.Equal(ChatRole.System, messages[0].Role);
            var user = messages[1];
            Assert.Equal(ChatRole.User, user.Role);
            Assert.Contains("Employee: Employee / 員工", user.Text);
            Assert.Same(s_document[0], user.Contents[^1]);
        }

        [Fact]
        [DisplayName("The model is given declarations only, so a function-invoking client cannot run the tools itself")]
        public async Task StartAsync_Tools_AreDeclarationsOnly()
        {
            var client = new ScriptedChatClient().Then(Text("Reading."));
            var session = new FormDraftSession(client, new FakeDraftingHost(),
                new FormDraftingOptions { ChatOptions = new ChatOptions { ModelId = "test-model" } });

            await session.StartAsync(s_document);

            var options = client.Requests[0].Options!;
            Assert.Equal("test-model", options.ModelId);
            Assert.Contains(options.Tools!, tool => tool.Name == "mark_ready");
            Assert.All(options.Tools!, tool => Assert.IsNotAssignableFrom<AIFunction>(tool));
        }

        [Fact]
        [DisplayName("Invalid tool input returns an error result, leaves the draft unchanged, and the conversation goes on")]
        public async Task StartAsync_InvalidToolInput_ReturnsErrorAndContinues()
        {
            var client = new ScriptedChatClient()
                .Then(Call("1", "upsert_field", """{"table":"master","fieldName":"amount","dbType":"Banana"}"""),
                    Call("2", "upsert_field", """{"table":"master"}"""),
                    Call("3", "upsert_field", """{"table":"master","fieldName":"sys_id"}"""),
                    Call("4", "upsert_field", """{"table":"master","fieldName":"amount","maxLength":0}"""),
                    Call("5", "no_such_tool"),
                    Call("6", "upsert_table", """{"table":"Lines"}"""))
                .Then(Text("Let me fix that."));
            var session = new FormDraftSession(client, new FakeDraftingHost());
            var replies = new List<string>();
            session.AssistantMessage += (_, e) => replies.Add(e.Text);

            await session.StartAsync(s_document);

            var results = client.LastResults();
            Assert.Equal(["1", "2", "3", "4", "5", "6"], results.Select(r => r.CallId));
            Assert.All(results.Take(5), result => Assert.StartsWith("Error:", result.Result?.ToString()));
            Assert.DoesNotContain("Error", results[5].Result?.ToString());
            Assert.Equal(["Let me fix that."], replies);
            Assert.Equal(["master", "Lines"], session.Draft.Tables.Select((t, i) => i == 0 ? "master" : t.TableName));
            Assert.All(session.Draft.Tables, table => Assert.Empty(table.Fields));
        }

        [Fact]
        [DisplayName("upsert_field keeps the field's name when renameTo is empty or the word null")]
        public async Task UpsertField_RenameToWithoutValue_KeepsTheName()
        {
            var client = new ScriptedChatClient()
                .Then(Call("1", "upsert_field", """{"table":"master","fieldName":"reason","caption":"Reason"}"""))
                .Then(Call("2", "upsert_field", """{"table":"master","fieldName":"reason","captionZhTw":"事由","renameTo":""}"""),
                    Call("3", "upsert_field", """{"table":"master","fieldName":"reason","dbType":"Text","renameTo":"null"}"""))
                .Then(Text("Done."));
            var session = new FormDraftSession(client, new FakeDraftingHost());

            await session.StartAsync(s_document);

            Assert.All(client.LastResults(), result => Assert.DoesNotContain("Error", result.Result?.ToString()));
            var field = Assert.Single(session.Draft.Tables[0].Fields);
            Assert.Equal("reason", field.FieldName);
            Assert.Equal("事由", field.CaptionZhTw);
            Assert.Equal(FieldDbType.Text, field.DbType);
        }

        [Fact]
        [DisplayName("mark_ready fails while the host's validation reports an error")]
        public async Task MarkReady_ValidationError_Fails()
        {
            var host = new FakeDraftingHost();
            host.ValidationIssues.Add(new DraftIssue(DraftIssueSeverity.Error, "LeaveRequest.reason", "Too long."));
            var client = CompleteDraftScript().Then(Call("m", "mark_ready")).Then(Text("Not yet."));
            var session = new FormDraftSession(client, host);

            await session.StartAsync(s_document);

            Assert.False(session.IsReady);
            var result = Assert.Single(client.LastResults()).Result?.ToString();
            Assert.StartsWith("Error:", result);
            Assert.Contains("Too long.", result);
        }

        [Fact]
        [DisplayName("mark_ready succeeds when there are no errors, and validate_draft runs the host's validation")]
        public async Task MarkReady_NoErrors_Succeeds()
        {
            var host = new FakeDraftingHost();
            host.ValidationIssues.Add(new DraftIssue(DraftIssueSeverity.Warning, "LeaveRequest", "Only a warning."));
            var client = CompleteDraftScript()
                .Then(Call("v", "validate_draft"))
                .Then(Call("m", "mark_ready"))
                .Then(Text("Ready."));
            var session = new FormDraftSession(client, host);

            await session.StartAsync(s_document);

            Assert.True(session.IsReady);
            Assert.True(host.ValidateCalls >= 2);
            Assert.DoesNotContain("Error", Assert.Single(client.LastResults()).Result?.ToString());
        }

        [Fact]
        [DisplayName("mark_ready fails without a ProgId, even when the converter only warns about it")]
        public async Task MarkReady_NoProgId_Fails()
        {
            var client = new ScriptedChatClient()
                .Then(Call("1", "upsert_field", """{"table":"master","fieldName":"reason","caption":"Reason","dbType":"Text"}"""))
                .Then(Call("m", "mark_ready"))
                .Then(Text("I need a ProgId."));
            var session = new FormDraftSession(client, new FakeDraftingHost());

            await session.StartAsync(s_document);

            Assert.False(session.IsReady);
            Assert.Contains("ProgId", Assert.Single(client.LastResults()).Result?.ToString());
        }

        [Fact]
        [DisplayName("A low-confidence relation blocks mark_ready until it is set again with full confidence")]
        public async Task MarkReady_LowConfidenceRelation_RequiresConfirmation()
        {
            var client = CompleteDraftScript()
                .Then(Call("r", "set_relation", """{"table":"master","fieldName":"applicant","progId":"employee","confidence":0.5,"reason":"Looks like a person."}"""))
                .Then(Call("m", "mark_ready"))
                .Then(Text("Is the applicant an employee?"))
                .Then(Call("r2", "set_relation", """{"table":"master","fieldName":"applicant","progId":"Employee","confidence":1,"reason":"Confirmed by the user."}"""))
                .Then(Call("m2", "mark_ready"))
                .Then(Text("Ready."));
            var session = new FormDraftSession(client, new FakeDraftingHost());

            await session.StartAsync(s_document);

            Assert.False(session.IsReady);
            Assert.StartsWith("Error:", Assert.Single(client.LastResults()).Result?.ToString());
            Assert.Contains("confirm", client.Requests[^2].Messages[^1].Contents.OfType<FunctionResultContent>().Single().Result?.ToString());

            await session.SendAsync("Yes, an employee.");

            Assert.True(session.IsReady);
            var relation = session.Draft.Tables[0].Fields.Single(f => f.FieldName == "applicant").Relation!;
            Assert.Equal("Employee", relation.ProgId);
            Assert.Equal(1, relation.Confidence);
        }

        [Fact]
        [DisplayName("set_relation refuses a ProgId that is not in the host's catalog")]
        public async Task SetRelation_UnknownProgId_IsRefused()
        {
            var client = CompleteDraftScript()
                .Then(Call("r", "set_relation", """{"table":"master","fieldName":"applicant","progId":"Supplier","confidence":0.9,"reason":"?"}"""))
                .Then(Text("No such form."));
            var session = new FormDraftSession(client, new FakeDraftingHost());

            await session.StartAsync(s_document);

            Assert.StartsWith("Error:", Assert.Single(client.LastResults()).Result?.ToString());
            Assert.Null(session.Draft.Tables[0].Fields.Single(f => f.FieldName == "applicant").Relation);
        }

        [Fact]
        [DisplayName("search_progids matches Chinese names as well as English ones")]
        public async Task SearchProgIds_ChineseKeyword_FindsForm()
        {
            var client = new ScriptedChatClient()
                .Then(Call("s", "search_progids", """{"query":"部門"}"""))
                .Then(Text("Found it."));
            var session = new FormDraftSession(client, new FakeDraftingHost());

            await session.StartAsync(s_document);

            var result = Assert.Single(client.LastResults()).Result?.ToString();
            Assert.Contains("Department", result);
            Assert.DoesNotContain("Employee", result);
        }

        [Fact]
        [DisplayName("A change to the draft after mark_ready makes it no longer ready")]
        public async Task ToolChange_AfterReady_ClearsIsReady()
        {
            var client = CompleteDraftScript()
                .Then(Call("m", "mark_ready"))
                .Then(Text("Ready."))
                .Then(Call("f", "upsert_field", """{"table":"master","fieldName":"note","caption":"Note","dbType":"Text"}"""))
                .Then(Text("Added a note field."));
            var session = new FormDraftSession(client, new FakeDraftingHost());
            await session.StartAsync(s_document);
            Assert.True(session.IsReady);

            await session.SendAsync("Add a note field.");

            Assert.False(session.IsReady);
        }

        [Fact]
        [DisplayName("The history only grows: each request starts with every message of the previous one, reasoning included")]
        public async Task History_IsAppendOnly_AndKeepsReasoning()
        {
            var reasoning = new TextReasoningContent("The applicant is a person.") { ProtectedData = "signature" };
            var client = new ScriptedChatClient()
                .Then(reasoning, Call("1", "set_form_info", """{"progId":"LeaveRequest"}"""))
                .Then(Text("Drafted."))
                .Then(Text("Done."));
            var session = new FormDraftSession(client, new FakeDraftingHost());

            await session.StartAsync(s_document);
            await session.SendAsync("Thanks.");

            for (var index = 1; index < client.Requests.Count; index++)
            {
                var previous = client.Requests[index - 1].Messages;
                var current = client.Requests[index].Messages;
                Assert.True(current.Count > previous.Count);
                for (var position = 0; position < previous.Count; position++)
                    Assert.Same(previous[position], current[position]);
            }
            Assert.Contains(client.Requests[^1].Messages, message => message.Contents.Contains(reasoning));
            Assert.Equal(client.Requests[^1].Messages.Count + 1, session.History.Count);
        }

        [Fact]
        [DisplayName("A user edit reaches the model at the start of the next message and clears IsReady")]
        public async Task ApplyUserEdit_NextMessage_CarriesTheEdit()
        {
            var client = CompleteDraftScript().Then(Call("m", "mark_ready")).Then(Text("Ready.")).Then(Text("Noted."));
            var session = new FormDraftSession(client, new FakeDraftingHost());
            await session.StartAsync(s_document);
            var historyBefore = session.History.ToList();
            var changes = 0;
            session.DraftChanged += (_, _) => changes++;

            session.ApplyUserEdit(draft => draft.Tables[0].Fields[0].Caption = "Employee",
                "Changed the caption of applicant to 'Employee'.");

            Assert.False(session.IsReady);
            Assert.Equal(1, changes);
            Assert.Equal(historyBefore, session.History);

            await session.SendAsync("Looks good.");

            var user = client.Requests[^1].Messages[^1];
            Assert.Equal(ChatRole.User, user.Role);
            Assert.Contains("Changed the caption of applicant to 'Employee'.", ((TextContent)user.Contents[0]).Text);
            Assert.Equal("Looks good.", ((TextContent)user.Contents[1]).Text);
        }

        [Fact]
        [DisplayName("A cancelled turn is removed as a whole and the draft is put back as it was")]
        public async Task SendAsync_Cancelled_RestoresSessionState()
        {
            using var cancellation = new CancellationTokenSource();
            var client = CompleteDraftScript()
                .Then(Call("m", "mark_ready"))
                .Then(Text("Ready."))
                .Then(Call("x", "remove_field", """{"table":"master","fieldName":"applicant"}"""),
                    Call("y", "remove_field", """{"table":"master","fieldName":"reason"}"""))
                .Then((_, token) =>
                {
                    cancellation.Cancel();
                    token.ThrowIfCancellationRequested();
                    return new ChatResponse(new ChatMessage(ChatRole.Assistant, "Unreachable."));
                })
                .Then(Text("Retried."));
            var session = new FormDraftSession(client, new FakeDraftingHost());
            await session.StartAsync(s_document);
            var historyBefore = session.History.ToList();
            var fieldsBefore = session.Draft.Tables[0].Fields.Select(f => f.FieldName).ToList();
            var changes = 0;
            session.DraftChanged += (_, _) => changes++;

            session.ApplyUserEdit(draft => draft.DisplayNameZhTw = "請假申請單", "Changed the zh-TW name.");
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.SendAsync("Remove them.", cancellation.Token));

            Assert.Equal(historyBefore, session.History);
            Assert.Equal(fieldsBefore, session.Draft.Tables[0].Fields.Select(f => f.FieldName));
            Assert.Equal("請假申請單", session.Draft.DisplayNameZhTw);
            Assert.Equal(4, changes);

            await session.SendAsync("Try again.");

            var user = client.Requests[^1].Messages[^1];
            Assert.Contains("Changed the zh-TW name.", ((TextContent)user.Contents[0]).Text);
        }

        [Fact]
        [DisplayName("After the tool-round limit the model is asked to answer without tools, and further calls are not run")]
        public async Task StartAsync_ToolRoundLimit_EndsTheTurn()
        {
            var client = new ScriptedChatClient()
                .Then(Call("1", "upsert_table", """{"table":"master"}"""))
                .Then(Call("2", "upsert_table", """{"table":"Lines"}"""))
                .Then(Text("Stopping here."));
            var session = new FormDraftSession(client, new FakeDraftingHost(), new FormDraftingOptions { MaxToolRoundsPerTurn = 2 });

            await session.StartAsync(s_document);

            Assert.Equal(3, client.Requests.Count);
            Assert.Equal(ChatToolMode.None, client.Requests[2].Options!.ToolMode);
            Assert.Equal(ChatToolMode.Auto, client.Requests[0].Options!.ToolMode);
        }

        [Fact]
        [DisplayName("SendAsync before StartAsync throws")]
        public async Task SendAsync_BeforeStart_Throws()
        {
            var session = new FormDraftSession(new ScriptedChatClient(), new FakeDraftingHost());

            await Assert.ThrowsAsync<InvalidOperationException>(() => session.SendAsync("Hello."));
        }

        [Fact]
        [DisplayName("A second turn cannot start while one is in progress")]
        public async Task SendAsync_DuringTurn_Throws()
        {
            FormDraftSession? session = null;
            Exception? nested = null;
            var client = new ScriptedChatClient()
                .Then((_, _) =>
                {
                    nested = Record.Exception(() => session!.ApplyUserEdit(_ => { }, "An edit."));
                    return new ChatResponse(new ChatMessage(ChatRole.Assistant, "Reading."));
                });
            session = new FormDraftSession(client, new FakeDraftingHost());

            await session.StartAsync(s_document);

            Assert.IsType<InvalidOperationException>(nested);
        }

        /// <summary>
        /// A script whose first response builds a draft that converts without errors: a ProgId, a display name and two
        /// fields with types and lengths.
        /// </summary>
        private static ScriptedChatClient CompleteDraftScript()
            => new ScriptedChatClient()
                .Then(Call("1", "set_form_info", """{"progId":"LeaveRequest","displayName":"Leave Request"}"""),
                    Call("2", "upsert_field", """{"table":"master","fieldName":"applicant","caption":"Applicant","dbType":"String","maxLength":20}"""),
                    Call("3", "upsert_field", """{"table":"master","fieldName":"reason","caption":"Reason","dbType":"Text"}"""));
    }
}
