using System.ComponentModel;
using System.Data;
using System.Globalization;
using Polhem.Business.AuditLog;
using Polhem.Definition.Identity;
using Polhem.Definition.Logging;
using Polhem.Definition.Paging;
using Polhem.Definition.Settings;
using Polhem.Repository.Abstractions.AuditLog;
using Polhem.Repository.Abstractions.Form;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Tests.Shared;

using Polhem.Definition;
using Polhem.Base.Exceptions;
namespace Polhem.Business.UnitTests.AuditLog
{
    /// <summary>
    /// BO-level behavior tests for <see cref="AuditLogBusinessObject"/> (a stub repository, no real DB):
    /// the list methods (<c>GetChangeLog</c> and others) return headers + paging and pass the filter through,
    /// the detail method (<c>GetChangeDetail</c>) restores the changes_xml DiffGram into structured before/after values
    /// and throws when nothing is found; every method has the permission gate and argument validation.
    /// </summary>
    /// <remarks>
    /// NOTE: the opening claim of no real DB used to be only an intention. The BO was constructed with a bare <c>Guid.NewGuid()</c> token,
    /// and its methods call <c>SessionInfoService.Get(AccessToken)</c> (for the current company and the locale). That token
    /// was not in the cache, so it took the rebuild path that reads <c>st_session</c>, and the whole class actually needed the container.
    /// It only became true after switching to <see cref="TestSessionFactory.CreateAccessToken"/>.
    /// </remarks>
    public class AuditLogBusinessObjectTests : IClassFixture<PolhemTestFixture>
    {
        private const string ProgId = "Employee";
        private readonly PolhemTestFixture _fx;

        public AuditLogBusinessObjectTests(PolhemTestFixture fx) { _fx = fx; }

        private AuditLogBusinessObject Bo(StubAuditLogRepository repo, bool authorized = true, bool deploymentAdmin = true)
        {
            var ctx = TestBusinessObjectContext.CreateWithOverrides(_fx,
                (typeof(ICompanyAuthorizationService), new FakeAuth(authorized)),
                (typeof(IDeploymentAuthorizationService), new FakeDeploymentAuth(deploymentAdmin)),
                (typeof(IRepositoryFactory), new StubAuditLogRepositoryFactory(repo)));
            return new AuditLogBusinessObject(ctx, TestSessionFactory.CreateAccessToken(_fx), SysProgIds.AuditLog);
        }

        // ---- GetChangeLog (filtered list) ----

        [Fact]
        [DisplayName("GetChangeLog returns the header list + paging and passes the typed filter through to the repository")]
        public void GetChangeLog_Authorized_PassesFilter()
        {
            var repo = new StubAuditLogRepository(HeaderPage(2));
            var bo = Bo(repo);
            var from = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

            var result = bo.GetChangeLog(new GetChangeLogArgs
            {
                FromUtc = from,
                UserId = "demo",
                ChangeKind = ChangeKind.Update,
                Paging = new PagingOptions { PageSize = 10 },
            });

            Assert.NotNull(result.Table);
            Assert.Equal(2, result.Table!.Rows.Count);
            Assert.Equal(from, repo.LastQuery!.FromUtc);
            Assert.Equal("demo", repo.LastQuery.UserId);
            Assert.Equal(ChangeKind.Update, repo.LastQuery.ChangeKind);
            Assert.Equal(10, repo.LastPaging!.PageSize);
        }

        [Fact]
        [DisplayName("GetChangeLog throws UserMessageException when not authorized")]
        public void GetChangeLog_NotAuthorized_Throws()
        {
            var bo = Bo(new StubAuditLogRepository(HeaderPage(0)), authorized: false);
            Assert.Throws<UserMessageException>(() => bo.GetChangeLog(new GetChangeLogArgs()));
        }

        // ---- GetChangeDetail (restore one event) ----

