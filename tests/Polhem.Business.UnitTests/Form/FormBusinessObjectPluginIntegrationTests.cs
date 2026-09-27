using System.ComponentModel;
using System.Data;
using Polhem.Base.Exceptions;
using Polhem.Business.Form;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Logging;
using Polhem.Definition.Settings;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// End-to-end behavior of plugins through the real <see cref="FormBusinessObject.Save"/> / <see cref="FormBusinessObject.Delete"/>:
    /// each hook sits at its place in the pipeline, the data it changes is really stored, an exception aborts the whole operation,
    /// and the delete stages get the <c>Snapshot</c>.
    /// </summary>
    /// <remarks>
    /// The unit level (<c>FormPluginRunnerTests</c>) already pins down the runner's own order and lifecycle. This verifies that
    /// <see cref="FormBusinessObject"/> connects the runner in the right places, which unit tests cannot see.
    /// </remarks>
    public class FormBusinessObjectPluginIntegrationTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public FormBusinessObjectPluginIntegrationTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a column changed by a BeforeSave plugin is really written to the database")]
        public void Save_BeforeSavePlugin_MutationIsPersisted()
        {
            var ctx = new CrudTestContext(_fx, DatabaseType.SQLite);
            var rowId = Guid.NewGuid();
            string runId = Guid.NewGuid().ToString("N")[..8];

            try
            {
                var dataSet = ctx.Repository.GetNewData();
                var master = dataSet.Tables[CrudTestContext.ProgId]!;
                master.Rows[0][SysFields.RowId] = rowId;
                master.Rows[0]["sys_id"] = $"S{runId}";
                master.Rows[0][SysFields.Name] = "原始名稱";

                ctx.CreateBo(Resolver<RenamingPlugin>(PluginStage.BeforeSave)).Save(new SaveArgs { DataSet = dataSet });

                // BeforeSave runs before persistence, so the change must be visible.
                var reloaded = ctx.CreateBo().GetData(new GetDataArgs { RowId = rowId });
                Assert.Equal("BeforeSave 改過",
                    reloaded.DataSet!.Tables[CrudTestContext.ProgId]!.Rows[0][SysFields.Name]);
            }
            finally
            {
                DeleteRow(ctx, rowId);
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: when a BeforeSave plugin throws, the whole save aborts and nothing is written")]
        public void Save_BeforeSavePluginThrows_AbortsWithoutWriting()
        {
            var ctx = new CrudTestContext(_fx, DatabaseType.SQLite);
            var rowId = Guid.NewGuid();
            string runId = Guid.NewGuid().ToString("N")[..8];

            var dataSet = ctx.Repository.GetNewData();
            var master = dataSet.Tables[CrudTestContext.ProgId]!;
            master.Rows[0][SysFields.RowId] = rowId;
            master.Rows[0]["sys_id"] = $"S{runId}";
            master.Rows[0][SysFields.Name] = "不該被存進去";

            var ex = Assert.Throws<UserMessageException>(() =>
                ctx.CreateBo(Resolver<RejectingPlugin>(PluginStage.BeforeSave)).Save(new SaveArgs { DataSet = dataSet }));
            Assert.Equal("擋下這筆。", ex.Message);

            // BeforeSave aborts before persistence, so nothing should be stored.
            Assert.Null(ctx.CreateBo().GetData(new GetDataArgs { RowId = rowId }).DataSet);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: both stages within one Save run in pipeline order as separate instances")]
        public void Save_TwoStages_RunInPipelineOrderAsSeparateInstances()
        {
            var ctx = new CrudTestContext(_fx, DatabaseType.SQLite);
            var rowId = Guid.NewGuid();
            string runId = Guid.NewGuid().ToString("N")[..8];
            TracingProbe.Reset();

            try
            {
                var dataSet = ctx.Repository.GetNewData();
                var master = dataSet.Tables[CrudTestContext.ProgId]!;
                master.Rows[0][SysFields.RowId] = rowId;
                master.Rows[0]["sys_id"] = $"S{runId}";
                master.Rows[0][SysFields.Name] = "順序驗證";

                ctx.CreateBo(Resolver(
                        new FormPluginBinding(typeof(BeforeSaveTracingPlugin), PluginStage.BeforeSave),
                        new FormPluginBinding(typeof(AfterSaveTracingPlugin), PluginStage.AfterSave)))
                    .Save(new SaveArgs { DataSet = dataSet });

                // Both stages ran, in the order the pipeline decides. A plugin binds to one stage, so these are two classes and
                // two instances, and no instance field can carry state across stages.
                Assert.Equal(["BeforeSave", "AfterSave"], TracingProbe.Calls);
                Assert.Equal(2, TracingProbe.ConstructedCount);
            }
            finally
            {
                DeleteRow(ctx, rowId);
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: an AfterSave plugin sees the RefreshedDataSet")]
        public void Save_AfterSavePlugin_SeesRefreshedDataSet()
        {
            var ctx = new CrudTestContext(_fx, DatabaseType.SQLite);
            var rowId = Guid.NewGuid();
            string runId = Guid.NewGuid().ToString("N")[..8];
            TracingProbe.Reset();

            try
            {
                var dataSet = ctx.Repository.GetNewData();
                var master = dataSet.Tables[CrudTestContext.ProgId]!;
                master.Rows[0][SysFields.RowId] = rowId;
                master.Rows[0]["sys_id"] = $"S{runId}";
                master.Rows[0][SysFields.Name] = "重讀驗證";

                ctx.CreateBo(Resolver<AfterSaveTracingPlugin>(PluginStage.AfterSave)).Save(new SaveArgs { DataSet = dataSet });

                Assert.True(TracingProbe.AfterSaveHadRefreshedDataSet);
            }
            finally
            {
                DeleteRow(ctx, rowId);
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: AfterDelete still gets the Snapshot when auditing is off")]
        public void Delete_AfterDeletePlugin_GetsSnapshotWithAuditDisabled()
        {
            // This is the regression test for the fix to the Snapshot loading condition, and it must run with auditing off. The old condition was
            // `auditChange || HasBeforeDeleteRules(schema)`; when neither held, the Snapshot was null,
            // and a synchronizing AfterDelete plugin needs exactly that information about what was deleted.
            var auditOptions = _fx.Provider.GetService(typeof(Definition.Settings.AuditLogOptions))
                as Definition.Settings.AuditLogOptions;
            Assert.True(auditOptions is not { Enabled: true, ChangeEnabled: true },
                "This test assumes change auditing is off. If the test environment turns it on by default, this regression is no longer tested.");

            var ctx = new CrudTestContext(_fx, DatabaseType.SQLite);
            var rowId = Guid.NewGuid();
            string runId = Guid.NewGuid().ToString("N")[..8];
            DeleteProbe.Reset();

            var dataSet = ctx.Repository.GetNewData();
            var master = dataSet.Tables[CrudTestContext.ProgId]!;
            master.Rows[0][SysFields.RowId] = rowId;
            master.Rows[0]["sys_id"] = $"S{runId}";
            master.Rows[0][SysFields.Name] = "待刪除";
            ctx.CreateBo().Save(new SaveArgs { DataSet = dataSet });

            ctx.CreateBo(Resolver(
                    new FormPluginBinding(typeof(BeforeDeleteProbePlugin), PluginStage.BeforeDelete),
                    new FormPluginBinding(typeof(AfterDeleteProbePlugin), PluginStage.AfterDelete)))
                .Delete(new DeleteArgs { RowId = rowId });

            Assert.True(DeleteProbe.BeforeDeleteSawSnapshot);
            Assert.True(DeleteProbe.AfterDeleteSawSnapshot);
            Assert.Equal("待刪除", DeleteProbe.DeletedName);
            Assert.Equal(DataRowState.Unchanged, DeleteProbe.AfterDeleteRowState);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: with auditing on, the Snapshot AfterDelete gets is still the unmodified original record with readable column values")]
        public void Delete_AfterDeletePlugin_ReadsSnapshotWithAuditEnabled()
        {
            // Complements the previous test: delete auditing builds its payload from the same Snapshot. If it changed the row state,
            // AfterDelete reading columns with the default version would throw `DeletedRowInaccessibleException`,
            // while the same plugin works on a deployment with auditing off. What a plugin sees must not depend on the audit switch.
            var ctx = new CrudTestContext(_fx, DatabaseType.SQLite);
            var writer = new CapturingAuditLogWriter();
            var rowId = Guid.NewGuid();
            string runId = Guid.NewGuid().ToString("N")[..8];
            DeleteProbe.Reset();

            var dataSet = ctx.Repository.GetNewData();
            var master = dataSet.Tables[CrudTestContext.ProgId]!;
            master.Rows[0][SysFields.RowId] = rowId;
            master.Rows[0]["sys_id"] = $"S{runId}";
            master.Rows[0][SysFields.Name] = "稽核開啟待刪除";
            ctx.CreateBo().Save(new SaveArgs { DataSet = dataSet });

            try
            {
                ctx.CreateBoWithOverrides(
                        (typeof(AuditLogOptions), new AuditLogOptions { Enabled = true, ChangeEnabled = true }),
                        (typeof(IAuditLogWriter), writer),
                        (typeof(IFormPluginResolver), Resolver<AfterDeleteProbePlugin>(PluginStage.AfterDelete)))
                    .Delete(new DeleteArgs { RowId = rowId });

                // First confirm that the audit really was written, otherwise this test could pass vacuously with auditing off.
                Assert.IsType<ChangeAuditEntry>(Assert.Single(writer.Entries));
                Assert.Equal(DataRowState.Unchanged, DeleteProbe.AfterDeleteRowState);
                Assert.Equal("稽核開啟待刪除", DeleteProbe.DeletedName);
            }
            finally
            {
                DeleteRow(ctx, rowId);
            }
        }

        // ---- Helpers ----

        private sealed class CapturingAuditLogWriter : IAuditLogWriter
        {
            public List<AuditEntry> Entries { get; } = [];

            public void Write(AuditEntry entry) => Entries.Add(entry);
        }

        private static void DeleteRow(CrudTestContext ctx, Guid rowId)
        {
            try { ctx.Repository.Delete(rowId); } catch (InvalidOperationException) { /* best effort */ }
        }

        private static FixedChainResolver Resolver<T>(PluginStage stage) where T : FormBusinessPlugin
            => Resolver(new FormPluginBinding(typeof(T), stage));

        private static FixedChainResolver Resolver(params FormPluginBinding[] bindings)
            => new FixedChainResolver(FormPluginChain.Create(CrudTestContext.ProgId, bindings));

        private sealed class FixedChainResolver : IFormPluginResolver
        {
            private readonly FormPluginChain _chain;
            public FixedChainResolver(FormPluginChain chain) => _chain = chain;
            public FormPluginChain Resolve(string customizeId, string progId) => _chain;
        }

        // ---- Test plugins ----

        public sealed class RenamingPlugin : FormBusinessPlugin
        {
            public RenamingPlugin(IBusinessObjectContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId) { }

            public override void BeforeSave(SaveContext context)
                => context.DataSet.Tables[CrudTestContext.ProgId]!.Rows[0][SysFields.Name] = "BeforeSave 改過";
        }

        public sealed class RejectingPlugin : FormBusinessPlugin
        {
            public RejectingPlugin(IBusinessObjectContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId) { }

            public override void BeforeSave(SaveContext context)
                => throw new UserMessageException("擋下這筆。");
        }

        /// <summary>The log shared by the two save stage probes.</summary>
        public static class TracingProbe
        {
            public static List<string> Calls { get; } = [];
            public static int ConstructedCount { get; set; }
            public static bool AfterSaveHadRefreshedDataSet { get; set; }

            public static void Reset()
            {
                Calls.Clear();
                ConstructedCount = 0;
                AfterSaveHadRefreshedDataSet = false;
            }
        }

        public sealed class BeforeSaveTracingPlugin : FormBusinessPlugin
        {
            public BeforeSaveTracingPlugin(IBusinessObjectContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId)
            {
                TracingProbe.ConstructedCount++;
            }

            public override void BeforeSave(SaveContext context) => TracingProbe.Calls.Add("BeforeSave");
        }

        public sealed class AfterSaveTracingPlugin : FormBusinessPlugin
        {
            public AfterSaveTracingPlugin(IBusinessObjectContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId)
            {
                TracingProbe.ConstructedCount++;
            }

            public override void AfterSave(SaveContext context)
            {
                TracingProbe.AfterSaveHadRefreshedDataSet = context.RefreshedDataSet != null;
                TracingProbe.Calls.Add("AfterSave");
            }
        }

        /// <summary>
        /// The log shared by the two delete stage probes. A plugin binds to one stage, so the two stages are two classes,
        /// and they have no instance field to share.
        /// </summary>
        public static class DeleteProbe
        {
            public static bool BeforeDeleteSawSnapshot { get; set; }
            public static bool AfterDeleteSawSnapshot { get; set; }
            public static string DeletedName { get; set; } = string.Empty;
            public static DataRowState? AfterDeleteRowState { get; set; }

            public static void Reset()
            {
                BeforeDeleteSawSnapshot = false;
                AfterDeleteSawSnapshot = false;
                DeletedName = string.Empty;
                AfterDeleteRowState = null;
            }
        }

        public sealed class BeforeDeleteProbePlugin : FormBusinessPlugin
        {
            public BeforeDeleteProbePlugin(IBusinessObjectContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId) { }

            public override void BeforeDelete(DeleteContext context)
                => DeleteProbe.BeforeDeleteSawSnapshot = context.Snapshot != null;
        }

        public sealed class AfterDeleteProbePlugin : FormBusinessPlugin
        {
            public AfterDeleteProbePlugin(IBusinessObjectContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId) { }

            public override void AfterDelete(DeleteContext context)
            {
                DeleteProbe.AfterDeleteSawSnapshot = context.Snapshot != null;
                var table = context.Snapshot?.Tables[CrudTestContext.ProgId];
                if (table is { Rows.Count: > 0 })
                {
                    // Recorded before the read below, which throws on a row marked deleted.
                    DeleteProbe.AfterDeleteRowState = table.Rows[0].RowState;
                    DeleteProbe.DeletedName = table.Rows[0][SysFields.Name]?.ToString() ?? string.Empty;
                }
            }
        }
    }
}
