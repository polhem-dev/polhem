using System.ComponentModel;
using Polhem.Business.AuditLog;
using Polhem.Business.Form;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Identity;
using Polhem.Definition.Logging;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.Repository.Abstractions.AuditLog;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.Form;
using Polhem.Repository.Form;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// The audit rule maintenance form itself (<c>AuditRule</c> / <c>st_audit_rule</c>): policy changes always leave a trail marked sensitive,
    /// and saving clears that company's rule cache.
    /// </summary>
    public class AuditRuleFormTests : IClassFixture<SharedDbFixture>
    {
        private const string CompanyId = "AUDITFORM";

        private readonly SharedDbFixture _fx;
        private readonly DataFormRepository _repository;

        public AuditRuleFormTests(SharedDbFixture fx)
        {
            _fx = fx;
            string databaseId = TestDbConventions.GetDatabaseId(DatabaseType.SQLite, "company");
            var defineAccess = fx.GetRequiredService<IDefineAccess>();
            _repository = new DataFormRepository(
                TestRepositoryContext.Create(
                    fx.GetRequiredService<IDbConnectionManager>(),
                    defineAccess: defineAccess,
                    dbAccessFactory: fx.GetRequiredService<IDbAccessFactory>()),
                SysProgIds.AuditRule,
                defineAccess.GetFormSchema(SysProgIds.AuditRule),
                databaseId);
        }

        private sealed class CapturingAuditLogWriter : IAuditLogWriter
        {
            public List<AuditEntry> Entries { get; } = [];
            public void Write(AuditEntry entry) => Entries.Add(entry);
        }

        /// <summary>
        /// Reports whether Remove was called, and answers queries with the rules the test specifies.
        /// </summary>
        private sealed class RecordingAuditRuleService : IAuditRuleService
        {
            private readonly CompanyAuditRules _rules;
            public RecordingAuditRuleService(AuditRule rule)
                => _rules = new CompanyAuditRules(CompanyId, [rule]);
            public List<string> Removed { get; } = [];
            public CompanyAuditRules? Get(string companyId) => _rules;
            public void Remove(string companyId) => Removed.Add(companyId);
        }

        /// <summary>
        /// Allows every action. The maintenance form declares a PermissionModelId and enforcement is fail-closed,
        /// so without this fake both tests would stop at ForbiddenException and never reach what they check.
        /// That necessity is itself indirect evidence that the policy form really is permission-gated.
        /// </summary>
        private sealed class AllowAllAuthorization : ICompanyAuthorizationService
        {
            public bool Can(Guid accessToken, string modelId, PermissionAction action) => true;
        }

        /// <summary>
        /// Grants every record scope, the scope-layer counterpart of <see cref="AllowAllAuthorization"/>. The test
        /// session has no roles, so the real resolver would deny every scope and a new rule could not be saved.
        /// </summary>
        private sealed class UnrestrictedScopeResolver : IScopeResolver
        {
            public Polhem.Definition.Filters.FilterNode? ResolveFilter(
                Guid accessToken, string modelId, PermissionAction action, Polhem.Definition.Forms.FormSchema formSchema) => null;
        }

        private sealed class StubFactory : IRepositoryFactory
        {
            private readonly IDataFormRepository _repository;
            private readonly IAuditRuleRepository? _auditRules;
            public StubFactory(IDataFormRepository repository, IAuditRuleRepository? auditRules)
            {
                _repository = repository;
                _auditRules = auditRules;
            }
            public T CreateFormRepository<T>(Guid accessToken, string progId) where T : class, IDataFormRepository
                => (T)_repository;
            public T Create<T>(Guid accessToken = default) where T : class
                => _auditRules as T ?? throw new NotSupportedException();
        }

        /// <summary>A notifier that does nothing: this test verifies the trail and the cache clearing, not the cross-node announcement.</summary>
        private sealed class NoOpAuditRuleRepository : IAuditRuleRepository
        {
            public List<string> Notified { get; } = [];
            public IReadOnlyList<AuditRule> GetRules(string databaseId) => [];
            public void NotifyRulesChanged(string companyId) => Notified.Add(companyId);
        }

        private Guid CreateSessionToken()
        {
            var accessToken = Guid.NewGuid();
            _fx.GetRequiredService<ISessionInfoService>().Set(new SessionInfo
            {
                AccessToken = accessToken,
                UserId = "audit_form_test",
                UserName = "audit_form_test",
                CompanyId = CompanyId,
                ExpiredAt = DateTime.UtcNow.AddHours(1),
                ApiEncryptionKey = [],
            });
            return accessToken;
        }

        private AuditRuleBusinessObject CreateBo(
            Guid accessToken, CapturingAuditLogWriter writer,
            IAuditRuleService ruleService, IAuditRuleRepository auditRules)
        {
            var ctx = TestPolhemContext.CreateWithOverrides(_fx,
                (typeof(IRepositoryFactory), new StubFactory(_repository, auditRules)),
                (typeof(AuditLogOptions), new AuditLogOptions
                {
                    Enabled = true,
                    // Both axes' deployment defaults are off, so any record written from here on can only come from the exemption.
                    ChangeEnabled = false,
                    AccessEnabled = false,
                }),
                (typeof(IAuditLogWriter), writer),
                (typeof(IAuditRuleService), ruleService),
                (typeof(ICompanyAuthorizationService), new AllowAllAuthorization()),
                (typeof(IScopeResolver), new UnrestrictedScopeResolver()));
            return new AuditRuleBusinessObject(ctx, accessToken, SysProgIds.AuditRule);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("The policy form is not governed by the rule table: with the rule Off and the deployment default off, it still leaves a trail marked sensitive")]
        public void Save_PolicyFormIsExemptFromItsOwnRule()
        {
            // Regression test for the rule that auditing cannot be turned off by the audit policy. Without this exemption, anyone who can maintain rules
            // could set the `AuditRule` row to Off, and every later policy change would leave no trace. The whole audit could
            // be quietly switched off by itself, with no record that it ever happened.
            var writer = new CapturingAuditLogWriter();
            var ruleService = new RecordingAuditRuleService(
                new AuditRule(SysProgIds.AuditRule, AuditRuleMode.Off, AuditRuleMode.Off, false));
            var auditRules = new NoOpAuditRuleRepository();
            var rowId = Guid.NewGuid();
            string runId = Guid.NewGuid().ToString("N")[..8];

            try
            {
                var dataSet = _repository.GetNewData();
                var master = dataSet.Tables[SysProgIds.AuditRule]!;
                master.Rows[0][SysFields.RowId] = rowId;
                master.Rows[0]["sys_id"] = $"P{runId}";
                master.Rows[0][SysFields.Name] = "受稽核的表單";
                master.Rows[0]["change_mode"] = (int)AuditRuleMode.On;
                master.Rows[0]["access_mode"] = (int)AuditRuleMode.Inherit;

                CreateBo(CreateSessionToken(), writer, ruleService, auditRules)
                    .Save(new SaveArgs { DataSet = dataSet });

                var entry = Assert.IsType<ChangeAuditEntry>(Assert.Single(writer.Entries));
                Assert.Equal(ChangeKind.Insert, entry.ChangeKind);
                Assert.Equal(SysProgIds.AuditRule, entry.ProgId);
                // Policy changes are always sensitive, matching how `SystemBusinessObject` treats operations that grant capabilities.
                Assert.True(entry.IsSensitive);
            }
            finally
            {
                TryDelete(rowId);
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Saving clears that company's rule cache and sends a cross-node announcement")]
        public void Save_InvalidatesCompanyRuleCache()
        {
            var writer = new CapturingAuditLogWriter();
            var ruleService = new RecordingAuditRuleService(
                new AuditRule("Other", AuditRuleMode.Inherit, AuditRuleMode.Inherit, false));
            var auditRules = new NoOpAuditRuleRepository();
            var rowId = Guid.NewGuid();
            string runId = Guid.NewGuid().ToString("N")[..8];

            try
            {
                var dataSet = _repository.GetNewData();
                var master = dataSet.Tables[SysProgIds.AuditRule]!;
                master.Rows[0][SysFields.RowId] = rowId;
                master.Rows[0]["sys_id"] = $"I{runId}";
                master.Rows[0][SysFields.Name] = "快取失效";

                CreateBo(CreateSessionToken(), writer, ruleService, auditRules)
                    .Save(new SaveArgs { DataSet = dataSet });

                // Without the local clearing, the operator would see the rule they just changed have no effect, because the snapshot has no expiry.
                Assert.Equal([CompanyId], ruleService.Removed);
                Assert.Equal([CompanyId], auditRules.Notified);
            }
            finally
            {
                TryDelete(rowId);
            }
        }

        private void TryDelete(Guid rowId)
        {
            try { _repository.Delete(rowId); } catch (InvalidOperationException) { /* best effort */ }
        }
    }
}