        [Fact]
        [DisplayName("GetChangeDetail restores a single changes_xml DiffGram into field-level before/after values")]
        public void GetChangeDetail_Authorized_RestoresFields()
        {
            var sysRowId = Guid.NewGuid();
            var rowKey = Guid.NewGuid().ToString();
            var xml = BuildModifyDiffGram("st_employee", rowKey, "name", "Alice", "Alice Wang");
            var repo = new StubAuditLogRepository(HeaderPage(0), DetailRow(sysRowId, rowKey, ChangeKind.Update, xml));
            var bo = Bo(repo);

            var result = bo.GetChangeDetail(new GetChangeDetailArgs { SysRowId = sysRowId });

            Assert.Equal(sysRowId, result.SysRowId);
            Assert.Equal(ChangeKind.Update, result.ChangeKind);
            Assert.Equal(ProgId, result.ProgId);
            Assert.Equal(rowKey, result.RowKey);
            var field = Assert.Single(result.Fields);
            Assert.Equal("name", field.FieldName);
            Assert.Equal("Alice", field.OldValue);
            Assert.Equal("Alice Wang", field.NewValue);
            Assert.Equal(sysRowId, repo.LastDetailId);
            // This payload is the old schemaless format, so no DataSet can be rebuilt.
            Assert.Null(result.DataSet);
        }

        [Fact]
        [DisplayName("GetChangeDetail returns the complete record DataSet before deletion for a delete event, with Fields as before")]
        public void GetChangeDetail_DeletedRecord_ReturnsRecordDataSet()
        {
            var sysRowId = Guid.NewGuid();
            var rowKey = Guid.NewGuid().ToString();
            using var record = new DataSet("st_employee");
            var table = record.Tables.Add("st_employee");
            table.Columns.Add("sys_rowid", typeof(string));
            table.Columns.Add("name", typeof(string));
            table.Rows.Add(rowKey, "Alice");
            record.AcceptChanges();
            var xml = AuditDiffGram.SerializeDeletedRecord(record);
            var repo = new StubAuditLogRepository(HeaderPage(0), DetailRow(sysRowId, rowKey, ChangeKind.Delete, xml));

            var result = Bo(repo).GetChangeDetail(new GetChangeDetailArgs { SysRowId = sysRowId });

            Assert.Equal(ChangeKind.Delete, result.ChangeKind);
            Assert.NotNull(result.DataSet);
            var row = Assert.Single(result.DataSet!.Tables["st_employee"]!.Rows.Cast<DataRow>());
            Assert.Equal(DataRowState.Unchanged, row.RowState);
            Assert.Equal("Alice", row["name"]);
            var field = Assert.Single(result.Fields);
            Assert.Equal(ChangeKind.Delete, field.RowState);
            Assert.Equal("Alice", field.OldValue);
        }

        [Fact]
        [DisplayName("GetChangeDetail throws UserMessageException when nothing is found")]
        public void GetChangeDetail_NotFound_Throws()
        {
            var repo = new StubAuditLogRepository(HeaderPage(0), detail: null);
            var bo = Bo(repo);
            Assert.Throws<UserMessageException>(() =>
                bo.GetChangeDetail(new GetChangeDetailArgs { SysRowId = Guid.NewGuid() }));
        }

        [Fact]
        [DisplayName("GetChangeDetail throws UserMessageException for a missing SysRowId")]
        public void GetChangeDetail_EmptySysRowId_Throws()
        {
            var bo = Bo(new StubAuditLogRepository(HeaderPage(0)));
            Assert.Throws<UserMessageException>(() =>
                bo.GetChangeDetail(new GetChangeDetailArgs { SysRowId = Guid.Empty }));
        }

        [Fact]
        [DisplayName("GetChangeDetail throws UserMessageException when not authorized")]
        public void GetChangeDetail_NotAuthorized_Throws()
        {
            var bo = Bo(new StubAuditLogRepository(HeaderPage(0)), authorized: false);
            Assert.Throws<UserMessageException>(() =>
                bo.GetChangeDetail(new GetChangeDetailArgs { SysRowId = Guid.NewGuid() }));
        }

        // ---- login / access / anomaly lists ----

        [Fact]
        [DisplayName("GetLoginLog returns the list + paging and passes the event / user filter through")]
        public void GetLoginLog_Authorized_PassesFilter()
        {
            var repo = new StubAuditLogRepository(HeaderPage(2));
            var result = Bo(repo).GetLoginLog(new GetLoginLogArgs { UserId = "demo", Event = LoginEvent.LoginFailed });

            Assert.Equal(2, result.Table!.Rows.Count);
            var q = Assert.IsType<LoginLogQuery>(repo.LastListQuery);
            Assert.Equal("demo", q.UserId);
            Assert.Equal(LoginEvent.LoginFailed, q.Event);
        }

