using System.ComponentModel;
using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Logging;
using Polhem.Business.AuditLog;
using Polhem.Business.Form;
using Polhem.Business.UnitTests.Fakes;
using Polhem.Business.UnitTests.Form;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Logging;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.Repository.Abstractions.AuditLog;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.Form;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests.AuditLog
{
    /// <summary>
    /// <see cref="AuditRuleBusinessObject"/> after a save or delete of the audit-policy form: the local rule cache is
    /// evicted for the session's company, the change is announced to other processes, and a failed announcement is
    /// logged instead of failing the committed write.
    /// </summary>
    public class AuditRuleBusinessObjectInvalidationTests : IClassFixture<PolhemTestFixture>
    {
        private const string CompanyId = "aud1";

        private readonly PolhemTestFixture _fx;

        public AuditRuleBusinessObjectInvalidationTests(PolhemTestFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("Save in a company evicts that company's cached rules and announces the change")]
        public void Save_InCompany_EvictsAndAnnounces()
        {
            var rules = new RecordingRuleService();
            var notify = new NotifyRepository();

            Bo(rules, notify, CompanyId).Save(new SaveArgs { DataSet = NewDataSet() });

            Assert.Equal([CompanyId], rules.Removed);
            Assert.Equal([CompanyId], notify.Notified);
        }

        [Fact]
        [DisplayName("Delete in a company evicts that company's cached rules and announces the change")]
        public void Delete_InCompany_EvictsAndAnnounces()
        {
            var rules = new RecordingRuleService();
            var notify = new NotifyRepository();

            Bo(rules, notify, CompanyId).Delete(new DeleteArgs { RowId = Guid.NewGuid() });

            Assert.Equal([CompanyId], rules.Removed);
            Assert.Equal([CompanyId], notify.Notified);
        }

        [Fact]
        [DisplayName("Outside any company there is no rule cache to evict and nothing is announced")]
        public void Save_OutsideCompany_DoesNothing()
        {
            var rules = new RecordingRuleService();
            var notify = new NotifyRepository();

            Bo(rules, notify, companyId: string.Empty).Save(new SaveArgs { DataSet = NewDataSet() });

            Assert.Empty(rules.Removed);
            Assert.Empty(notify.Notified);
        }

        [Fact]
        [DisplayName("A database failure of the announcement is logged and the save still succeeds")]
        public void Save_NotifyThrowsDbException_LogsError()
        {
            var loggers = new RecordingLoggerFactory();
            var rules = new RecordingRuleService();
            var notify = new NotifyRepository { Failure = new FakeDbException() };

            var ex = Record.Exception(() => Bo(rules, notify, CompanyId, loggers).Save(new SaveArgs { DataSet = NewDataSet() }));

            Assert.Null(ex);
            Assert.Equal([CompanyId], rules.Removed);
            var error = Assert.Single(loggers.Entries, e => e.Level == LogLevel.Error);
            Assert.IsType<FakeDbException>(error.Exception);
            Assert.Contains(CompanyId, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A missing cache-notify database is logged and the delete still succeeds")]
        public void Delete_NotifyThrowsInvalidOperation_LogsError()
        {
            var loggers = new RecordingLoggerFactory();
            var notify = new NotifyRepository { Failure = new InvalidOperationException("No cache-notify database.") };

            var ex = Record.Exception(() => Bo(new RecordingRuleService(), notify, CompanyId, loggers)
                .Delete(new DeleteArgs { RowId = Guid.NewGuid() }));

            Assert.Null(ex);
            var error = Assert.Single(loggers.Entries, e => e.Level == LogLevel.Error);
            Assert.IsType<InvalidOperationException>(error.Exception);
        }

        [Fact]
        [DisplayName("A failed announcement with no logger registered is still not reported as a failure")]
        public void Save_NotifyFailsWithoutLogger_Completes()
        {
            var notify = new NotifyRepository { Failure = new InvalidOperationException("No cache-notify database.") };

            var ex = Record.Exception(() => Bo(new RecordingRuleService(), notify, CompanyId)
                .Save(new SaveArgs { DataSet = NewDataSet() }));

            Assert.Null(ex);
        }

        // -------- Fixtures --------

        private AuditRuleBusinessObject Bo(RecordingRuleService rules, NotifyRepository notify, string companyId,
            RecordingLoggerFactory? loggers = null)
        {
            var services = new List<(Type, object?)>
            {
                (typeof(IRepositoryFactory), new Factory(new StubFormRepository(), notify)),
                (typeof(IAuditRuleService), rules),
                (typeof(AuditLogOptions), null),
                (typeof(ICompanyInfoService), new StubCompanyInfoService()),
                (typeof(ILogger<AuditRuleBusinessObject>), loggers is null ? null : new Logger<AuditRuleBusinessObject>(loggers)),
            };
            var ctx = new BusinessObjectContext
            {
                DefineAccess = new FormSchemaOverlayDefineAccess(_fx.GetRequiredService<IDefineAccess>(), BuildSchema()),
                SessionInfoService = _fx.GetRequiredService<ISessionInfoService>(),
                LanguageService = _fx.GetRequiredService<ILanguageService>(),
                BoFactory = _fx.GetRequiredService<IBusinessObjectFactory>(),
                Services = new TestOverrideServiceProvider(_fx.Provider, [.. services]),
            };
            return new AuditRuleBusinessObject(ctx, TestSessionFactory.CreateAccessToken(_fx, companyId: companyId),
                SysProgIds.AuditRule);
        }

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema(SysProgIds.AuditRule, "Audit rules") { CategoryId = "company" };
            var master = schema.Tables!.Add(SysProgIds.AuditRule, "Audit rules");
            master.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            master.Fields.Add("prog_id", "Program", FieldDbType.String);
            return schema;
        }

        private static DataSet NewDataSet()
        {
            var ds = new DataSet();
            var table = ds.Tables.Add(SysProgIds.AuditRule);
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            table.Columns.Add("prog_id");
            table.Rows.Add(Guid.NewGuid(), "Orders");
            return ds;
        }

        private sealed class FakeDbException : DbException
        {
            public FakeDbException() : base("Cache-notify table unavailable.") { }
        }

        private sealed class RecordingRuleService : IAuditRuleService
        {
            public List<string> Removed { get; } = [];
            public CompanyAuditRules? Get(string companyId) => null;
            public void Remove(string companyId) => Removed.Add(companyId);
        }

        private sealed class NotifyRepository : IAuditRuleRepository
        {
            public Exception? Failure { get; set; }
            public List<string> Notified { get; } = [];
            public IReadOnlyList<AuditRule> GetRules(string databaseId, string companyId) => [];

            public void NotifyRulesChanged(string companyId)
            {
                if (Failure != null) { throw Failure; }
                Notified.Add(companyId);
            }
        }

        private sealed class Factory : IRepositoryFactory
        {
            private readonly IDataFormRepository _form;
            private readonly IAuditRuleRepository _rules;

            public Factory(IDataFormRepository form, IAuditRuleRepository rules) { _form = form; _rules = rules; }

            public T Create<T>(Guid accessToken = default) where T : class
                => typeof(T) == typeof(IAuditRuleRepository) ? (T)_rules : throw new NotSupportedException(typeof(T).Name);
            public T CreateFormRepository<T>(Guid accessToken, string progId) where T : class, IDataFormRepository => (T)_form;
            public T CreateFormRepository<T>(Guid accessToken, string progId, SessionCompanyScope companyScope) where T : class, IDataFormRepository => (T)_form;
        }
    }
}
