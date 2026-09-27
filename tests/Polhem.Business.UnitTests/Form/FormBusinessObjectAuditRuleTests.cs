using System.ComponentModel;
using Polhem.Business.Form;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Identity;
using Polhem.Definition.Logging;
using Polhem.Definition.Settings;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// How per-form audit rules (<c>st_audit_rule</c>) change the trail decisions of <c>FormBusinessObject</c>:
    /// rule <c>Off</c> silences an axis the deployment enables by default, rule <c>On</c> enables an axis the deployment disables by default,
    /// the sensitive flag flows into the records actually written, and the delete snapshot is still loaded when a rule turns off change records.
    /// </summary>
    /// <remarks>
    /// Rules are injected through a stub <see cref="IAuditRuleService"/> rather than written to <c>st_audit_rule</c>:
    /// this verifies the BO's decision logic, not how rules are read (that is covered by
    /// <c>AuditRuleRepositoryTests</c> and <c>AuditRuleServiceTests</c>).
    /// </remarks>
    public class FormBusinessObjectAuditRuleTests : IClassFixture<SharedDbFixture>
    {
        private const string CompanyId = "AUDITRULE";

        private readonly SharedDbFixture _fx;

        public FormBusinessObjectAuditRuleTests(SharedDbFixture fx) { _fx = fx; }

        private sealed class CapturingAuditLogWriter : IAuditLogWriter
        {
            public List<AuditEntry> Entries { get; } = [];
            public void Write(AuditEntry entry) => Entries.Add(entry);
        }

        private sealed class StubAuditRuleService : IAuditRuleService
        {
            private readonly CompanyAuditRules _rules;
            public StubAuditRuleService(AuditRule rule)
                => _rules = new CompanyAuditRules(CompanyId, [rule]);
            public CompanyAuditRules? Get(string companyId) => _rules;
            public void Remove(string companyId) { }
        }

        /// <summary>
        /// Seeds a session with a company. The rule lookup is keyed by the session's CompanyId,
        /// and without a company it amounts to no rule found, so the rule itself would not be tested.
        /// </summary>
        private Guid CreateSessionToken()
        {
            var accessToken = Guid.NewGuid();
            _fx.GetRequiredService<ISessionInfoService>().Set(new SessionInfo
            {
                AccessToken = accessToken,
                UserId = "audit_rule_test",
                UserName = "audit_rule_test",
                CompanyId = CompanyId,
                ExpiredAt = DateTime.UtcNow.AddHours(1),
                ApiEncryptionKey = [],
            });
            return accessToken;
        }

        private static (Type, object?)[] Overrides(
            CapturingAuditLogWriter writer, AuditRule rule,
            bool changeEnabled, bool accessEnabled)
            =>
            [
                (typeof(AuditLogOptions), new AuditLogOptions
                {
                    Enabled = true,
                    ChangeEnabled = changeEnabled,
                    AccessEnabled = accessEnabled,
                }),
                (typeof(IAuditLogWriter), writer),
                (typeof(IAuditRuleService), new StubAuditRuleService(rule)),
            ];

        private static AuditRule Rule(AuditRuleMode change, AuditRuleMode access, bool sensitive = false)
            => new(CrudTestContext.ProgId, change, access, sensitive);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Rule Off suppresses Save's change records even when the deployment default is on")]
        public void Save_RuleOff_WritesNothingDespiteEnabledDefault()
        {
            var ctx = new CrudTestContext(_fx, DatabaseType.SQLite);
            var writer = new CapturingAuditLogWriter();
            var rowId = Guid.NewGuid();
            string runId = Guid.NewGuid().ToString("N")[..8];

            try
            {
                var dataSet = ctx.Repository.GetNewData();
                var master = dataSet.Tables[CrudTestContext.ProgId]!;
                master.Rows[0][SysFields.RowId] = rowId;
                master.Rows[0]["sys_id"] = $"F{runId}";
                master.Rows[0][SysFields.Name] = "規則關閉";

                ctx.CreateBoWithSession(CreateSessionToken(), null,
                        Overrides(writer, Rule(AuditRuleMode.Off, AuditRuleMode.Off),
                            changeEnabled: true, accessEnabled: true))
                    .Save(new SaveArgs { DataSet = dataSet });

                Assert.Empty(writer.Entries);
            }
            finally
            {
                TryDelete(ctx, rowId);
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Rule On enables change records the deployment disables by default, and the sensitive flag is written into the record")]
        public void Save_RuleOn_OverridesDisabledDefaultAndCarriesSensitiveFlag()
        {
            var ctx = new CrudTestContext(_fx, DatabaseType.SQLite);
            var writer = new CapturingAuditLogWriter();
            var rowId = Guid.NewGuid();
            string runId = Guid.NewGuid().ToString("N")[..8];

            try
            {
                var dataSet = ctx.Repository.GetNewData();
                var master = dataSet.Tables[CrudTestContext.ProgId]!;
                master.Rows[0][SysFields.RowId] = rowId;
                master.Rows[0]["sys_id"] = $"S{runId}";
                master.Rows[0][SysFields.Name] = "規則開啟";

                // `changeEnabled: false` is the point of this test: rule On must override the deployment's default of off,
                // otherwise the main use case, recording only this one important form, does not hold.
                ctx.CreateBoWithSession(CreateSessionToken(), null,
                        Overrides(writer, Rule(AuditRuleMode.On, AuditRuleMode.Off, sensitive: true),
                            changeEnabled: false, accessEnabled: false))
                    .Save(new SaveArgs { DataSet = dataSet });

                var entry = Assert.IsType<ChangeAuditEntry>(Assert.Single(writer.Entries));
                Assert.Equal(ChangeKind.Insert, entry.ChangeKind);
                Assert.True(entry.IsSensitive);
            }
            finally
            {
                TryDelete(ctx, rowId);
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Rule On enables view records the deployment disables by default")]
        public void GetData_RuleOn_OverridesDisabledDefault()
        {
            var ctx = new CrudTestContext(_fx, DatabaseType.SQLite);
            var writer = new CapturingAuditLogWriter();
            var rowId = Guid.NewGuid();
            string runId = Guid.NewGuid().ToString("N")[..8];

            try
            {
                InsertRow(ctx, rowId, $"V{runId}", "規則檢視");

                ctx.CreateBoWithSession(CreateSessionToken(), null,
                        Overrides(writer, Rule(AuditRuleMode.Off, AuditRuleMode.On),
                            changeEnabled: false, accessEnabled: false))
                    .GetData(new GetDataArgs { RowId = rowId });

                var entry = Assert.IsType<AccessAuditEntry>(Assert.Single(writer.Entries));
                Assert.Equal(rowId.ToString(), entry.RowKey);
            }
            finally
            {
                TryDelete(ctx, rowId);
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A delete-stage plugin still gets the Snapshot when a rule turns off change records")]
        public void Delete_RuleOff_PluginStillGetsSnapshot()
        {
            // Regression test: the Snapshot loads when `auditChange || pluginNeedsSnapshot || rule`,
            // and per-form rules can now make `auditChange` false. If someone ever simplified the condition to look only at
            // `auditChange`, the same plugin would see null on a deployment with a rule and data on one without.
            var ctx = new CrudTestContext(_fx, DatabaseType.SQLite);
            var writer = new CapturingAuditLogWriter();
            var rowId = Guid.NewGuid();
            string runId = Guid.NewGuid().ToString("N")[..8];
            RuleOffDeleteProbe.Reset();

            InsertRow(ctx, rowId, $"P{runId}", "規則關閉待刪");

            var resolver = new FixedChainResolver(FormPluginChain.Create("Order",
                [new FormPluginBinding(typeof(RuleOffBeforeDeleteProbePlugin), PluginStage.BeforeDelete),
                 new FormPluginBinding(typeof(RuleOffAfterDeleteProbePlugin), PluginStage.AfterDelete)]));

            ctx.CreateBoWithSession(CreateSessionToken(), resolver,
                    Overrides(writer, Rule(AuditRuleMode.Off, AuditRuleMode.Off),
                        changeEnabled: true, accessEnabled: true))
                .Delete(new DeleteArgs { RowId = rowId });

            Assert.Empty(writer.Entries);
            Assert.True(RuleOffDeleteProbe.BeforeDeleteSawSnapshot);
            Assert.True(RuleOffDeleteProbe.AfterDeleteSawSnapshot);
            Assert.Equal("規則關閉待刪", RuleOffDeleteProbe.DeletedName);
        }

        /// <summary>
        /// The log shared by the two delete stage probes.
        /// </summary>
        /// <remarks>
        /// A plugin binds to one stage, so the two stages are two classes. The log lives in a shared static container
        /// rather than an instance field, which no longer works across stages.
        /// <para>
        /// This deliberately does not share the equivalent probes of <c>FormBusinessObjectPluginIntegrationTests</c>:
        /// the state is <c>static</c>, and xUnit runs different test classes in parallel, so sharing would make them overwrite each other.
        /// </para>
        /// </remarks>
        public static class RuleOffDeleteProbe
        {
            public static bool BeforeDeleteSawSnapshot { get; set; }
            public static bool AfterDeleteSawSnapshot { get; set; }
            public static string DeletedName { get; set; } = string.Empty;

            public static void Reset()
            {
                BeforeDeleteSawSnapshot = false;
                AfterDeleteSawSnapshot = false;
                DeletedName = string.Empty;
            }
        }

        public sealed class RuleOffBeforeDeleteProbePlugin : FormBusinessPlugin
        {
            public RuleOffBeforeDeleteProbePlugin(IBusinessObjectContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId) { }

            public override void BeforeDelete(DeleteContext context)
                => RuleOffDeleteProbe.BeforeDeleteSawSnapshot = context.Snapshot != null;
        }

        public sealed class RuleOffAfterDeleteProbePlugin : FormBusinessPlugin
        {
            public RuleOffAfterDeleteProbePlugin(IBusinessObjectContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId) { }

            public override void AfterDelete(DeleteContext context)
            {
                RuleOffDeleteProbe.AfterDeleteSawSnapshot = context.Snapshot != null;
                var table = context.Snapshot?.Tables[CrudTestContext.ProgId];
                if (table is { Rows.Count: > 0 })
                    RuleOffDeleteProbe.DeletedName = table.Rows[0][SysFields.Name]?.ToString() ?? string.Empty;
            }
        }

        private sealed class FixedChainResolver : IFormPluginResolver
        {
            private readonly FormPluginChain _chain;
            public FixedChainResolver(FormPluginChain chain) => _chain = chain;
            public FormPluginChain Resolve(string customizeId, string progId) => _chain;
        }

        private static void InsertRow(CrudTestContext ctx, Guid rowId, string sysId, string sysName)
        {
            var dataSet = ctx.Repository.GetNewData();
            var master = dataSet.Tables[CrudTestContext.ProgId]!;
            master.Rows[0][SysFields.RowId] = rowId;
            master.Rows[0]["sys_id"] = sysId;
            master.Rows[0][SysFields.Name] = sysName;
            ctx.Repository.Save(dataSet);
        }

        private static void TryDelete(CrudTestContext ctx, Guid rowId)
        {
            try { ctx.Repository.Delete(rowId); } catch (InvalidOperationException) { /* best effort */ }
        }
    }
}