        [Fact]
        [DisplayName("GetAccessLog returns the list + paging and passes the progId / rowKey filter through")]
        public void GetAccessLog_Authorized_PassesFilter()
        {
            var repo = new StubAuditLogRepository(HeaderPage(1));
            var result = Bo(repo).GetAccessLog(new GetAccessLogArgs { ProgId = "Order", RowKey = "R-9" });

            Assert.Single(result.Table!.Rows);
            var q = Assert.IsType<AccessLogQuery>(repo.LastListQuery);
            Assert.Equal("Order", q.ProgId);
            Assert.Equal("R-9", q.RowKey);
        }

        [Fact]
        [DisplayName("GetApiAnomalyLog returns the list and passes the method / kind filter through")]
        public void GetApiAnomalyLog_Authorized_PassesFilter()
        {
            var repo = new StubAuditLogRepository(HeaderPage(3));
            var result = Bo(repo).GetApiAnomalyLog(new GetApiAnomalyLogArgs { Method = "Order.Save", Kind = AnomalyKind.Slow });

            Assert.Equal(3, result.Table!.Rows.Count);
            var q = Assert.IsType<ApiAnomalyLogQuery>(repo.LastListQuery);
            Assert.Equal("Order.Save", q.Method);
            Assert.Equal(AnomalyKind.Slow, q.Kind);
        }

        [Fact]
        [DisplayName("GetDbAnomalyLog returns the list and passes the databaseId / kind filter through")]
        public void GetDbAnomalyLog_Authorized_PassesFilter()
        {
            var repo = new StubAuditLogRepository(HeaderPage(1));
            var result = Bo(repo).GetDbAnomalyLog(new GetDbAnomalyLogArgs { DatabaseId = "company", Kind = AnomalyKind.Timeout });

            Assert.Single(result.Table!.Rows);
            var q = Assert.IsType<DbAnomalyLogQuery>(repo.LastListQuery);
            Assert.Equal("company", q.DatabaseId);
            Assert.Equal(AnomalyKind.Timeout, q.Kind);
        }

        [Fact]
        [DisplayName("The list methods throw UserMessageException when not authorized")]
        public void ListMethods_NotAuthorized_Throw()
        {
            var bo = Bo(new StubAuditLogRepository(HeaderPage(0)), authorized: false);
            Assert.Throws<UserMessageException>(() => bo.GetLoginLog(new GetLoginLogArgs()));
            Assert.Throws<UserMessageException>(() => bo.GetAccessLog(new GetAccessLogArgs()));
            Assert.Throws<UserMessageException>(() => bo.GetApiAnomalyLog(new GetApiAnomalyLogArgs()));
        }

        [Fact]
        [DisplayName("The DB anomaly methods refuse a company audit reader who is not a deployment administrator")]
        public void DbAnomalyMethods_CompanyAuditReaderWithoutDeploymentAdmin_Throw()
        {
            // The company permission alone used to be enough, and `st_log_anomaly_db` carries every tenant's rows.
            var bo = Bo(new StubAuditLogRepository(HeaderPage(1)), authorized: true, deploymentAdmin: false);

            Assert.Throws<UserMessageException>(() => bo.GetDbAnomalyLog(new GetDbAnomalyLogArgs()));
            Assert.Throws<UserMessageException>(() => bo.GetDbAnomalySummary(new GetDbAnomalySummaryArgs()));
        }

        [Fact]
        [DisplayName("The DB anomaly methods serve a deployment administrator who has no company audit permission")]
        public void DbAnomalyMethods_DeploymentAdminWithoutCompanyPermission_Succeed()
        {
            var repo = new StubAuditLogRepository(HeaderPage(1));
            var bo = Bo(repo, authorized: false, deploymentAdmin: true);

            Assert.Single(bo.GetDbAnomalyLog(new GetDbAnomalyLogArgs()).Table!.Rows);
            Assert.Same(repo.AggregateResult, bo.GetDbAnomalySummary(new GetDbAnomalySummaryArgs()).Table);
        }

