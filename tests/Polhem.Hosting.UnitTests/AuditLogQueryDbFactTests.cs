using System.ComponentModel;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Definition.Logging;
using Polhem.Definition.Paging;
using Polhem.Repository.AuditLog;
using Polhem.Repository.Abstractions.AuditLog;
using Polhem.Tests.Shared;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// Read-side integration tests, one per dialect: a few <c>st_log_change</c> rows are written to the log database
    /// through the write-side sink and read back with <see cref="AuditLogRepository"/>, checking the parameterized
    /// filters of <c>GetChangeLog</c>, the <c>log_time</c> DESC order, the company filter, paging (TotalCount /
    /// HasMore), and the single-row read and company scope of <c>GetChangeById</c>. Each test is skipped
    /// automatically when its <c>POLHEM_TEST_CONNSTR_*</c> is not set.
    /// </summary>
    public class AuditLogQueryDbFactTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public AuditLogQueryDbFactTests(SharedDbFixture fx) { _fx = fx; }

        private void RunChangeLogQuery(DatabaseType databaseType)
        {
            var databaseId = TestDbConventions.GetDatabaseId(databaseType, "log");
            var connectionManager = _fx.GetRequiredService<IDbConnectionManager>();
            var dbAccess = new Polhem.Db.DbAccess(databaseId, connectionManager);

            // Unique keys so the append-only log stays isolated from other rows / reruns / processes.
            const string progId = "Employee";
            var rowKey = Guid.NewGuid().ToString();
            var older = new DateTime(2026, 7, 8, 1, 0, 0, DateTimeKind.Utc);
            var newer = new DateTime(2026, 7, 8, 2, 0, 0, DateTimeKind.Utc);
            var olderId = Guid.NewGuid();
            var newerId = Guid.NewGuid();

            var otherCompanyId = Guid.NewGuid();

            // Two rows belong to c1 (for ordering and paging) and one to c2 (for tenant isolation).
            dbAccess.Execute(AuditLogWriteRepository.BuildInsert(ChangeEntry(olderId, progId, rowKey, "c1", ChangeKind.Insert, older)));
            dbAccess.Execute(AuditLogWriteRepository.BuildInsert(ChangeEntry(newerId, progId, rowKey, "c1", ChangeKind.Update, newer)));
            dbAccess.Execute(AuditLogWriteRepository.BuildInsert(ChangeEntry(otherCompanyId, progId, rowKey, "c2", ChangeKind.Update, newer)));

            var repository = new AuditLogRepository(TestRepositoryContext.Create(connectionManager), databaseId);
            var byRecord = new ChangeLogQuery { ProgId = progId, RowKey = rowKey };
            var inC1 = new ChangeLogQuery { ProgId = progId, RowKey = rowKey, CompanyId = "c1" };

            // No company scope → refused. The company filter is a tenant boundary, not an optional
            // filter: dropping the clause returned every company's audit trail. This replaces an
            // assertion that expected the opposite ("no company filter → both rows") — that
            // behaviour was the defect, not a decision.
            Assert.Throws<InvalidOperationException>(
                () => repository.GetChangeLog(byRecord, new PagingOptions { PageSize = 50 }));

            // Scoped to c1 → its two rows, newest first (log_time DESC).
            var all = repository.GetChangeLog(inC1, new PagingOptions { PageSize = 50 });
            Assert.Equal(2, all.Table.Rows.Count);
            Assert.Equal(newerId, (Guid)all.Table.Rows[0]["sys_rowid"]);
            Assert.Equal(olderId, (Guid)all.Table.Rows[1]["sys_rowid"]);
            // Header projection excludes the heavy DiffGram payload.
            Assert.False(all.Table.Columns.Contains("changes_xml"));

            // Tenant isolation: c2 sees only its own row, never c1's.
            var inC2 = repository.GetChangeLog(
                new ChangeLogQuery { ProgId = progId, RowKey = rowKey, CompanyId = "c2" },
                new PagingOptions { PageSize = 50 });
            Assert.Single(inC2.Table.Rows);
            Assert.Equal(otherCompanyId, (Guid)inC2.Table.Rows[0]["sys_rowid"]);

            // Paging: PageSize 1 with total count → 1 row, TotalCount 2, HasMore true.
            var firstPage = repository.GetChangeLog(inC1, new PagingOptions { Page = 1, PageSize = 1, IncludeTotalCount = true });
            Assert.Single(firstPage.Table.Rows);
            Assert.Equal(newerId, (Guid)firstPage.Table.Rows[0]["sys_rowid"]);
            Assert.Equal(2, firstPage.Paging.TotalCount);
            Assert.True(firstPage.Paging.HasMore);

            var secondPage = repository.GetChangeLog(inC1, new PagingOptions { Page = 2, PageSize = 1, IncludeTotalCount = true });
            Assert.Single(secondPage.Table.Rows);
            Assert.Equal(olderId, (Guid)secondPage.Table.Rows[0]["sys_rowid"]);
            Assert.False(secondPage.Paging.HasMore);

            // GetChangeById → the row incl. changes_xml; out-of-company scope → null.
            var detail = repository.GetChangeById(olderId, companyId: "c1");
            Assert.NotNull(detail);
            Assert.Single(detail!.Rows);
            Assert.True(detail.Columns.Contains("changes_xml"));
            Assert.Null(repository.GetChangeById(olderId, companyId: "c2"));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: GetChangeLog ordering, filtering and paging and GetChangeById are correct")]
        public void ChangeLog_SqlServer() => RunChangeLogQuery(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: GetChangeLog ordering, filtering and paging and GetChangeById are correct")]
        public void ChangeLog_PostgreSQL() => RunChangeLogQuery(DatabaseType.PostgreSQL);

        private void RunOtherAxisQueries(DatabaseType databaseType)
        {
            var databaseId = TestDbConventions.GetDatabaseId(databaseType, "log");
            var connectionManager = _fx.GetRequiredService<IDbConnectionManager>();
            var dbAccess = new Polhem.Db.DbAccess(databaseId, connectionManager);
            var repository = new AuditLogRepository(TestRepositoryContext.Create(connectionManager), databaseId);

            // Login: write one failed-login for a unique user, read it back by user + event filter.
            // Every axis must carry a tenant scope: the read side now requires it (the company is a tenant
            // boundary, not an optional filter).
            const string CompanyId = "c_axes";
            var loginUser = "u_" + Guid.NewGuid().ToString("N");
            dbAccess.Execute(AuditLogWriteRepository.BuildInsert(new LoginAuditEntry
            {
                SysRowId = Guid.NewGuid(),
                LogTimeUtc = new DateTime(2026, 7, 8, 1, 0, 0, DateTimeKind.Utc),
                UserId = loginUser,
                UserName = "U",
                CompanyId = CompanyId,
                Event = LoginEvent.LoginFailed,
                FailReason = "bad password",
            }));
            var loginPage = repository.GetLoginLog(
                new LoginLogQuery { CompanyId = CompanyId, UserId = loginUser, Event = LoginEvent.LoginFailed },
                new PagingOptions { PageSize = 10, IncludeTotalCount = true });
            Assert.Single(loginPage.Table.Rows);
            Assert.Equal(1, loginPage.Paging.TotalCount);

            // Access: write one record-view for a unique prog+row.
            var accessRow = Guid.NewGuid().ToString();
            dbAccess.Execute(AuditLogWriteRepository.BuildInsert(new AccessAuditEntry
            {
                SysRowId = Guid.NewGuid(),
                LogTimeUtc = new DateTime(2026, 7, 8, 1, 0, 0, DateTimeKind.Utc),
                UserId = "demo",
                UserName = "Demo",
                CompanyId = CompanyId,
                ProgId = "Order",
                RowKey = accessRow,
            }));
            var accessPage = repository.GetAccessLog(
                new AccessLogQuery { CompanyId = CompanyId, ProgId = "Order", RowKey = accessRow }, new PagingOptions { PageSize = 10 });
            Assert.Single(accessPage.Table.Rows);

            // API anomaly: write one Slow for a unique method.
            var method = "M_" + Guid.NewGuid().ToString("N");
            dbAccess.Execute(AuditLogWriteRepository.BuildInsert(new ApiAnomalyEntry
            {
                SysRowId = Guid.NewGuid(),
                LogTimeUtc = new DateTime(2026, 7, 8, 1, 0, 0, DateTimeKind.Utc),
                UserId = "demo",
                CompanyId = CompanyId,
                Method = method,
                Kind = AnomalyKind.Slow,
                ElapsedMs = 5000,
                ThresholdMs = 3000,
            }));
            var apiPage = repository.GetApiAnomalyLog(
                new ApiAnomalyLogQuery { CompanyId = CompanyId, Method = method, Kind = AnomalyKind.Slow }, new PagingOptions { PageSize = 10 });
            Assert.Single(apiPage.Table.Rows);

            // DB anomaly: write one Timeout for a unique database id (no company dimension).
            var dbId = "d_" + Guid.NewGuid().ToString("N");
            dbAccess.Execute(AuditLogWriteRepository.BuildInsert(new DbAnomalyEntry
            {
                SysRowId = Guid.NewGuid(),
                LogTimeUtc = new DateTime(2026, 7, 8, 1, 0, 0, DateTimeKind.Utc),
                DatabaseId = dbId,
                Command = "UPDATE ft_order SET amount={0}",
                Kind = AnomalyKind.Timeout,
                ElapsedMs = 30000,
                ErrorType = "DbException",
                ErrorMessage = "timeout expired",
            }));
            var dbPage = repository.GetDbAnomalyLog(
                new DbAnomalyLogQuery { DatabaseId = dbId, Kind = AnomalyKind.Timeout }, new PagingOptions { PageSize = 10 });
            Assert.Single(dbPage.Table.Rows);
            Assert.True(dbPage.Table.Columns.Contains("command"));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: GetLoginLog, GetAccessLog, GetApiAnomalyLog and GetDbAnomalyLog filter correctly")]
        public void OtherAxes_SqlServer() => RunOtherAxisQueries(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: GetLoginLog, GetAccessLog, GetApiAnomalyLog and GetDbAnomalyLog filter correctly")]
        public void OtherAxes_PostgreSQL() => RunOtherAxisQueries(DatabaseType.PostgreSQL);

        private void RunAggregateQueries(DatabaseType databaseType)
        {
            var databaseId = TestDbConventions.GetDatabaseId(databaseType, "log");
            var connectionManager = _fx.GetRequiredService<IDbConnectionManager>();
            var dbAccess = new Polhem.Db.DbAccess(databaseId, connectionManager);
            var repository = new AuditLogRepository(TestRepositoryContext.Create(connectionManager), databaseId);

            // Seed a unique method with 3 Slow anomalies within a tight window so the aggregates are
            // isolated from other rows in the shared append-only table.
            var method = "AGG_" + Guid.NewGuid().ToString("N");
            const string CompanyId = "c_agg";
            // A fresh real-time window per run bounds contamination of the shared append-only table to
            // recent runs, so the method-isolated top-N stays reliable across reruns / parallel processes.
            var baseTime = DateTime.UtcNow;
            var from = baseTime.AddMinutes(-1);
            var to = baseTime.AddMinutes(1);
            for (int i = 0; i < 3; i++)
            {
                dbAccess.Execute(AuditLogWriteRepository.BuildInsert(new ApiAnomalyEntry
                {
                    SysRowId = Guid.NewGuid(),
                    LogTimeUtc = baseTime.AddSeconds(i),
                    UserId = "demo",
                    CompanyId = CompanyId,
                    Method = method,
                    Kind = AnomalyKind.Slow,
                    ElapsedMs = 5000 + i,
                    ThresholdMs = 3000,
                }));
            }

            // API summary groups by anomaly_kind across ALL methods in the window; the shared append-only
            // table means other rows / prior runs may add to the Slow bucket, so assert it includes our 3
            // (exact-count correctness is covered by the method-isolated GetTopApiMethods below).
            var apiSummary = repository.GetApiAnomalySummary(from, to, CompanyId);
            var slowRow = apiSummary.Select("anomaly_kind = " + (int)AnomalyKind.Slow);
            Assert.Single(slowRow);
            Assert.True(Convert.ToInt64(slowRow[0]["event_count"], System.Globalization.CultureInfo.InvariantCulture) >= 3L);

            // Top API methods within the window → our method present with count 3 + max elapsed 5002.
            var top = repository.GetTopApiMethods(from, to, topN: 10, CompanyId);
            var methodRow = top.Select("method = '" + method + "'");
            Assert.Single(methodRow);
            Assert.Equal(3L, Convert.ToInt64(methodRow[0]["event_count"], System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(5002L, Convert.ToInt64(methodRow[0]["max_elapsed_ms"], System.Globalization.CultureInfo.InvariantCulture));

            // DB summary is a smoke check: seed one Timeout, confirm the grouped table comes back.
            dbAccess.Execute(AuditLogWriteRepository.BuildInsert(new DbAnomalyEntry
            {
                SysRowId = Guid.NewGuid(),
                LogTimeUtc = from,
                DatabaseId = "company",
                Command = "UPDATE ft_x SET a={0}",
                Kind = AnomalyKind.Timeout,
                ElapsedMs = 30000,
            }));
            var dbSummary = repository.GetDbAnomalySummary(from, to);
            Assert.True(dbSummary.Columns.Contains("event_count"));
            Assert.NotEmpty(dbSummary.Rows);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: GetApiAnomalySummary, GetTopApiMethods and GetDbAnomalySummary aggregate correctly")]
        public void Aggregates_SqlServer() => RunAggregateQueries(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: GetApiAnomalySummary, GetTopApiMethods and GetDbAnomalySummary aggregate correctly")]
        public void Aggregates_PostgreSQL() => RunAggregateQueries(DatabaseType.PostgreSQL);

        private static ChangeAuditEntry ChangeEntry(Guid sysRowId, string progId, string rowKey, string companyId, ChangeKind kind, DateTime logTimeUtc)
            => new ChangeAuditEntry
            {
                SysRowId = sysRowId,
                LogTimeUtc = logTimeUtc,
                UserId = "demo",
                UserName = "Demo User",
                CompanyId = companyId,
                CompanyName = "Company " + companyId,
                ProgId = progId,
                ChangeTableName = "st_employee",
                RowKey = rowKey,
                ChangeKind = kind,
                IsSensitive = false,
                ChangesXml = "<diffgr:diffgram xmlns:diffgr=\"urn:schemas-microsoft-com:xml-diffgram-v1\" />",
            };
    }
}
