using System.ComponentModel;
using System.Data;
using Polhem.Business.Form;
using Polhem.Business.UnitTests.Fakes;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Logging;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// The audit decisions of <c>FormBusinessObject.Audit.cs</c> that the database tests do not reach: payloads whose
    /// master row cannot be read, a delete without a snapshot, a per-company rule, and the services a host may not
    /// register.
    /// </summary>
    /// <remarks>
    /// The repository is a stub, so each shape is built exactly as the audit code sees it.
    /// </remarks>
    public class FormBusinessObjectAuditEdgeTests : IClassFixture<PolhemTestFixture>
    {
        private const string ProgId = "AudEdgeForm";
        private const string DetailTable = "AudEdgeForm_Item";
        private const string CompanyId = "AUD01";

        private readonly PolhemTestFixture _fx;

        public FormBusinessObjectAuditEdgeTests(PolhemTestFixture fx) { _fx = fx; }

        private sealed class CapturingWriter : IAuditLogWriter
        {
            public List<AuditEntry> Entries { get; } = [];
            public void Write(AuditEntry entry) => Entries.Add(entry);
        }

        private sealed class FixedAuditRuleService : IAuditRuleService
        {
            private readonly CompanyAuditRules _rules;
            public FixedAuditRuleService(params AuditRule[] rules) { _rules = new CompanyAuditRules(CompanyId, rules); }
            public CompanyAuditRules? Get(string companyId) => _rules;
            public void Remove(string companyId) { }
        }

        [Fact]
        [DisplayName("Save of a deleted master row writes a Delete change entry keyed on its original rowid")]
        public void Save_DeletedMaster_WritesDeleteChange()
        {
            var writer = new CapturingWriter();
            var rowId = Guid.NewGuid();
            var ds = NewDataSet();
            ds.Tables[ProgId]!.Rows.Add(rowId, "m");
            ds.AcceptChanges();
            ds.Tables[ProgId]!.Rows[0].Delete();

            Bo(new StubFormRepository(), writer).Save(new SaveArgs { DataSet = ds });

            var entry = Assert.IsType<ChangeAuditEntry>(Assert.Single(writer.Entries));
            Assert.Equal(ChangeKind.Delete, entry.ChangeKind);
            Assert.Equal(rowId.ToString(), entry.RowKey);
        }

        [Fact]
        [DisplayName("Save of a payload without the master table writes an Update change entry with no row key")]
        public void Save_NoMasterTable_WritesUpdateWithoutRowKey()
        {
            var writer = new CapturingWriter();
            var ds = new DataSet();
            var detail = AddDetailTable(ds);
            detail.Rows.Add(Guid.NewGuid(), Guid.NewGuid());

            Bo(new StubFormRepository(), writer).Save(new SaveArgs { DataSet = ds });

            var entry = Assert.IsType<ChangeAuditEntry>(Assert.Single(writer.Entries));
            Assert.Equal(ChangeKind.Update, entry.ChangeKind);
            Assert.Null(entry.RowKey);
        }

        [Fact]
        [DisplayName("Save of a master table with no rows writes an Update change entry with no row key")]
        public void Save_EmptyMasterTable_WritesUpdateWithoutRowKey()
        {
            var writer = new CapturingWriter();
            var ds = NewDataSet();
            ds.Tables[DetailTable]!.Rows.Add(Guid.NewGuid(), Guid.NewGuid());

            Bo(new StubFormRepository(), writer).Save(new SaveArgs { DataSet = ds });

            var entry = Assert.IsType<ChangeAuditEntry>(Assert.Single(writer.Entries));
            Assert.Equal(ChangeKind.Update, entry.ChangeKind);
            Assert.Null(entry.RowKey);
        }

        [Fact]
        [DisplayName("Delete without a snapshot records the deleted key alone")]
        public void Delete_NoSnapshot_WritesMinimalDeleteXml()
        {
            var writer = new CapturingWriter();
            var rowId = Guid.NewGuid();

            Bo(new StubFormRepository { Snapshot = null }, writer).Delete(new DeleteArgs { RowId = rowId });

            var entry = Assert.IsType<ChangeAuditEntry>(Assert.Single(writer.Entries));
            Assert.Equal(ChangeKind.Delete, entry.ChangeKind);
            Assert.Contains($"sys_rowid=\"{rowId}\"", entry.ChangesXml);
            Assert.StartsWith("<DeletedRow", entry.ChangesXml);
        }

        [Fact]
        [DisplayName("Delete with a snapshot that holds no rows records the deleted key alone")]
        public void Delete_EmptySnapshot_WritesMinimalDeleteXml()
        {
            var writer = new CapturingWriter();
            var rowId = Guid.NewGuid();

            Bo(new StubFormRepository { Snapshot = NewDataSet() }, writer).Delete(new DeleteArgs { RowId = rowId });

            var entry = Assert.IsType<ChangeAuditEntry>(Assert.Single(writer.Entries));
            Assert.StartsWith("<DeletedRow", entry.ChangesXml);
            Assert.Contains($"table=\"{ProgId}\"", entry.ChangesXml);
        }

        [Fact]
        [DisplayName("Without a registered audit writer, Save completes and nothing is recorded")]
        public void Save_NoAuditWriter_Completes()
        {
            var repo = new StubFormRepository();
            var ds = NewDataSet();
            ds.Tables[ProgId]!.Rows.Add(Guid.NewGuid(), "m");

            var ex = Record.Exception(() => Bo(repo, writer: null).Save(new SaveArgs { DataSet = ds }));

            Assert.Null(ex);
            Assert.Single(repo.Saved);
        }

        [Fact]
        [DisplayName("Without registered audit options, neither Save nor GetData records anything")]
        public void SaveAndGetData_NoAuditOptions_RecordNothing()
        {
            var writer = new CapturingWriter();
            var ds = NewDataSet();
            ds.Tables[ProgId]!.Rows.Add(Guid.NewGuid(), "m");
            var bo = Bo(new StubFormRepository { Snapshot = NewDataSet() }, writer, registerOptions: false);

            bo.Save(new SaveArgs { DataSet = ds });
            bo.GetData(new GetDataArgs { RowId = Guid.NewGuid() });

            Assert.Empty(writer.Entries);
        }

        [Fact]
        [DisplayName("GetData records an access entry when access auditing is on and a record is read")]
        public void GetData_AccessEnabled_WritesAccessEntry()
        {
            var writer = new CapturingWriter();
            var rowId = Guid.NewGuid();

            Bo(new StubFormRepository { Snapshot = NewDataSet() }, writer).GetData(new GetDataArgs { RowId = rowId });

            var entry = Assert.IsType<AccessAuditEntry>(Assert.Single(writer.Entries));
            Assert.Equal(rowId.ToString(), entry.RowKey);
            Assert.Equal($"{ProgId}.GetData", entry.Source);
        }

        [Fact]
        [DisplayName("In a company, a rule that switches change auditing off for the form wins over the deployment default")]
        public void Save_CompanyRuleOff_RecordsNothing()
        {
            var writer = new CapturingWriter();
            var rules = new FixedAuditRuleService(new AuditRule(ProgId, AuditRuleMode.Off, AuditRuleMode.Off, IsSensitive: false));
            var ds = NewDataSet();
            ds.Tables[ProgId]!.Rows.Add(Guid.NewGuid(), "m");

            Bo(new StubFormRepository(), writer, companyId: CompanyId, rules: rules).Save(new SaveArgs { DataSet = ds });

            Assert.Empty(writer.Entries);
        }

        [Fact]
        [DisplayName("In a company, a sensitive rule marks the change entry sensitive and names the company")]
        public void Save_CompanySensitiveRule_MarksEntrySensitive()
        {
            var writer = new CapturingWriter();
            var rules = new FixedAuditRuleService(new AuditRule(ProgId, AuditRuleMode.On, AuditRuleMode.Inherit, IsSensitive: true));
            var ds = NewDataSet();
            ds.Tables[ProgId]!.Rows.Add(Guid.NewGuid(), "m");

            Bo(new StubFormRepository(), writer, companyId: CompanyId, rules: rules).Save(new SaveArgs { DataSet = ds });

            var entry = Assert.IsType<ChangeAuditEntry>(Assert.Single(writer.Entries));
            Assert.True(entry.IsSensitive);
            Assert.Equal(CompanyId, entry.CompanyId);
        }

        [Fact]
        [DisplayName("In a company with no audit rule service registered, the deployment defaults apply")]
        public void Save_CompanyWithoutRuleService_UsesDefaults()
        {
            var writer = new CapturingWriter();
            var ds = NewDataSet();
            ds.Tables[ProgId]!.Rows.Add(Guid.NewGuid(), "m");

            Bo(new StubFormRepository(), writer, companyId: CompanyId, rules: null).Save(new SaveArgs { DataSet = ds });

            var entry = Assert.IsType<ChangeAuditEntry>(Assert.Single(writer.Entries));
            Assert.False(entry.IsSensitive);
        }

        // -------- Fixtures --------

        private FormBusinessObject Bo(StubFormRepository repo, IAuditLogWriter? writer, bool registerOptions = true,
            string companyId = "", IAuditRuleService? rules = null)
        {
            var services = new List<(Type, object?)>
            {
                (typeof(IRepositoryFactory), new StubRepositoryFactory(repo)),
                (typeof(IAuditLogWriter), writer),
                (typeof(IAuditRuleService), rules),
                (typeof(ICompanyInfoService), new StubCompanyInfoService()),
                (typeof(AuditLogOptions), registerOptions
                    ? new AuditLogOptions { Enabled = true, ChangeEnabled = true, AccessEnabled = true }
                    : null),
            };
            var ctx = new BusinessObjectContext
            {
                DefineAccess = new FormSchemaOverlayDefineAccess(_fx.GetRequiredService<IDefineAccess>(), BuildSchema()),
                SessionInfoService = _fx.GetRequiredService<ISessionInfoService>(),
                LanguageService = _fx.GetRequiredService<ILanguageService>(),
                BoFactory = _fx.GetRequiredService<IBusinessObjectFactory>(),
                Services = new TestOverrideServiceProvider(_fx.Provider, [.. services]),
            };
            return new FormBusinessObject(ctx, TestSessionFactory.CreateAccessToken(_fx, companyId: companyId), ProgId);
        }

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema(ProgId, "Audit edges") { CategoryId = "company" };
            var master = schema.Tables!.Add(ProgId, "Audit edges");
            master.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            master.Fields.Add("sys_id", "ID", FieldDbType.String);
            var detail = schema.Tables.Add(DetailTable, "Item");
            detail.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            detail.Fields.Add(SysFields.MasterRowId, "Master Row ID", FieldDbType.Guid);
            return schema;
        }

        private static DataSet NewDataSet()
        {
            var ds = new DataSet();
            var master = ds.Tables.Add(ProgId);
            master.Columns.Add(SysFields.RowId, typeof(Guid));
            master.Columns.Add("sys_id");
            AddDetailTable(ds);
            return ds;
        }

        private static DataTable AddDetailTable(DataSet ds)
        {
            var detail = ds.Tables.Add(DetailTable);
            detail.Columns.Add(SysFields.RowId, typeof(Guid));
            detail.Columns.Add(SysFields.MasterRowId, typeof(Guid));
            return detail;
        }
    }
}