        [Fact]
        [DisplayName("The DB anomaly methods ask the deployment authorization for ReadDbAnomalyLog")]
        public void DbAnomalyMethods_AskForReadDbAnomalyLog()
        {
            var deploymentAuth = new FakeDeploymentAuth(true);
            var repo = new StubAuditLogRepository(HeaderPage(0));
            var ctx = TestBusinessObjectContext.CreateWithOverrides(_fx,
                (typeof(ICompanyAuthorizationService), new FakeAuth(false)),
                (typeof(IDeploymentAuthorizationService), deploymentAuth),
                (typeof(IRepositoryFactory), new StubAuditLogRepositoryFactory(repo)));
            var bo = new AuditLogBusinessObject(ctx, TestSessionFactory.CreateAccessToken(_fx), SysProgIds.AuditLog);

            bo.GetDbAnomalyLog(new GetDbAnomalyLogArgs());

            Assert.Equal(DeploymentAction.ReadDbAnomalyLog, deploymentAuth.LastAction);
        }

        // ---- anomaly aggregates ----

        [Fact]
        [DisplayName("GetApiAnomalySummary returns the aggregated Table")]
        public void GetApiAnomalySummary_Authorized_ReturnsTable()
        {
            var repo = new StubAuditLogRepository(HeaderPage(0));
            var result = Bo(repo).GetApiAnomalySummary(new GetApiAnomalySummaryArgs());
            Assert.Same(repo.AggregateResult, result.Table);
        }

        [Fact]
        [DisplayName("GetDbAnomalySummary uses the DB aggregation (no company scope)")]
        public void GetDbAnomalySummary_Authorized_UsesDbSummary()
        {
            var repo = new StubAuditLogRepository(HeaderPage(0));
            var result = Bo(repo).GetDbAnomalySummary(new GetDbAnomalySummaryArgs());
            Assert.Same(repo.AggregateResult, result.Table);
            Assert.True(repo.DbSummaryCalled);
        }

        [Fact]
        [DisplayName("GetTopApiMethods passes TopN through to the repository")]
        public void GetTopApiMethods_Authorized_PassesTopN()
        {
            var repo = new StubAuditLogRepository(HeaderPage(0));
            var result = Bo(repo).GetTopApiMethods(new GetTopApiMethodsArgs { TopN = 7 });
            Assert.Same(repo.AggregateResult, result.Table);
            Assert.Equal(7, repo.LastTopN);
        }

        [Fact]
        [DisplayName("The aggregate methods throw UserMessageException when not authorized")]
        public void AggregateMethods_NotAuthorized_Throw()
        {
            var bo = Bo(new StubAuditLogRepository(HeaderPage(0)), authorized: false);
            Assert.Throws<UserMessageException>(() => bo.GetApiAnomalySummary(new GetApiAnomalySummaryArgs()));
            Assert.Throws<UserMessageException>(() => bo.GetTopApiMethods(new GetTopApiMethodsArgs()));
        }

        // ---- helpers ----

        private static DataTable HeaderTable()
        {
            var t = new DataTable("st_log_change");
            t.Columns.Add("sys_rowid", typeof(Guid));
            t.Columns.Add("log_time", typeof(DateTime));
            t.Columns.Add("user_id", typeof(string));
            t.Columns.Add("user_name", typeof(string));
            t.Columns.Add("company_id", typeof(string));
            t.Columns.Add("company_name", typeof(string));
            t.Columns.Add("prog_id", typeof(string));
            t.Columns.Add("row_key", typeof(string));
            t.Columns.Add("change_kind", typeof(int));
            t.Columns.Add("is_sensitive", typeof(bool));
            t.Columns.Add("source", typeof(string));
            return t;
        }

        private static AuditLogPage HeaderPage(int rows)
        {
            var t = HeaderTable();
            var logTime = new DateTime(2026, 7, 8, 3, 0, 0, DateTimeKind.Utc);
            for (int i = 0; i < rows; i++)
            {
                t.Rows.Add(Guid.NewGuid(), logTime, "demo", "Demo User", "c1", "Company One",
                    ProgId, Guid.NewGuid().ToString(), (int)ChangeKind.Update, false, ProgId + ".Save");
            }
            return new AuditLogPage { Table = t, Paging = new PagingInfo { Page = 1, PageSize = 50, HasMore = false } };
        }

