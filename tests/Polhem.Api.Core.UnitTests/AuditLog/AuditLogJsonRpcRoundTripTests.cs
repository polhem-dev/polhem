using System.ComponentModel;
using System.Data;
using System.Text;
using System.Xml;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages.AuditLog;
using Polhem.Business;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Logging;
using Polhem.Definition.Paging;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.Repository.Abstractions.AuditLog;
using Polhem.Repository.Abstractions.Form;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Tests.Shared;

namespace Polhem.Api.Core.UnitTests.AuditLog
{
    /// <summary>
    /// An end-to-end round trip through <see cref="JsonRpcExecutor"/>: the <c>AuditLog.*</c> actions are dispatched
    /// through the dispatch branch to <see cref="Polhem.Business.AuditLog.LogBusinessObject"/>, and a stub repository
    /// returns known data, verifying the axis routing and the input/output converters. A fake
    /// ICompanyAuthorizationService grants permission; no real database is involved.
    /// </summary>
    public class AuditLogJsonRpcRoundTripTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public AuditLogJsonRpcRoundTripTests(PolhemTestFixture fx) { _fx = fx; }

        private JsonRpcResponse Dispatch(StubAuditLogRepository repo, string action, object request)
        {
            var overrideServices = new TestOverrideServiceProvider(
                _fx.Provider,
                (typeof(ICompanyAuthorizationService), new FakeAuth()),
                (typeof(IDeploymentAuthorizationService), new FakeDeploymentAuth()),
                (typeof(IRepositoryFactory), new StubAuditLogRepositoryFactory(repo)));

            var boFactory = new BusinessObjectFactory(
                overrideServices,
                _fx.GetRequiredService<IDefineAccess>(),
                _fx.GetRequiredService<ISessionInfoService>(),
                _fx.GetRequiredService<ILanguageService>(),
                _fx.GetRequiredService<IBoTypeResolver>());

            var executor = new JsonRpcExecutor(
                boFactory,
                _fx.GetRequiredService<IAccessTokenValidator>(),
                _fx.GetRequiredService<IApiEncryptionKeyProvider>())
            {
                AccessToken = TestSessionFactory.CreateAccessToken(_fx),
                IsLocalCall = true,
            };

            return executor.Execute(new JsonRpcRequest
            {
                Method = $"{SysProgIds.AuditLog}.{action}",
                Params = new JsonRpcParams { Value = request },
                Id = Guid.NewGuid().ToString(),
            });
        }

        [Fact]
        [DisplayName("AuditLog.GetChangeLog dispatches through the executor and returns the header DataTable with paging")]
        public void GetChangeLog_ThroughJsonRpc_Dispatches()
        {
            var repo = new StubAuditLogRepository(HeaderPage(2), null);
            var response = Dispatch(repo, LogActions.GetChangeLog,
                new GetChangeLogRequest { ProgId = "Employee", ChangeKind = ChangeKind.Update });

            Assert.Null(response.Error);
            var result = Assert.IsType<LogListResponse>(response.Result!.Value);
            Assert.Equal(2, result.Table!.Rows.Count);
            Assert.NotNull(result.Paging);
        }

        [Fact]
        [DisplayName("AuditLog.GetChangeDetail dispatches through the executor and returns the restored header with no DataSet for an empty payload")]
        public void GetChangeDetail_ThroughJsonRpc_Dispatches()
        {
            var sysRowId = Guid.NewGuid();
            var repo = new StubAuditLogRepository(HeaderPage(0), DetailRow(sysRowId));
            var response = Dispatch(repo, LogActions.GetChangeDetail,
                new GetChangeDetailRequest { SysRowId = sysRowId });

            Assert.Null(response.Error);
            var result = Assert.IsType<GetChangeDetailResponse>(response.Result!.Value);
            Assert.Equal(sysRowId, result.SysRowId);
            Assert.Equal(ChangeKind.Insert, result.ChangeKind);
            Assert.Null(result.DataSet);
        }

        [Fact]
        [DisplayName("AuditLog.GetChangeDetail through the executor carries the restored DataSet to the wire response")]
        public void GetChangeDetail_ThroughJsonRpc_CarriesDataSet()
        {
            var sysRowId = Guid.NewGuid();
            var repo = new StubAuditLogRepository(HeaderPage(0),
                DetailRow(sysRowId, ChangeKind.Update, SchemaBoundChangePayload()));

            var response = Dispatch(repo, LogActions.GetChangeDetail,
                new GetChangeDetailRequest { SysRowId = sysRowId });

            Assert.Null(response.Error);
            var result = Assert.IsType<GetChangeDetailResponse>(response.Result!.Value);
            Assert.NotNull(result.DataSet);
            AuditLogMessagePackTests.AssertChangeDataSet(result.DataSet!);
            Assert.Single(result.Fields);
        }

