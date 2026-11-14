using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;
using Polhem.Definition.Drafting;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Drafting.UnitTests;
using Polhem.Definition.Spreadsheet;
using Polhem.Tests.Shared;
using static Polhem.Definition.Drafting.UnitTests.ScriptedChatClient;

namespace Polhem.DefineEditor.UnitTests;

/// <summary>
/// Drafting a form from documents in the editor: the solution as the drafting host, the hand-over to the workbook import,
/// the FormLayout preview, and the drafting window's view-model over a scripted model. Everything is written to an
/// isolated temporary folder; no real model is called.
/// </summary>
public sealed class FormDraftingTests : IDisposable
{
    private const string EmployeeProgId = "Employee";

    private readonly string _define = Path.Combine(Path.GetTempPath(), "polhem-drafting-" + Guid.NewGuid().ToString("N"));
    private readonly SolutionContext _solution;

    public FormDraftingTests()
    {
        var formSchemaFolder = Path.Combine(_define, "FormSchema");
        Directory.CreateDirectory(formSchemaFolder);
        var employeePath = Path.Combine(formSchemaFolder, "Employee.FormSchema.xml");
        File.Copy(Path.Combine(RepoRoot.Find(), "tests", "Define", "FormSchema", "Employee.FormSchema.xml"), employeePath);

        var languageFolder = Path.Combine(_define, "Language", "zh-TW");
        Directory.CreateDirectory(languageFolder);
        var language = new LanguageResource { Namespace = EmployeeProgId, Lang = "zh-TW" };
        language.Items.Add(FormSchemaLocalizer.SchemaDisplayNameKey, "員工");
        var languagePath = Path.Combine(languageFolder, "Employee.Language.xml");
        XmlCodec.SerializeToFile(language, languagePath);

        _solution = new SolutionContext([EmployeeProgId])
        {
            FormSchemaPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [EmployeeProgId] = employeePath },
            LanguagePaths = [languagePath],
            DefinePath = _define,
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_define)) Directory.Delete(_define, recursive: true);
    }

    /// <summary>A leave request with a code, a date, a leave type with a length, and an employee picked from Employee.</summary>
    private static FormDraft LeaveRequest()
    {
        var draft = new FormDraft { ProgId = "LeaveRequest", DisplayName = "Leave Request", DisplayNameZhTw = "請假單" };
        draft.Menu.FolderId = "hr";
        draft.Menu.FolderCaption = "HR";
        var master = new DraftTable();
        master.Fields.Add(new DraftField { FieldName = "leave_no", Caption = "No", DbType = FieldDbType.String, MaxLength = 20 });
        master.Fields.Add(new DraftField { FieldName = "start_date", Caption = "Start Date", CaptionZhTw = "開始日期", DbType = FieldDbType.Date });
        master.Fields.Add(new DraftField
        {
            FieldName = "leave_type", Caption = "Leave Type", CaptionZhTw = "假別", DbType = FieldDbType.String, MaxLength = 10,
            Options = { new DraftOption { Value = "annual", Text = "Annual" }, new DraftOption { Value = "sick", Text = "Sick" } },
        });
        master.Fields.Add(new DraftField
        {
            FieldName = "employee_rowid", Caption = "Employee", DbType = FieldDbType.Guid,
            Relation = new DraftRelation { ProgId = EmployeeProgId, Confidence = 1 },
        });
        draft.Tables.Add(master);
        return draft;
    }

    [Fact]
    [DisplayName("A converted draft becomes workbook content with a copy of the schema, its zh-TW texts and its menu placement")]
    public void ToContent_ConvertedDraft_CopiesSchemaTextsAndMenu()
    {
        var result = FormDraftConverter.Convert(LeaveRequest());

        var content = FormDraftImport.ToContent(result);
        FormSchemaConventions.Apply(content);

        Assert.Equal("LeaveRequest", content.Schema.ProgId);
        Assert.Equal("請假單", content.ZhTw[FormSchemaLocalizer.SchemaDisplayNameKey]);
        Assert.Equal("假別", content.ZhTw["Field.leave_type.Caption"]);
        Assert.Equal("hr", content.Menu.FolderId);
        Assert.Equal("HR", content.Menu.FolderCaption);
        Assert.Empty(content.FieldSources);
        // The conventions changed the copy, not the converted draft.
        Assert.False(string.IsNullOrEmpty(content.Schema.MasterTable!.DbTableName));
        Assert.True(string.IsNullOrEmpty(result.Schema.MasterTable!.DbTableName));
    }

    [Fact]
    [DisplayName("The host's catalog lists the solution's forms with their master fields and their zh-TW names")]
    public void GetCatalog_Solution_ListsFormsWithFieldsAndZhTwNames()
    {
        var host = new DefineEditorDraftHost(_solution, _define);

        var entry = Assert.Single(host.GetCatalog());
        Assert.Equal(EmployeeProgId, entry.ProgId);
        Assert.Equal("員工", entry.DisplayNameZhTw);
        Assert.Contains(entry.Fields, field => field.FieldName == "sys_id");
        Assert.Equal(entry.Fields.Select(f => f.FieldName), host.GetFields("employee").Select(f => f.FieldName));
        Assert.Empty(host.GetFields("Nothing"));
    }

    [Fact]
    [DisplayName("The host validates a draft with the checks of the workbook import")]
    public void Validate_DraftTheImportRefuses_ReportsTheImportsError()
    {
        var host = new DefineEditorDraftHost(_solution, _define);
        var draft = LeaveRequest();

        Assert.DoesNotContain(host.Validate(FormDraftConverter.Convert(draft)), i => i.Severity == DraftIssueSeverity.Error);

        draft.Tables[0].Fields[2].MaxLength = null;
        var issue = Assert.Single(host.Validate(FormDraftConverter.Convert(draft)), i => i.Severity == DraftIssueSeverity.Error);
        Assert.Contains("leave_type", issue.Location);
        Assert.Contains("max length", issue.Message);
        // Validation plans only; nothing is written.
        Assert.False(Directory.Exists(Path.Combine(_define, "FormLayout")));
    }

    [Fact]
    [DisplayName("The host reports a value expression the runtime cannot parse, such as a sum over another table")]
    public void Validate_ExpressionThatDoesNotParse_ReportsAnError()
    {
        var host = new DefineEditorDraftHost(_solution, _define);
        var draft = LeaveRequest();
        draft.Tables[0].Fields.Add(new DraftField { FieldName = "days", Caption = "Days", DbType = FieldDbType.Integer });
        draft.Tables[0].Fields.Add(new DraftField
        {
            FieldName = "total", Caption = "Total", DbType = FieldDbType.Decimal, ValueExpression = "SUM(LeaveRequestDetail.days)",
        });

        var issue = Assert.Single(host.Validate(FormDraftConverter.Convert(draft)), i => i.Severity == DraftIssueSeverity.Error);
        Assert.Contains("total", issue.Location);
        Assert.Contains("SUM(LeaveRequestDetail.days)", issue.Message);

        draft.Tables[0].Fields[^1].ValueExpression = "days * 8";
        Assert.DoesNotContain(host.Validate(FormDraftConverter.Convert(draft)), i => i.Severity == DraftIssueSeverity.Error);
    }

    [Fact]
    [DisplayName("The host parses expressions against the fields a relation brings")]
    public void Validate_ExpressionOverRelationField_Parses()
    {
        var host = new DefineEditorDraftHost(_solution, _define);
        var draft = LeaveRequest();
        draft.Tables[0].Fields.Add(new DraftField
        {
            FieldName = "employee_label", Caption = "Employee Label", DbType = FieldDbType.String, MaxLength = 100,
            ValueExpression = "ref_employee_id + \" \" + ref_employee_name",
        });

        Assert.DoesNotContain(host.Validate(FormDraftConverter.Convert(draft)), i => i.Severity == DraftIssueSeverity.Error);
    }

    [Fact]
    [DisplayName("The preview of a new form is the FormLayout the import writes for it")]
    public void BuildPreviewLayout_NewForm_EqualsTheImportedLayout()
    {
        var result = FormDraftConverter.Convert(LeaveRequest());

        var preview = FormDraftImport.BuildPreviewLayout(result, _define, zhTw: false);
        var plan = FormImporter.Plan(FormDraftImport.ToContent(result), new FormImportOptions { DefinePath = _define });

        Assert.False(plan.HasErrors, string.Join("\n", plan.Issues));
        var layoutFile = Assert.Single(plan.Files, f => FormWorkbookImport.IsFormLayout(f.Path));
        Assert.Equal(PlannedFileAction.Create, layoutFile.Action);
        Assert.Equal(layoutFile.Content, XmlCodec.Serialize(preview));
        // The relation brings the employee's name, which the conventions add and the form shows.
        Assert.Contains("ref_employee", layoutFile.Content);
    }

    [Fact]
    [DisplayName("The preview of a form that already has a FormLayout is that layout completed the way the import's merge mode writes it")]
    public void BuildPreviewLayout_ExistingLayout_EqualsTheMergedLayout()
    {
        var earlier = LeaveRequest();
        earlier.Tables[0].Fields.RemoveAt(2);
        var earlierPlan = FormImporter.Plan(FormDraftImport.ToContent(FormDraftConverter.Convert(earlier)),
            new FormImportOptions { DefinePath = _define });
        FormImporter.Apply(earlierPlan);

        var result = FormDraftConverter.Convert(LeaveRequest());
        var preview = FormDraftImport.BuildPreviewLayout(result, _define, zhTw: false);
        var plan = FormImporter.Plan(FormDraftImport.ToContent(result),
            new FormImportOptions { DefinePath = _define, LayoutMode = FormLayoutImportMode.Merge });

        var layoutFile = Assert.Single(plan.Files, f => FormWorkbookImport.IsFormLayout(f.Path));
        Assert.Equal(PlannedFileAction.Update, layoutFile.Action);
        Assert.Equal(layoutFile.Content, XmlCodec.Serialize(preview));
        Assert.Contains("leave_type", layoutFile.Content);
    }

    [Fact]
    [DisplayName("A draft without a ProgId and with undecided types still has a preview, in the language asked for")]
    public void BuildPreviewLayout_IncompleteDraft_DrawsUnderThePlaceholder()
    {
        var draft = new FormDraft { DisplayNameZhTw = "請假單" };
        var master = new DraftTable();
        master.Fields.Add(new DraftField { FieldName = "reason", Caption = "Reason", CaptionZhTw = "事由" });
        draft.Tables.Add(master);

        var preview = FormDraftImport.BuildPreviewLayout(FormDraftConverter.Convert(draft), _define, zhTw: true);

        Assert.Equal(FormDraftConverter.PlaceholderProgId, preview.LayoutId);
        var xml = XmlCodec.Serialize(preview);
        Assert.Contains("reason", xml);
        Assert.Contains("事由", xml);
    }

    [Fact]
    [DisplayName("The drafting window shows the model's reply, the drafted fields and the ready state, and imports the converted draft")]
    public async Task FormDraftingViewModel_ScriptedTurn_ShowsDraftAndImports()
    {
        var client = new ScriptedChatClient()
            .Then(Call("1", "set_form_info", """{"progId":"LeaveRequest","displayName":"Leave Request","displayNameZhTw":"請假單"}"""),
                Call("2", "upsert_field", """{"table":"master","fieldName":"leave_no","caption":"No","dbType":"String","maxLength":20}"""),
                Call("3", "upsert_field", """{"table":"master","fieldName":"leave_type","caption":"Leave Type","captionZhTw":"假別","dbType":"String","maxLength":10}"""))
            .Then(Call("4", "mark_ready"))
            .Then(Text("The draft is ready."));
        var host = new DefineEditorDraftHost(_solution, _define);
        FormDraftResult? imported = null;
        var viewModel = new FormDraftingViewModel(new FormDraftSession(client, host), host, _define, "leave.pdf", "test",
            result =>
            {
                imported = result;
                return Task.FromResult(true);
            });

        await viewModel.StartAsync([Text("a scanned leave request")]);

        Assert.Equal("The draft is ready.", viewModel.Messages[^1].Text);
        Assert.Equal(["leave_no", "leave_type"], viewModel.Fields.Select(f => f.FieldName));
        Assert.True(viewModel.IsReady);
        Assert.False(viewModel.IsBusy);
        Assert.DoesNotContain(viewModel.Issues, issue => issue.IsError);

        viewModel.RefreshPreview();
        Assert.NotNull(viewModel.PreviewLayout);
        Assert.False(viewModel.HasPreviewError);

        await viewModel.CreateCommand.ExecuteAsync(null);
        Assert.Equal("LeaveRequest", imported?.Schema.ProgId);
    }

    [Fact]
    [DisplayName("Editing a field in the Fields tab changes the draft, clears the ready state and reaches the model with the next message")]
    public async Task CommitFieldEdit_ChangedCaption_ChangesDraftAndTellsTheModel()
    {
        var client = new ScriptedChatClient()
            .Then(Call("1", "set_form_info", """{"progId":"LeaveRequest","displayName":"Leave Request"}"""),
                Call("2", "upsert_field", """{"table":"master","fieldName":"leave_type","caption":"Type","dbType":"String","maxLength":10}"""))
            .Then(Call("3", "mark_ready"))
            .Then(Text("Ready."))
            .Then(Text("Noted."));
        var host = new DefineEditorDraftHost(_solution, _define);
        var session = new FormDraftSession(client, host);
        var viewModel = new FormDraftingViewModel(session, host, _define, "leave.pdf", "test", _ => Task.FromResult(false));
        await viewModel.StartAsync([Text("a scanned leave request")]);
        Assert.True(viewModel.IsReady);

        var row = Assert.Single(viewModel.Fields);
        row.Caption = "Leave Type";
        viewModel.CommitFieldEdit(row);

        Assert.Equal("Leave Type", session.Draft.Tables[0].Fields[0].Caption);
        Assert.False(viewModel.IsReady);
        viewModel.InputText = "Thanks.";
        await viewModel.SendCommand.ExecuteAsync(null);
        Assert.Contains("caption of the field leave_type from \"Type\" to \"Leave Type\"", client.Requests[^1].Messages[^1].Text);
    }

    [Fact]
    [DisplayName("A failed turn is reported in the conversation and leaves the window usable")]
    public async Task FormDraftingViewModel_FailedTurn_ReportsTheFailure()
    {
        var client = new ScriptedChatClient().Then((_, _) => throw new HttpRequestException("network down"));
        var host = new DefineEditorDraftHost(_solution, _define);
        var viewModel = new FormDraftingViewModel(new FormDraftSession(client, host), host, _define, "leave.pdf", "test",
            _ => Task.FromResult(false));

        await viewModel.StartAsync([Text("a scanned leave request")]);

        var last = viewModel.Messages[^1];
        Assert.True(last.IsError);
        Assert.Contains("network down", last.Text);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    [DisplayName("A field picked in the preview is selected in the Fields tab")]
    public async Task RevealField_PreviewFieldName_SelectsTheRow()
    {
        var client = new ScriptedChatClient()
            .Then(Call("1", "upsert_field", """{"table":"master","fieldName":"Leave Type","caption":"Leave Type"}"""))
            .Then(Text("Drafted."));
        var host = new DefineEditorDraftHost(_solution, _define);
        var viewModel = new FormDraftingViewModel(new FormDraftSession(client, host), host, _define, "leave.pdf", "test",
            _ => Task.FromResult(false));
        await viewModel.StartAsync([Text("a scanned leave request")]);

        viewModel.RevealField("leave_type");

        Assert.Equal(1, viewModel.SelectedTabIndex);
        Assert.Same(viewModel.Fields[0], viewModel.SelectedField);
    }

    [Fact]
    [DisplayName("The Questions tab lists a relation the user has not confirmed, as the session does before it marks the draft ready")]
    public async Task Issues_UnconfirmedRelation_IsListedAsAnError()
    {
        var client = new ScriptedChatClient()
            .Then(Call("1", "set_form_info", """{"progId":"LeaveRequest","displayName":"Leave Request"}"""),
                Call("2", "upsert_field", """{"table":"master","fieldName":"deputy_rowid","caption":"Deputy","dbType":"Guid"}"""),
                Call("3", "set_relation", """{"table":"master","fieldName":"deputy_rowid","progId":"Employee","confidence":0.5,"reason":"A person."}"""))
            .Then(Text("Is the deputy an employee?"));
        var host = new DefineEditorDraftHost(_solution, _define);
        var viewModel = new FormDraftingViewModel(new FormDraftSession(client, host), host, _define, "leave.pdf", "test",
            _ => Task.FromResult(false));

        await viewModel.StartAsync([Text("a scanned leave request")]);

        var issue = Assert.Single(viewModel.Issues, i => i.IsError);
        Assert.Equal("deputy_rowid", issue.Location);
        Assert.Contains("not confirmed", issue.Message);
        Assert.False(viewModel.HasNoIssues);
    }
}