        private static DataTable DetailRow(Guid sysRowId, string rowKey, ChangeKind kind, string changesXml)
        {
            var t = HeaderTable();
            t.Columns.Add("changes_xml", typeof(string));
            t.Rows.Add(sysRowId, new DateTime(2026, 7, 8, 3, 0, 0, DateTimeKind.Utc), "demo", "Demo User",
                "c1", "Company One", ProgId, rowKey, (int)kind, false, ProgId + ".Save", changesXml);
            return t;
        }

        private static string BuildModifyDiffGram(string tableName, string rowKey, string column, string oldValue, string newValue)
        {
            var ds = new DataSet("Root");
            var table = ds.Tables.Add(tableName);
            table.Columns.Add("sys_rowid", typeof(string));
            table.Columns.Add(column, typeof(string));
            var row = table.Rows.Add(rowKey, oldValue);
            ds.AcceptChanges();
            row[column] = newValue;
            using var changes = ds.GetChanges()!;
            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            changes.WriteXml(writer, XmlWriteMode.DiffGram);
            return writer.ToString();
        }

        private sealed class FakeAuth : ICompanyAuthorizationService
        {
            private readonly bool _allowed;
            public FakeAuth(bool allowed) { _allowed = allowed; }
            public bool Can(Guid accessToken, string modelId, PermissionActions action) => _allowed;
        }

        private sealed class FakeDeploymentAuth : IDeploymentAuthorizationService
        {
            private readonly bool _allowed;
            public FakeDeploymentAuth(bool allowed) { _allowed = allowed; }
            public DeploymentAction? LastAction { get; private set; }
            public bool Can(Guid accessToken, DeploymentAction action)
            {
                LastAction = action;
                return _allowed;
            }
        }

        private sealed class StubAuditLogRepository : IAuditLogRepository
        {
            private readonly AuditLogPage _page;
            private readonly DataTable? _detail;
            public ChangeLogQuery? LastQuery { get; private set; }
            public PagingOptions? LastPaging { get; private set; }
            public Guid? LastDetailId { get; private set; }
            public object? LastListQuery { get; private set; }

            public StubAuditLogRepository(AuditLogPage page, DataTable? detail = null)
            {
                _page = page;
                _detail = detail;
            }

            public AuditLogPage GetChangeLog(ChangeLogQuery query, PagingOptions paging)
            {
                LastQuery = query;
                LastPaging = paging;
                return _page;
            }

            public DataTable? GetChangeById(Guid sysRowId, string? companyId)
            {
                LastDetailId = sysRowId;
                return _detail;
            }

            public AuditLogPage GetLoginLog(LoginLogQuery query, PagingOptions paging) { LastListQuery = query; LastPaging = paging; return _page; }
            public AuditLogPage GetAccessLog(AccessLogQuery query, PagingOptions paging) { LastListQuery = query; LastPaging = paging; return _page; }
            public AuditLogPage GetApiAnomalyLog(ApiAnomalyLogQuery query, PagingOptions paging) { LastListQuery = query; LastPaging = paging; return _page; }
            public AuditLogPage GetDbAnomalyLog(DbAnomalyLogQuery query, PagingOptions paging) { LastListQuery = query; LastPaging = paging; return _page; }

            public DataTable AggregateResult { get; } = new DataTable("agg");
            public int? LastTopN { get; private set; }
            public bool DbSummaryCalled { get; private set; }
            public DataTable GetApiAnomalySummary(DateTime? fromUtc, DateTime? toUtc, string? companyId) => AggregateResult;
            public DataTable GetDbAnomalySummary(DateTime? fromUtc, DateTime? toUtc) { DbSummaryCalled = true; return AggregateResult; }
            public DataTable GetTopApiMethods(DateTime? fromUtc, DateTime? toUtc, int topN, string? companyId) { LastTopN = topN; return AggregateResult; }
        }

        private sealed class StubAuditLogRepositoryFactory : IRepositoryFactory
        {
            private readonly IAuditLogRepository _repo;
            public StubAuditLogRepositoryFactory(IAuditLogRepository repo) { _repo = repo; }

            public T Create<T>(Guid accessToken = default) where T : class
                => typeof(T) == typeof(IAuditLogRepository)
                    ? (T)_repo
                    : throw new NotSupportedException(typeof(T).FullName);

            public T CreateFormRepository<T>(Guid accessToken, string progId) where T : class, IDataFormRepository
                => throw new NotSupportedException();
        }
    }
}
