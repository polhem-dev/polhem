using System.ComponentModel;
using System.Data;
using System.Data.Common;
using Polhem.Business.AuditLog;
using Polhem.Business.Form;
using Polhem.Business.UnitTests.Fakes;
using Polhem.Db.Dml;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Filters;
using Polhem.Definition.Logging;
using Polhem.Definition.Settings;
using Polhem.Tests.Shared;
using Microsoft.Extensions.Logging;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// The audit trail of <c>FormBusinessObject</c> (<c>FormBusinessObject.Audit.cs</c>): Save splits Insert / Update by row state,
    /// Delete carries the before image, and nothing at all is written when the switch is off.
    /// </summary>
    /// <remarks>
    /// It goes through the real CRUD path instead of calling the audit methods directly: <c>ChangeKind</c> is derived from <c>DataRow.RowState</c>,
    /// and <c>Save</c> resets the RowState. Reading the value at the right moment is exactly what this code must guarantee,
    /// and bypassing CRUD would not test it.
    /// </remarks>
    public class FormBusinessObjectAuditTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public FormBusinessObjectAuditTests(SharedDbFixture fx) { _fx = fx; }

        private sealed class CapturingAuditLogWriter : IAuditLogWriter
        {
            public List<AuditEntry> Entries { get; } = [];

            public void Write(AuditEntry entry) => Entries.Add(entry);
        }

        /// <summary>Simulates a failure of the log database or a custom writer.</summary>
        private sealed class ThrowingAuditLogWriter : IAuditLogWriter
        {
            public void Write(AuditEntry entry) => throw new InvalidOperationException("Audit sink unavailable.");
        }

        /// <summary>Records whether the extension points after commit ran.</summary>
        private sealed class AfterStepProbeBo : FormBusinessObject
        {
            public AfterStepProbeBo(IBusinessObjectContext ctx, Guid accessToken) : base(ctx, accessToken, CrudTestContext.ProgId) { }

            public bool AfterSaveRan { get; private set; }

            public bool AfterDeleteRan { get; private set; }

            protected override void DoAfterSave(SaveContext context)
            {
                base.DoAfterSave(context);
                AfterSaveRan = true;
            }

            protected override void DoAfterDelete(DeleteContext context)
            {
                base.DoAfterDelete(context);
                AfterDeleteRan = true;
            }
        }

        private static (Type, object?)[] AuditOverrides(
            IAuditLogWriter writer, bool enabled = true,
            bool changeEnabled = true, bool accessEnabled = true)
            =>
            [
                (typeof(AuditLogOptions), new AuditLogOptions
                {
                    Enabled = enabled,
                    ChangeEnabled = changeEnabled,
                    AccessEnabled = accessEnabled
                }),
                (typeof(IAuditLogWriter), writer)
            ];

        private static ChangeAuditEntry SingleChange(CapturingAuditLogWriter writer)
            => Assert.IsType<ChangeAuditEntry>(Assert.Single(writer.Entries));

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Save of an added row writes an Insert audit entry carrying the master table, sys_rowid and the session identity")]
        public void Save_AddedRow_WritesInsertAudit()
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
                master.Rows[0]["sys_id"] = $"A{runId}";
                master.Rows[0][SysFields.Name] = "稽核新增";

                ctx.CreateBoWithOverrides(AuditOverrides(writer))
                    .Save(new SaveArgs { DataSet = dataSet });

                var entry = SingleChange(writer);
                Assert.Equal(ChangeKind.Insert, entry.ChangeKind);
                Assert.Equal(CrudTestContext.ProgId, entry.ProgId);
                Assert.Equal(rowId.ToString(), entry.RowKey);
                Assert.Equal($"{CrudTestContext.ProgId}.Save", entry.Source);
                Assert.False(entry.IsSensitive);
                Assert.False(string.IsNullOrEmpty(entry.ChangesXml));
                Assert.Equal(CrudTestContext.UserId, entry.UserId);
                Assert.Equal(CrudTestContext.CompanyId, entry.CompanyId);
                Assert.Equal(CrudTestContext.CompanyName, entry.CompanyName);
            }
            finally
            {
                TryDelete(ctx, rowId);
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Save of a modified row writes an Update audit entry whose changes carry the new value")]
        public void Save_ModifiedRow_WritesUpdateAudit()
        {
            var ctx = new CrudTestContext(_fx, DatabaseType.SQLite);
            var writer = new CapturingAuditLogWriter();
            var rowId = Guid.NewGuid();
            string runId = Guid.NewGuid().ToString("N")[..8];

            try
            {
                InsertEmployee(ctx, rowId, $"U{runId}", "稽核原值");

                var loaded = ctx.CreateBo().GetData(new GetDataArgs { RowId = rowId }).DataSet!;
                loaded.Tables[CrudTestContext.ProgId]!.Rows[0][SysFields.Name] = "稽核新值";

                ctx.CreateBoWithOverrides(AuditOverrides(writer))
                    .Save(new SaveArgs { DataSet = loaded });

                var entry = SingleChange(writer);
                Assert.Equal(ChangeKind.Update, entry.ChangeKind);
                // Case-insensitive: the update path takes `RowKey` from the value the DataRow read back from the DB, and SQLite stores
                // GUIDs as strings and returns them in uppercase, while the insert path gets it from `Guid.ToString()` (lowercase).
                Assert.Equal(rowId.ToString(), entry.RowKey, StringComparer.OrdinalIgnoreCase);

                var changed = ChangeDiffGramReader.Read(entry.ChangesXml);
                var field = Assert.Single(changed, f => f.FieldName == SysFields.Name);
                Assert.Equal("稽核新值", field.NewValue);
            }
            finally
            {
                TryDelete(ctx, rowId);
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Delete writes a Delete audit entry whose before image contains the deleted data, not only the key")]
        public void Delete_WritesDeleteAuditWithBeforeImage()
        {
            var ctx = new CrudTestContext(_fx, DatabaseType.SQLite);
            var writer = new CapturingAuditLogWriter();
            var rowId = Guid.NewGuid();
            string runId = Guid.NewGuid().ToString("N")[..8];

            try
            {
                InsertEmployee(ctx, rowId, $"D{runId}", "稽核待刪");

                ctx.CreateBoWithOverrides(AuditOverrides(writer))
                    .Delete(new DeleteArgs { RowId = rowId });

                var entry = SingleChange(writer);
                Assert.Equal(ChangeKind.Delete, entry.ChangeKind);
                Assert.Equal(rowId.ToString(), entry.RowKey);
                Assert.Equal($"{CrudTestContext.ProgId}.Delete", entry.Source);
                // Only the before image shows what was deleted. A minimal XML with only the key has no column values.
                Assert.Contains("稽核待刪", entry.ChangesXml, StringComparison.Ordinal);
                // A delete stores the original record itself, not a change set with the row marked Deleted.
                Assert.StartsWith("<" + AuditDiffGram.DeletedRecordRootElementName + ">", entry.ChangesXml, StringComparison.Ordinal);
            }
            finally
            {
                TryDelete(ctx, rowId);
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Deleting a row that does not exist writes no audit entry (the trail records actual changes, not attempts)")]
        public void Delete_MissingRow_WritesNothing()
        {
            var ctx = new CrudTestContext(_fx, DatabaseType.SQLite);
            var writer = new CapturingAuditLogWriter();

            var result = ctx.CreateBoWithOverrides(AuditOverrides(writer))
                .Delete(new DeleteArgs { RowId = Guid.NewGuid() });

            Assert.Equal(0, result.RowsAffected);
            Assert.Empty(writer.Entries);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("GetData writes an access trail when AccessEnabled is on")]
        public void GetData_WritesAccessAudit()
        {
            var ctx = new CrudTestContext(_fx, DatabaseType.SQLite);
            var writer = new CapturingAuditLogWriter();
            var rowId = Guid.NewGuid();
            string runId = Guid.NewGuid().ToString("N")[..8];

            try
            {
                InsertEmployee(ctx, rowId, $"R{runId}", "稽核讀取");

                ctx.CreateBoWithOverrides(AuditOverrides(writer))
                    .GetData(new GetDataArgs { RowId = rowId });

                var entry = Assert.IsType<AccessAuditEntry>(Assert.Single(writer.Entries));
                Assert.Equal(CrudTestContext.ProgId, entry.ProgId);
                Assert.Equal(rowId.ToString(), entry.RowKey);
                Assert.Equal($"{CrudTestContext.ProgId}.GetData", entry.Source);
                Assert.Equal(CrudTestContext.CompanyId, entry.CompanyId);
            }
            finally
            {
                TryDelete(ctx, rowId);
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("No access trail is written when no data is read")]
        public void GetData_MissingRow_WritesNothing()
        {
            var ctx = new CrudTestContext(_fx, DatabaseType.SQLite);
            var writer = new CapturingAuditLogWriter();

            var result = ctx.CreateBoWithOverrides(AuditOverrides(writer))
                .GetData(new GetDataArgs { RowId = Guid.NewGuid() });

            Assert.Null(result.DataSet);
            Assert.Empty(writer.Entries);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Save writes no change trail when ChangeEnabled is off")]
        public void Save_ChangeAuditDisabled_WritesNothing()
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
                master.Rows[0]["sys_id"] = $"N{runId}";
                master.Rows[0][SysFields.Name] = "不留痕";

                ctx.CreateBoWithOverrides(AuditOverrides(writer, changeEnabled: false, accessEnabled: false))
                    .Save(new SaveArgs { DataSet = dataSet });

                Assert.Empty(writer.Entries);
            }
            finally
            {
                TryDelete(ctx, rowId);
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Delete writes no change trail when the global switch is off")]
        public void Delete_AuditDisabled_WritesNothing()
        {
            var ctx = new CrudTestContext(_fx, DatabaseType.SQLite);
            var writer = new CapturingAuditLogWriter();
            var rowId = Guid.NewGuid();
            string runId = Guid.NewGuid().ToString("N")[..8];

            try
            {
                InsertEmployee(ctx, rowId, $"Z{runId}", "不留痕待刪");

                ctx.CreateBoWithOverrides(AuditOverrides(writer, enabled: false))
                    .Delete(new DeleteArgs { RowId = rowId });

                Assert.Empty(writer.Entries);
            }
            finally
            {
                TryDelete(ctx, rowId);
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Save succeeds when a column value contains a control character XML forbids, and the audit reads back the original value")]
        public void Save_ValueWithControlCharacter_WritesReadableAudit()
        {
            var ctx = new CrudTestContext(_fx, DatabaseType.SQLite);
            var writer = new CapturingAuditLogWriter();
            var rowId = Guid.NewGuid();
            string runId = Guid.NewGuid().ToString("N")[..8];
            const string pasted = "貼上\u0001的值";

            try
            {
                InsertEmployee(ctx, rowId, $"C{runId}", "控制字元原值");

                var loaded = ctx.CreateBo().GetData(new GetDataArgs { RowId = rowId }).DataSet!;
                loaded.Tables[CrudTestContext.ProgId]!.Rows[0][SysFields.Name] = pasted;

                ctx.CreateBoWithOverrides(AuditOverrides(writer))
                    .Save(new SaveArgs { DataSet = loaded });

                var entry = SingleChange(writer);
                var field = Assert.Single(ChangeDiffGramReader.Read(entry.ChangesXml), f => f.FieldName == SysFields.Name);
                Assert.Equal(pasted, field.NewValue);
            }
            finally
            {
                TryDelete(ctx, rowId);
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Delete succeeds when the deleted data contains a control character XML forbids, and the before image reads back the original value")]
        public void Delete_ValueWithControlCharacter_WritesReadableAudit()
        {
            var ctx = new CrudTestContext(_fx, DatabaseType.SQLite);
            var writer = new CapturingAuditLogWriter();
            var rowId = Guid.NewGuid();
            string runId = Guid.NewGuid().ToString("N")[..8];
            const string stored = "待刪\u0001資料";

            try
            {
                InsertEmployee(ctx, rowId, $"E{runId}", stored);

                ctx.CreateBoWithOverrides(AuditOverrides(writer))
                    .Delete(new DeleteArgs { RowId = rowId });

                var entry = SingleChange(writer);
                var field = Assert.Single(ChangeDiffGramReader.Read(entry.ChangesXml),
                    f => f.FieldName == SysFields.Name && f.RowState == ChangeKind.Delete);
                Assert.Equal(stored, field.OldValue);
            }
            finally
            {
                TryDelete(ctx, rowId);
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("When the audit write fails, a committed Save does not report failure, AfterSave still runs, and an error is logged")]
        public void Save_AuditWriteFails_CompletesAndLogsError()
        {
            var ctx = new CrudTestContext(_fx, DatabaseType.SQLite);
            var loggers = new RecordingLoggerFactory();
            var rowId = Guid.NewGuid();
            string runId = Guid.NewGuid().ToString("N")[..8];

            try
            {
                var dataSet = ctx.Repository.GetNewData();
                var master = dataSet.Tables[CrudTestContext.ProgId]!;
                master.Rows[0][SysFields.RowId] = rowId;
                master.Rows[0]["sys_id"] = $"F{runId}";
                master.Rows[0][SysFields.Name] = "稽核失敗仍存檔";

                var bo = new AfterStepProbeBo(ctx.CreateContextWithOverrides(
                    [.. AuditOverrides(new ThrowingAuditLogWriter()), (typeof(ILoggerFactory), loggers)]), ctx.CreateSessionToken());
                bo.Save(new SaveArgs { DataSet = dataSet });

                Assert.True(bo.AfterSaveRan);
                Assert.NotNull(ctx.CreateBo().GetData(new GetDataArgs { RowId = rowId }).DataSet);
                var error = Assert.Single(loggers.Entries, e => e.Level == LogLevel.Error);
                Assert.IsType<InvalidOperationException>(error.Exception);
                Assert.Contains(rowId.ToString(), error.Message, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                TryDelete(ctx, rowId);
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("When the audit write fails, a committed Delete does not report failure, AfterDelete still runs, and an error is logged")]
        public void Delete_AuditWriteFails_CompletesAndLogsError()
        {
            var ctx = new CrudTestContext(_fx, DatabaseType.SQLite);
            var loggers = new RecordingLoggerFactory();
            var rowId = Guid.NewGuid();
            string runId = Guid.NewGuid().ToString("N")[..8];

            try
            {
                InsertEmployee(ctx, rowId, $"G{runId}", "稽核失敗仍刪除");

                var bo = new AfterStepProbeBo(ctx.CreateContextWithOverrides(
                    [.. AuditOverrides(new ThrowingAuditLogWriter()), (typeof(ILoggerFactory), loggers)]), ctx.CreateSessionToken());
                var result = bo.Delete(new DeleteArgs { RowId = rowId });

                Assert.Equal(1, result.RowsAffected);
                Assert.True(bo.AfterDeleteRan);
                var error = Assert.Single(loggers.Entries, e => e.Level == LogLevel.Error);
                Assert.IsType<InvalidOperationException>(error.Exception);
                Assert.Contains(rowId.ToString(), error.Message, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                TryDelete(ctx, rowId);
            }
        }

        private static void InsertEmployee(CrudTestContext ctx, Guid rowId, string sysId, string sysName)
        {
            var dt = new DataTable();
            dt.Columns.Add(SysFields.RowId, typeof(Guid));
            dt.Columns.Add("sys_id", typeof(string));
            dt.Columns.Add(SysFields.Name, typeof(string));
            dt.Columns.Add("dept_rowid", typeof(Guid));
            var row = dt.NewRow();
            row[SysFields.RowId] = rowId;
            row["sys_id"] = sysId;
            row[SysFields.Name] = sysName;
            row["dept_rowid"] = Guid.Empty;
            ctx.DbAccess.Execute(
                new InsertCommandBuilder(ctx.EmployeeSchema, ctx.DbType).Build(CrudTestContext.ProgId, row));
        }

        private static void TryDelete(CrudTestContext ctx, Guid rowId)
        {
            try
            {
                ctx.DbAccess.Execute(new DeleteCommandBuilder(ctx.EmployeeSchema, ctx.DbType)
                    .Build(CrudTestContext.ProgId, FilterCondition.Equal(SysFields.RowId, rowId)));
            }
            catch (DbException ex)
            {
                Console.WriteLine($"AuditTests cleanup of Employee#{rowId} failed — {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
