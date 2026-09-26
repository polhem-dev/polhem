using System.ComponentModel;
using Polhem.Base.Exceptions;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Identity;

namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// Behavior tests for <see cref="RepositoryDatabaseRouter"/>. ISessionInfoService and ICompanyInfoService are
    /// stubbed, so no physical DB is needed. The tests focus on the DbScope → databaseId resolution paths and the
    /// error branches.
    /// </summary>
    public class RepositoryDatabaseRouterTests
    {
        #region Stubs

        private sealed class StubSessionInfoService : ISessionInfoService
        {
            private readonly Dictionary<Guid, SessionInfo> _store = [];

            public SessionInfo Get(Guid accessToken)
                => _store.TryGetValue(accessToken, out var info) ? info : null!;

            public void Set(SessionInfo sessionInfo) => _store[sessionInfo.AccessToken] = sessionInfo;

            public void Remove(Guid accessToken) => _store.Remove(accessToken);
        }

        private sealed class StubCompanyInfoService : ICompanyInfoService
        {
            private readonly Dictionary<string, CompanyInfo> _store = [];

            public CompanyInfo? Get(string companyId)
                => _store.TryGetValue(companyId, out var info) ? info : null;

            public void Set(CompanyInfo companyInfo) => _store[companyInfo.CompanyId] = companyInfo;

            public void Remove(string companyId) => _store.Remove(companyId);
        }

        private static (RepositoryDatabaseRouter router, StubSessionInfoService sessions, StubCompanyInfoService companies) NewRouter()
        {
            var sessions = new StubSessionInfoService();
            var companies = new StubCompanyInfoService();
            return (new RepositoryDatabaseRouter(sessions, companies), sessions, companies);
        }

        #endregion

        [Fact]
        [DisplayName("Resolve(Common) returns the fixed \"common\" without a session")]
        public void Resolve_Common_ReturnsCommon()
        {
            var (router, _, _) = NewRouter();
            Assert.Equal(DbCategoryIds.Common, router.Resolve(DbScope.Common, Guid.Empty));
        }

        [Fact]
        [DisplayName("Resolve(Log) returns the fixed \"log\" without a session (so logs can be written before EnterCompany)")]
        public void Resolve_Log_ReturnsLog()
        {
            var (router, _, _) = NewRouter();
            Assert.Equal(DbCategoryIds.Log, router.Resolve(DbScope.Log, Guid.Empty));
        }

        [Fact]
        [DisplayName("Resolve(Common/Log) still returns the fixed databaseId with Guid.Empty")]
        public void Resolve_CommonAndLogWithEmptyAccessToken_ReturnsFixedDatabaseId()
        {
            var (router, _, _) = NewRouter();
            Assert.Equal(DbCategoryIds.Common, router.Resolve(DbScope.Common, Guid.Empty));
            Assert.Equal(DbCategoryIds.Log, router.Resolve(DbScope.Log, Guid.Empty));
        }

        [Fact]
        [DisplayName("Resolve(Company) returns CompanyDatabaseId when both the session and CompanyInfo exist")]
        public void Resolve_CompanyWithSession_ReturnsCompanyDatabaseId()
        {
            var (router, sessions, companies) = NewRouter();
            var token = Guid.NewGuid();
            sessions.Set(new SessionInfo { AccessToken = token, UserId = "u", CompanyId = "C001" });
            companies.Set(new CompanyInfo { CompanyId = "C001", CompanyDatabaseId = "biz_shared_01" });

            Assert.Equal("biz_shared_01", router.Resolve(DbScope.Company, token));
        }

        [Fact]
        [DisplayName("Resolve(Company) throws UnauthorizedAccessException when there is no session")]
        public void Resolve_CompanyNoSession_ThrowsUnauthorized()
        {
            var (router, _, _) = NewRouter();
            Assert.Throws<UnauthorizedAccessException>(
                () => router.Resolve(DbScope.Company, Guid.NewGuid()));
        }

        [Fact]
        [DisplayName("Resolve(Company) throws CompanyNotEntered when the session has not entered a company")]
        public void Resolve_CompanySessionWithoutCompanyId_ThrowsCompanyNotEntered()
        {
            var (router, sessions, _) = NewRouter();
            var token = Guid.NewGuid();
            sessions.Set(new SessionInfo { AccessToken = token, UserId = "u", CompanyId = null });

            // Assert the type, not the message: the message ends up on the client's screen, and the error code is
            // what the front end uses to redirect to company selection. This test used to pin the error code name
            // as the message text, which amounted to showing the string "CompanyNotEntered" to the user.
            var ex = Assert.Throws<CompanyNotEnteredException>(
                () => router.Resolve(DbScope.Company, token));
            Assert.DoesNotContain("CompanyNotEntered", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Resolve(Company) throws InvalidOperationException without the CompanyId in the message on a CompanyInfo cache miss")]
        public void Resolve_CompanyInfoCacheMiss_ThrowsAndDoesNotLeakCompanyId()
        {
            var (router, sessions, _) = NewRouter();
            var token = Guid.NewGuid();
            sessions.Set(new SessionInfo { AccessToken = token, UserId = "u", CompanyId = "SECRET_C001" });

            var ex = Assert.Throws<InvalidOperationException>(
                () => router.Resolve(DbScope.Company, token));
            Assert.DoesNotContain("SECRET_C001", ex.Message);
        }

        [Fact]
        [DisplayName("Resolve(Company) returns the shared CompanyDatabaseId for each of several companies pointing to it")]
        public void Resolve_TwoCompaniesWithSameCompanyDatabaseId_BothReturnSameDatabaseId()
        {
            var (router, sessions, companies) = NewRouter();
            companies.Set(new CompanyInfo { CompanyId = "CA", CompanyDatabaseId = "biz_shared" });
            companies.Set(new CompanyInfo { CompanyId = "CB", CompanyDatabaseId = "biz_shared" });

            var tokenA = Guid.NewGuid();
            var tokenB = Guid.NewGuid();
            sessions.Set(new SessionInfo { AccessToken = tokenA, UserId = "ua", CompanyId = "CA" });
            sessions.Set(new SessionInfo { AccessToken = tokenB, UserId = "ub", CompanyId = "CB" });

            Assert.Equal("biz_shared", router.Resolve(DbScope.Company, tokenA));
            Assert.Equal("biz_shared", router.Resolve(DbScope.Company, tokenB));
        }

        [Fact]
        [DisplayName("Ctor throws ArgumentNullException for null services")]
        public void Ctor_NullServices_ThrowsArgumentNullException()
        {
            var companies = new StubCompanyInfoService();
            var sessions = new StubSessionInfoService();
            Assert.Throws<ArgumentNullException>(
                () => new RepositoryDatabaseRouter(null!, companies));
            Assert.Throws<ArgumentNullException>(
                () => new RepositoryDatabaseRouter(sessions, null!));
        }
    }
}