        /// <summary>
        /// Writes a change-set payload with an embedded schema by hand. The writer, <c>AuditDiffGram</c>, is internal to
        /// <c>Polhem.Business</c> and invisible to this test assembly, so its shape (outer element + XSD + DiffGram) is
        /// written out directly.
        /// </summary>
        private static string SchemaBoundChangePayload()
        {
            using var dataSet = AuditLogMessagePackTests.NewChangeDataSet();
            using var changes = dataSet.GetChanges()!;
            // GetChanges drops the unchanged row; put it back so the payload carries both states.
            changes.Tables["Employee"]!.ImportRow(dataSet.Tables["Employee"]!.Rows[1]);
            var builder = new StringBuilder();
            using (var writer = XmlWriter.Create(builder,
                new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true }))
            {
                writer.WriteStartElement("AuditChanges");
                changes.WriteXmlSchema(writer);
                changes.WriteXml(writer, XmlWriteMode.DiffGram);
                writer.WriteEndElement();
            }
            return builder.ToString();
        }

        [Theory]
        [InlineData("GetLoginLog")]
        [InlineData("GetAccessLog")]
        [InlineData("GetApiAnomalyLog")]
        [InlineData("GetDbAnomalyLog")]
        [DisplayName("Each AuditLog list action dispatches through the executor and returns a LogListResponse")]
        public void ListActions_ThroughJsonRpc_ReturnLogListResponse(string action)
        {
            var repo = new StubAuditLogRepository(HeaderPage(2), null);
            object request = action switch
            {
                "GetLoginLog" => new GetLoginLogRequest { UserId = "demo", Event = LoginEvent.LoginFailed },
                "GetAccessLog" => new GetAccessLogRequest { ProgId = "Order" },
                "GetApiAnomalyLog" => new GetApiAnomalyLogRequest { Kind = AnomalyKind.Slow },
                _ => new GetDbAnomalyLogRequest { DatabaseId = "company", Kind = AnomalyKind.Timeout },
            };

            var response = Dispatch(repo, action, request);

            Assert.Null(response.Error);
            var result = Assert.IsType<LogListResponse>(response.Result!.Value);
            Assert.Equal(2, result.Table!.Rows.Count);
            Assert.NotNull(result.Paging);
        }

        [Theory]
        [InlineData("GetApiAnomalySummary")]
        [InlineData("GetDbAnomalySummary")]
        [InlineData("GetTopApiMethods")]
        [DisplayName("Each AuditLog aggregate action dispatches through the executor and returns a LogAggregateResponse")]
        public void AggregateActions_ThroughJsonRpc_ReturnLogAggregateResponse(string action)
        {
            var repo = new StubAuditLogRepository(HeaderPage(0), null);
            object request = action switch
            {
                "GetApiAnomalySummary" => new GetApiAnomalySummaryRequest(),
                "GetDbAnomalySummary" => new GetDbAnomalySummaryRequest(),
                _ => new GetTopApiMethodsRequest { TopN = 5 },
            };

            var response = Dispatch(repo, action, request);

            Assert.Null(response.Error);
            var result = Assert.IsType<LogAggregateResponse>(response.Result!.Value);
            Assert.NotNull(result.Table);
            Assert.Single(result.Table!.Rows);
        }

        private static DataTable HeaderTable(bool withChangesXml = false)
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
            if (withChangesXml) { t.Columns.Add("changes_xml", typeof(string)); }
            return t;
        }

        private static AuditLogPage HeaderPage(int rows)
        {
            var t = HeaderTable();
            for (int i = 0; i < rows; i++)
            {
                t.Rows.Add(Guid.NewGuid(), new DateTime(2026, 7, 8, 3, 0, 0, DateTimeKind.Utc),
                    "demo", "Demo User", "c1", "Company One", "Employee", Guid.NewGuid().ToString(),
                    (int)ChangeKind.Update, false, "Employee.Save");
            }
            return new AuditLogPage { Table = t, Paging = new PagingInfo { Page = 1, PageSize = 50 } };
        }

        // Empty (non-DiffGram) payload: the header still maps; Fields is empty.
        private static DataTable DetailRow(Guid sysRowId)
            => DetailRow(sysRowId, ChangeKind.Insert, string.Empty);

        private static DataTable DetailRow(Guid sysRowId, ChangeKind kind, string changesXml)
        {
            var t = HeaderTable(withChangesXml: true);
            t.Rows.Add(sysRowId, new DateTime(2026, 7, 8, 3, 0, 0, DateTimeKind.Utc),
                "demo", "Demo User", "c1", "Company One", "Employee", "R-1",
                (int)kind, false, "Employee.Save", changesXml);
            return t;
        }

        private sealed class FakeAuth : ICompanyAuthorizationService
        {
            public bool Can(Guid accessToken, string modelId, PermissionAction action) => true;
        }

        private sealed class FakeDeploymentAuth : IDeploymentAuthorizationService
        {
            public bool Can(Guid accessToken, DeploymentAction action) => true;
        }

        private sealed class StubAuditLogRepository : IAuditLogRepository
        {
            private readonly AuditLogPage _page;
            private readonly DataTable? _detail;
            public StubAuditLogRepository(AuditLogPage page, DataTable? detail) { _page = page; _detail = detail; }
            public AuditLogPage GetChangeLog(ChangeLogQuery query, PagingOptions paging) => _page;
            public DataTable? GetChangeById(Guid sysRowId, string? companyId) => _detail;
            public AuditLogPage GetLoginLog(LoginLogQuery query, PagingOptions paging) => _page;
            public AuditLogPage GetAccessLog(AccessLogQuery query, PagingOptions paging) => _page;
            public AuditLogPage GetApiAnomalyLog(ApiAnomalyLogQuery query, PagingOptions paging) => _page;
            public AuditLogPage GetDbAnomalyLog(DbAnomalyLogQuery query, PagingOptions paging) => _page;

            private static DataTable Agg() { var t = new DataTable("agg"); t.Columns.Add("anomaly_kind", typeof(int)); t.Rows.Add((int)AnomalyKind.Slow); return t; }
            public DataTable GetApiAnomalySummary(DateTime? fromUtc, DateTime? toUtc, string? companyId) => Agg();
            public DataTable GetDbAnomalySummary(DateTime? fromUtc, DateTime? toUtc) => Agg();
            public DataTable GetTopApiMethods(DateTime? fromUtc, DateTime? toUtc, int topN, string? companyId) => Agg();
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
