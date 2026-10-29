using System.ComponentModel;
using System.Data;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Polhem.Api.Core.Messages.Form;
using Polhem.Api.Core.Messages.System;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Tests.Shared;
using Polhem.UI.Core;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Server.DependencyInjection;
using Polhem.Web.Blazor.Server.Identity;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// Renders <see cref="FormPage"/> with bUnit and opens a relation field's record from a viewed record: a
    /// double-click on the field opens a <see cref="RelatedRecordDialog"/> when the user may read the related form,
    /// names the form in a notice when they may not, and does nothing while the record is edited or the field is empty.
    /// </summary>
    /// <remarks>
    /// A <see cref="FakeApiServer"/> stands in for the backend of two forms: <c>Leave</c>, whose
    /// <c>leave_type_rowid</c> relates to <c>LeaveType</c>, and <c>LeaveType</c> itself. The page keeps the default
    /// definition loader. Each test pins <c>en-US</c> so the run does not depend on the machine's culture.
    /// </remarks>
    public class FormPageRelatedRecordTests : BunitContext
    {
        private const string LeaveProgId = "Leave";
        private const string LeaveTypeProgId = "LeaveType";
        private const string LeaveTypeModelId = "LeaveTypeModel";
        private const string RelationInput = "input#polhem-form-leave_type_rowid";
        private static readonly Guid s_leaveRowId = new("cccccccc-0000-0000-0000-000000000001");
        private static readonly Guid s_annualRowId = new("dddddddd-0000-0000-0000-000000000001");

        private static FormSchema BuildLeaveSchema()
        {
            var schema = new FormSchema(LeaveProgId, "Leave request") { ListFields = "sys_id" };
            var fields = schema.Tables!.Add(LeaveProgId, "Leave request").Fields!;
            fields.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            fields.Add("sys_id", "No.", FieldDbType.String);
            var leaveType = fields.Add("leave_type_rowid", "Leave type", FieldDbType.Guid);
            leaveType.RelationProgId = LeaveTypeProgId;
            leaveType.RelationFieldMappings!.Add("sys_name", "ref_leave_type_name");
            fields.Add("ref_leave_type_name", "Leave type name", FieldDbType.String);
            return schema;
        }

        private static FormSchema BuildLeaveTypeSchema(bool permissionBound)
        {
            var schema = new FormSchema(LeaveTypeProgId, "Leave type");
            if (permissionBound) { schema.PermissionModelId = LeaveTypeModelId; }
            var fields = schema.Tables!.Add(LeaveTypeProgId, "Leave type").Fields!;
            fields.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            fields.Add("sys_name", "Name", FieldDbType.String);
            return schema;
        }

        /// <summary>
        /// Serves both forms' definitions and records and records each form's <c>GetData</c> calls.
        /// </summary>
        private sealed class FakeBackend
        {
            private readonly FormSchema _leave = BuildLeaveSchema();
            private readonly FormSchema _leaveType;

            public FakeBackend(bool permissionBound, Guid? leaveTypeRowId)
            {
                _leaveType = BuildLeaveTypeSchema(permissionBound);
                Server = new FakeApiServer()
                    .On<GetListRequest>($"{LeaveProgId}.{FormActions.GetList}", _ => LeaveList())
                    .On<GetDataRequest>($"{LeaveProgId}.{FormActions.GetData}", request =>
                    {
                        LeaveLoaded.Add(request.RowId);
                        return new GetDataResponse { DataSet = LeaveRecord(leaveTypeRowId) };
                    })
                    .On<GetDataRequest>($"{LeaveTypeProgId}.{FormActions.GetData}", request =>
                    {
                        LeaveTypeLoaded.Add(request.RowId);
                        return new GetDataResponse { DataSet = LeaveTypeRecord(request.RowId) };
                    })
                    .On<GetDefineRequest>($"{SysProgIds.System}.{SystemActions.GetDefine}", GetDefine)
                    .On<GetFormLayoutRequest>($"{SysProgIds.System}.{SystemActions.GetCustomizeFormLayout}",
                        _ => new GetFormLayoutResponse())
                    .On<GetLanguageRequest>($"{SysProgIds.System}.{SystemActions.GetCustomizeLanguage}",
                        _ => new GetLanguageResponse())
                    .On<EnterCompanyRequest>($"{SysProgIds.System}.{SystemActions.EnterCompany}",
                        request => new EnterCompanyResponse
                        {
                            Company = new CompanyInfo { CompanyId = request.CompanyId },
                            Capabilities = new Dictionary<string, PermissionActions>(Capabilities, StringComparer.Ordinal),
                        });
            }

            public FakeApiServer Server { get; }
            public List<Guid> LeaveLoaded { get; } = [];
            public List<Guid> LeaveTypeLoaded { get; } = [];
            public Dictionary<string, PermissionActions> Capabilities { get; } = new(StringComparer.Ordinal);

            private static GetListResponse LeaveList()
            {
                var table = new DataTable(LeaveProgId);
                table.Columns.Add(SysFields.RowId, typeof(Guid));
                table.Columns.Add("sys_id", typeof(string));
                table.Rows.Add(s_leaveRowId, "L-001");
                return new GetListResponse { Table = table };
            }

            private DataSet LeaveRecord(Guid? leaveTypeRowId)
            {
                var dataSet = FormValueBinding.BuildEmptyDataSet(_leave);
                var master = dataSet.Tables[LeaveProgId]!;
                var row = master.NewRow();
                row[SysFields.RowId] = s_leaveRowId;
                row["sys_id"] = "L-001";
                row["leave_type_rowid"] = leaveTypeRowId is { } id ? id : Guid.Empty;
                row["ref_leave_type_name"] = leaveTypeRowId is null ? string.Empty : "Annual leave";
                master.Rows.Add(row);
                dataSet.AcceptChanges();
                return dataSet;
            }

            private DataSet LeaveTypeRecord(Guid rowId)
            {
                var dataSet = FormValueBinding.BuildEmptyDataSet(_leaveType);
                var master = dataSet.Tables[LeaveTypeProgId]!;
                var row = master.NewRow();
                row[SysFields.RowId] = rowId;
                row["sys_name"] = "Annual leave";
                master.Rows.Add(row);
                dataSet.AcceptChanges();
                return dataSet;
            }

            private GetDefineResponse GetDefine(GetDefineRequest request)
            {
                string progId = request.Keys is [var key, ..] ? key : string.Empty;
                var schema = progId == LeaveTypeProgId ? _leaveType : _leave;
                object? stored = request.DefineType switch
                {
                    DefineType.FormSchema => schema,
                    DefineType.FormLayout => FormLayoutGenerator.Generate(schema, progId),
                    DefineType.Language or DefineType.CurrencySettings or DefineType.UnitSettings => null,
                    _ => throw new NotSupportedException($"GetDefine for {request.DefineType} is not supported by the fake."),
                };
                return new GetDefineResponse { Xml = stored is null ? string.Empty : XmlCodec.Serialize(stored) };
            }
        }

        private FakeBackend RegisterBackend(bool permissionBound = false, Guid? leaveTypeRowId = null)
        {
            var backend = new FakeBackend(permissionBound, leaveTypeRowId ?? s_annualRowId);
            CompactViewportModule.Setup(JSInterop, false);
            Services.AddPolhemBlazor(options => options.UseLocalProvider());
            var server = backend.Server;
            Services.AddScoped(_ => server.CreateClient());
            return backend;
        }

        private string Text(string key)
            => PolhemUIText.Get(Services.GetRequiredService<IStringLocalizer<PolhemUIText>>(), key);

        private void Click(IRenderedComponent<FormPage> cut, string textKey)
            => cut.FindAll("div.polhem-form-page__toolbar button").Single(b => b.TextContent == Text(textKey)).Click();

        // Opens the only leave request with the View or Edit command and waits for the record.
        private IRenderedComponent<FormPage> OpenLeave(string commandKey)
        {
            var cut = Render<FormPage>(p => p.Add(c => c.ProgId, LeaveProgId));
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll("tr.polhem-dynamic-grid__row")));
            cut.Find("tr.polhem-dynamic-grid__row").Click();
            Click(cut, commandKey);
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(RelationInput)));
            return cut;
        }

        [Fact]
        [DisplayName("On a viewed record a relation field with a value shows the open hint and the hand-cursor class")]
        public void ViewedRecord_RelationField_ShowsOpenHint()
        {
            using var culture = new CultureScope("en-US");
            RegisterBackend();

            var cut = OpenLeave(PolhemUIText.View);

            var input = cut.Find(RelationInput);
            Assert.Equal(Text(PolhemUIText.OpenRelatedRecord), input.GetAttribute("title"));
            Assert.Contains("polhem-field-editor--opens-record", input.ClassList);
        }

        [Fact]
        [DisplayName("Double-clicking a relation field of a viewed record opens its record read-only in a dialog")]
        public void ViewedRecord_DoubleClickRelation_OpensRelatedRecordDialog()
        {
            using var culture = new CultureScope("en-US");
            var backend = RegisterBackend();
            var cut = OpenLeave(PolhemUIText.View);

            cut.Find(RelationInput).DoubleClick();

            cut.WaitForAssertion(() => Assert.Single(cut.FindAll("div.polhem-related-record-dialog div.polhem-form-layout")));
            Assert.Equal([s_annualRowId], backend.LeaveTypeLoaded);
            var dialog = cut.Find("div.polhem-related-record-dialog");
            Assert.Equal("Leave type", cut.Find("div.polhem-related-record-dialog__title").TextContent);
            Assert.All(dialog.QuerySelectorAll("input.polhem-field-editor"),
                input => Assert.True(input.HasAttribute("readonly")));
        }

        [Fact]
        [DisplayName("Closing the related record's dialog leaves the viewed record on screen without reloading it")]
        public void RelatedRecordDialog_Close_KeepsViewedRecord()
        {
            using var culture = new CultureScope("en-US");
            var backend = RegisterBackend();
            var cut = OpenLeave(PolhemUIText.View);
            cut.Find(RelationInput).DoubleClick();
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll("div.polhem-related-record-dialog div.polhem-form-layout")));

            cut.Find("div.polhem-related-record-dialog__toolbar button").Click();

            Assert.Empty(cut.FindAll("div.polhem-related-record-dialog"));
            Assert.Single(cut.FindAll(RelationInput));
            Assert.Single(backend.LeaveLoaded);
            Assert.Contains(cut.FindAll("div.polhem-form-page__toolbar button"), b => b.TextContent == Text(PolhemUIText.Back));
        }

        [Fact]
        [DisplayName("A user without Read on the related form gets a notice naming it, and its record is never requested")]
        public async Task ViewedRecord_RelatedFormNotReadable_ShowsNoticeWithoutLoading()
        {
            using var culture = new CultureScope("en-US");
            var backend = RegisterBackend(permissionBound: true);
            backend.Capabilities[LeaveTypeModelId] = PermissionActions.Create;
            await Services.GetRequiredService<CircuitUserContext>().EnterCompanyAsync("ACME");
            var cut = OpenLeave(PolhemUIText.View);

            cut.Find(RelationInput).DoubleClick();

            cut.WaitForAssertion(() => Assert.Equal(
                string.Format(System.Globalization.CultureInfo.InvariantCulture, Text(PolhemUIText.RelatedRecordDenied), "Leave type"),
                cut.Find("div.polhem-form-page__notice").TextContent));
            Assert.Empty(cut.FindAll("div.polhem-related-record-dialog"));
            Assert.Empty(backend.LeaveTypeLoaded);
        }

        [Fact]
        [DisplayName("A user with Read on the related form's model opens its record")]
        public async Task ViewedRecord_RelatedFormReadable_OpensDialog()
        {
            using var culture = new CultureScope("en-US");
            var backend = RegisterBackend(permissionBound: true);
            backend.Capabilities[LeaveTypeModelId] = PermissionActions.Read;
            await Services.GetRequiredService<CircuitUserContext>().EnterCompanyAsync("ACME");
            var cut = OpenLeave(PolhemUIText.View);

            cut.Find(RelationInput).DoubleClick();

            cut.WaitForAssertion(() => Assert.Single(cut.FindAll("div.polhem-related-record-dialog div.polhem-form-layout")));
            Assert.Equal([s_annualRowId], backend.LeaveTypeLoaded);
        }

        [Fact]
        [DisplayName("While the record is edited a double-click on a relation field opens nothing and shows no hint")]
        public void EditedRecord_DoubleClickRelation_OpensNothing()
        {
            using var culture = new CultureScope("en-US");
            var backend = RegisterBackend();
            var cut = OpenLeave(PolhemUIText.Edit);

            var input = cut.Find(RelationInput);
            Assert.Null(input.GetAttribute("title"));
            input.DoubleClick();

            Assert.Empty(cut.FindAll("div.polhem-related-record-dialog"));
            Assert.Empty(backend.LeaveTypeLoaded);
        }

        [Fact]
        [DisplayName("An empty relation field on a viewed record opens nothing and shows no hint")]
        public void ViewedRecord_EmptyRelation_OpensNothing()
        {
            using var culture = new CultureScope("en-US");
            var backend = RegisterBackend(leaveTypeRowId: Guid.Empty);
            var cut = OpenLeave(PolhemUIText.View);

            var input = cut.Find(RelationInput);
            Assert.Null(input.GetAttribute("title"));
            input.DoubleClick();

            Assert.Empty(cut.FindAll("div.polhem-related-record-dialog"));
            Assert.Empty(backend.LeaveTypeLoaded);
        }
    }
}
