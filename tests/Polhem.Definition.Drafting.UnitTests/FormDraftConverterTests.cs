using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.Drafting.UnitTests
{
    public class FormDraftConverterTests
    {
        [Fact]
        [DisplayName("A complete draft converts into a schema whose master table is named after the ProgId, with no issues")]
        public void Convert_CompleteDraft_BuildsSchemaWithoutIssues()
        {
            var draft = LeaveRequest();

            var result = FormDraftConverter.Convert(draft);

            Assert.Empty(result.Issues);
            Assert.Equal("LeaveRequest", result.Schema.ProgId);
            Assert.Equal("Leave Request", result.Schema.DisplayName);
            var master = result.Schema.MasterTable;
            Assert.NotNull(master);
            Assert.Equal("Leave Request", master.DisplayName);
            Assert.Equal(["applicant", "leave_type", "start_date", "reason"], master.Fields!.Select(f => f.FieldName));
        }

        [Fact]
        [DisplayName("Types, lengths, editors and the required flag are copied from the draft")]
        public void Convert_DeclaredField_CopiesTypeLengthEditorAndRequired()
        {
            var result = FormDraftConverter.Convert(LeaveRequest());

            var reason = result.Schema.FindField("reason")!;
            Assert.Equal(FieldDbType.String, reason.DbType);
            Assert.Equal(200, reason.MaxLength);
            Assert.Equal(ControlType.MemoEdit, reason.ControlType);
            Assert.True(reason.Required);
            var start = result.Schema.FindField("start_date")!;
            Assert.Equal(FieldDbType.Date, start.DbType);
            Assert.Equal(0, start.MaxLength);
        }

        [Fact]
        [DisplayName("Options become list items and make the field a drop-down")]
        public void Convert_FieldWithOptions_BuildsDropDownListItems()
        {
            var result = FormDraftConverter.Convert(LeaveRequest());

            var leaveType = result.Schema.FindField("leave_type")!;
            Assert.Equal(ControlType.DropDownEdit, leaveType.ControlType);
            Assert.Equal(["annual", "sick"], leaveType.ListItems!.Select(item => item.Value));
            Assert.Equal(["Annual leave", "sick"], leaveType.ListItems!.Select(item => item.Text));
        }

        [Fact]
        [DisplayName("An empty or repeated option value is left out and reported")]
        public void Convert_BadOptions_AreLeftOutWithErrors()
        {
            var draft = LeaveRequest();
            var options = draft.Tables[0].Fields[1].Options;
            options.Add(new DraftOption { Value = " " });
            options.Add(new DraftOption { Value = "annual", Text = "Again" });

            var result = FormDraftConverter.Convert(draft);

            Assert.Equal(2, result.Schema.FindField("leave_type")!.ListItems!.Count);
            Assert.Equal(2, result.Issues.Count(issue => issue.Severity == DraftIssueSeverity.Error));
        }

        [Fact]
        [DisplayName("A relation sets only RelationProgId; the reference fields are left to the host's conventions")]
        public void Convert_Relation_SetsRelationProgIdOnly()
        {
            var result = FormDraftConverter.Convert(LeaveRequest());

            var applicant = result.Schema.FindField("applicant")!;
            Assert.Equal("Employee", applicant.RelationProgId);
            Assert.Empty(applicant.RelationFieldMappings ?? []);
            Assert.DoesNotContain(result.Schema.MasterTable!.Fields!, f => f.FieldName.StartsWith("ref_", StringComparison.Ordinal));
            Assert.DoesNotContain(result.Schema.MasterTable!.Fields!, f => f.FieldName.StartsWith("sys_", StringComparison.Ordinal));
            Assert.Equal(string.Empty, result.Schema.CategoryId);
            Assert.Equal(string.Empty, result.Schema.MasterTable!.DbTableName);
        }

        [Fact]
        [DisplayName("A computed field keeps its expression and is read-only")]
        public void Convert_ComputedField_IsReadOnly()
        {
            var draft = Order();

            var result = FormDraftConverter.Convert(draft);

            var amount = result.Schema.FindField("amount", "OrderLine")!;
            Assert.Equal("quantity * unit_price", amount.ValueExpression);
            Assert.True(amount.ReadOnly);
            Assert.False(result.Schema.FindField("quantity", "OrderLine")!.ReadOnly);
        }

        [Fact]
        [DisplayName("Detail tables follow the master table under their own PascalCase names")]
        public void Convert_DetailTable_IsAddedAfterMaster()
        {
            var result = FormDraftConverter.Convert(Order());

            Assert.Equal(["Order", "OrderLine"], result.Schema.Tables!.Select(t => t.TableName));
            Assert.Equal("Order Line", result.Schema.Tables!["OrderLine"].DisplayName);
        }

        [Fact]
        [DisplayName("The zh-TW texts are keyed by the FormSchemaLocalizer sub-keys")]
        public void Convert_ZhTwTexts_UseLocalizerSubKeys()
        {
            var result = FormDraftConverter.Convert(Order());

            Assert.Equal("訂購單", result.ZhTw["Schema.DisplayName"]);
            Assert.Equal("訂購單", result.ZhTw["Table.Order.DisplayName"]);
            Assert.Equal("訂購明細", result.ZhTw["Table.OrderLine.DisplayName"]);
            Assert.Equal("數量", result.ZhTw["Field.quantity.Caption"]);
            Assert.False(result.ZhTw.ContainsKey("Field.amount.Caption"));
        }

        [Fact]
        [DisplayName("The menu placement is copied to the result")]
        public void Convert_Menu_IsCopied()
        {
            var draft = LeaveRequest();
            draft.Menu.FolderId = "HumanResources";
            draft.Menu.FolderCaption = "Human Resources";
            draft.Menu.FolderCaptionZhTw = "人事";
            draft.Menu.EntryCaption = "Leave Request";
            draft.Menu.EntryCaptionZhTw = "請假單";

            var menu = FormDraftConverter.Convert(draft).Menu;

            Assert.Equal("HumanResources", menu.FolderId);
            Assert.Equal("Human Resources", menu.FolderCaption);
            Assert.Equal("人事", menu.FolderCaptionZhTw);
            Assert.Equal("Leave Request", menu.EntryCaption);
            Assert.Equal("請假單", menu.EntryCaptionZhTw);
        }

        [Fact]
        [DisplayName("The source quote of each field is kept, keyed by table and field name")]
        public void Convert_SourceQuotes_AreKeptByTableAndField()
        {
            var result = FormDraftConverter.Convert(LeaveRequest());

            Assert.Equal("請假事由", result.FieldSources["LeaveRequest.reason"]);
            Assert.False(result.FieldSources.ContainsKey("LeaveRequest.start_date"));
        }

        [Fact]
        [DisplayName("An empty draft still converts into a previewable schema with a placeholder ProgId and warnings")]
        public void Convert_EmptyDraft_ConvertsWithPlaceholder()
        {
            var result = FormDraftConverter.Convert(new FormDraft());

            Assert.Equal(FormDraftConverter.PlaceholderProgId, result.Schema.ProgId);
            Assert.NotNull(result.Schema.MasterTable);
            Assert.NotEmpty(result.Issues);
            Assert.All(result.Issues, issue => Assert.Equal(DraftIssueSeverity.Warning, issue.Severity));
        }

        [Fact]
        [DisplayName("Undecided names, types and lengths are filled with defaults and reported as warnings")]
        public void Convert_IncompleteField_FillsDefaultsWithWarnings()
        {
            var draft = new FormDraft { ProgId = "Visit", DisplayName = "Visit" };
            draft.Tables.Add(new DraftTable());
            draft.Tables[0].Fields.Add(new DraftField { Caption = "訪客姓名" });
            draft.Tables[0].Fields.Add(new DraftField { FieldName = "visit_date", Caption = "Visit date", ControlType = ControlType.DateEdit });
            draft.Tables[0].Fields.Add(new DraftField { FieldName = "host", Caption = "Host", DbType = FieldDbType.String, MaxLength = 50, Relation = new DraftRelation() });
            draft.Tables.Add(new DraftTable());

            var result = FormDraftConverter.Convert(draft);

            var placeholder = result.Schema.FindField("field_1")!;
            Assert.Equal(FieldDbType.String, placeholder.DbType);
            Assert.Equal(FieldDbType.Date, result.Schema.FindField("visit_date")!.DbType);
            Assert.Equal(string.Empty, result.Schema.FindField("host")!.RelationProgId);
            Assert.Equal("VisitDetail1", result.Schema.Tables![1].TableName);
            Assert.All(result.Issues, issue => Assert.Equal(DraftIssueSeverity.Warning, issue.Severity));
            Assert.Contains(result.Issues, issue => issue.Location == "Visit.field_1" && issue.Message.Contains("maximum length", StringComparison.Ordinal));
            Assert.Contains(result.Issues, issue => issue.Location == "Visit.visit_date" && issue.Message.Contains("previewed as Date", StringComparison.Ordinal));
            Assert.Contains(result.Issues, issue => issue.Location == "Visit.host" && issue.Message.Contains("not decided", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData(ControlType.CheckEdit, FieldDbType.Boolean)]
        [InlineData(ControlType.NumericEdit, FieldDbType.Decimal)]
        [InlineData(ControlType.MemoEdit, FieldDbType.Text)]
        [InlineData(ControlType.DateTimeEdit, FieldDbType.DateTime)]
        [InlineData(ControlType.TimeEdit, FieldDbType.Time)]
        [InlineData(ControlType.TextEdit, FieldDbType.String)]
        [DisplayName("An undecided type is inferred from the editor")]
        public void Convert_UndecidedType_IsInferredFromEditor(ControlType controlType, FieldDbType expected)
        {
            var draft = new FormDraft { ProgId = "Sample", DisplayName = "Sample" };
            draft.Tables.Add(new DraftTable());
            draft.Tables[0].Fields.Add(new DraftField { FieldName = "value", Caption = "Value", ControlType = controlType });

            var field = FormDraftConverter.Convert(draft).Schema.FindField("value")!;

            Assert.Equal(expected, field.DbType);
        }

        [Fact]
        [DisplayName("An Unknown type is reported as an error and previewed as a string")]
        public void Convert_UnknownType_IsAnError()
        {
            var draft = new FormDraft { ProgId = "Sample", DisplayName = "Sample" };
            draft.Tables.Add(new DraftTable());
            draft.Tables[0].Fields.Add(new DraftField { FieldName = "value", Caption = "Value", DbType = FieldDbType.Unknown, MaxLength = 10 });

            var result = FormDraftConverter.Convert(draft);

            Assert.Equal(FieldDbType.String, result.Schema.FindField("value")!.DbType);
            Assert.Contains(result.Issues, issue => issue.Severity == DraftIssueSeverity.Error && issue.Location == "Sample.value");
        }

        [Fact]
        [DisplayName("A duplicate field, a reserved prefix and a non-positive length are reported as errors")]
        public void Convert_InvalidFields_AreReportedAsErrors()
        {
            var draft = new FormDraft { ProgId = "Sample", DisplayName = "Sample" };
            draft.Tables.Add(new DraftTable());
            var fields = draft.Tables[0].Fields;
            fields.Add(new DraftField { FieldName = "Name", Caption = "Name", DbType = FieldDbType.String, MaxLength = 0 });
            fields.Add(new DraftField { FieldName = "name", Caption = "Again", DbType = FieldDbType.String, MaxLength = 10 });
            fields.Add(new DraftField { FieldName = "sys_id", Caption = "Id", DbType = FieldDbType.String, MaxLength = 10 });
            fields.Add(new DraftField { FieldName = "ref_customer_name", Caption = "Customer", DbType = FieldDbType.String, MaxLength = 10 });

            var result = FormDraftConverter.Convert(draft);

            Assert.Equal(["name"], result.Schema.MasterTable!.Fields!.Select(f => f.FieldName));
            Assert.Equal(4, result.Issues.Count(issue => issue.Severity == DraftIssueSeverity.Error));
        }

        [Fact]
        [DisplayName("Names proposed in other forms are normalized: the ProgId in PascalCase, fields in snake_case")]
        public void Convert_Names_AreNormalized()
        {
            var draft = new FormDraft { ProgId = "leave request", DisplayName = "Leave Request" };
            draft.Tables.Add(new DraftTable { TableName = "ignored for the master" });
            draft.Tables[0].Fields.Add(new DraftField { FieldName = "Start Date", Caption = "Start", DbType = FieldDbType.Date });
            draft.Tables.Add(new DraftTable { TableName = "leave_day" });

            var result = FormDraftConverter.Convert(draft);

            Assert.Equal("LeaveRequest", result.Schema.ProgId);
            Assert.Equal(["LeaveRequest", "LeaveDay"], result.Schema.Tables!.Select(t => t.TableName));
            Assert.NotNull(result.Schema.FindField("start_date"));
        }

        [Fact]
        [DisplayName("Converting does not change the draft")]
        public void Convert_DoesNotChangeDraft()
        {
            var draft = new FormDraft { ProgId = " leave request ", DisplayName = " Leave " };
            draft.Tables.Add(new DraftTable());
            draft.Tables[0].Fields.Add(new DraftField { FieldName = "Leave Type", Caption = " Type " });

            FormDraftConverter.Convert(draft);

            Assert.Equal(" leave request ", draft.ProgId);
            Assert.Equal("Leave Type", draft.Tables[0].Fields[0].FieldName);
            Assert.Null(draft.Tables[0].Fields[0].DbType);
        }

        private static FormDraft LeaveRequest()
        {
            var draft = new FormDraft { ProgId = "LeaveRequest", DisplayName = "Leave Request", DisplayNameZhTw = "請假單" };
            var master = new DraftTable();
            master.Fields.Add(new DraftField
            {
                FieldName = "applicant", Caption = "Applicant", CaptionZhTw = "申請人", DbType = FieldDbType.String, MaxLength = 20,
                Required = true, Relation = new DraftRelation { ProgId = "Employee", Confidence = 0.9, Reason = "An employee applies." },
            });
            var leaveType = new DraftField { FieldName = "leave_type", Caption = "Leave type", DbType = FieldDbType.String, MaxLength = 10 };
            leaveType.Options.Add(new DraftOption { Value = "annual", Text = "Annual leave" });
            leaveType.Options.Add(new DraftOption { Value = "sick" });
            master.Fields.Add(leaveType);
            master.Fields.Add(new DraftField { FieldName = "start_date", Caption = "Start date", DbType = FieldDbType.Date });
            master.Fields.Add(new DraftField
            {
                FieldName = "reason", Caption = "Reason", DbType = FieldDbType.String, MaxLength = 200,
                ControlType = ControlType.MemoEdit, Required = true, SourceQuote = "請假事由",
            });
            draft.Tables.Add(master);
            return draft;
        }

        private static FormDraft Order()
        {
            var draft = new FormDraft { ProgId = "Order", DisplayName = "Order", DisplayNameZhTw = "訂購單" };
            var master = new DraftTable { DisplayNameZhTw = "訂購單" };
            master.Fields.Add(new DraftField { FieldName = "order_date", Caption = "Order date", DbType = FieldDbType.Date });
            draft.Tables.Add(master);
            var lines = new DraftTable { TableName = "OrderLine", DisplayName = "Order Line", DisplayNameZhTw = "訂購明細" };
            lines.Fields.Add(new DraftField { FieldName = "quantity", Caption = "Quantity", CaptionZhTw = "數量", DbType = FieldDbType.Decimal });
            lines.Fields.Add(new DraftField { FieldName = "unit_price", Caption = "Unit price", DbType = FieldDbType.Decimal });
            lines.Fields.Add(new DraftField { FieldName = "amount", Caption = "Amount", DbType = FieldDbType.Decimal, ValueExpression = " quantity * unit_price " });
            draft.Tables.Add(lines);
            return draft;
        }
    }
}
