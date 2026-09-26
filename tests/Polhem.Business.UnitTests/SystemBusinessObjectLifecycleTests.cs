using System.ComponentModel;
using Polhem.Business.System;
using Polhem.Business.UnitTests.Fakes;
using Polhem.Db;
using Polhem.Definition.Database;
using Polhem.Definition.Identity;
using Polhem.Tests.Shared;

using Polhem.Definition;
namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Integration test of the whole SystemBO session lifecycle. It chains Login → EnterCompany(A) →
    /// EnterCompany(B) → LeaveCompany → EnterCompany(A) → Logout and verifies that the session state transitions
    /// stay consistent across the four methods, with valid and invalid paths as separate cases.
    /// </summary>
    public class SystemBusinessObjectLifecycleTests : IClassFixture<SharedDbFixture>
    {
        // The company permission tables live in the company-category DB. `company_database_id` must point there
        // for `EnterCompany` to load the role snapshot. BO tests are bound to SQL Server.
        private static readonly string s_companyDbId = TestDbConventions.GetDatabaseId(DatabaseType.SQLServer, "company");
        private readonly SharedDbFixture _fx;

        public SystemBusinessObjectLifecycleTests(SharedDbFixture fx) { _fx = fx; }

        private static string UniqueCompanyId() => "C_" + Guid.NewGuid().ToString("N")[..12];

        [Fact]
        [DisplayName("The whole session lifecycle Login → EnterCompany(A) → EnterCompany(B) → LeaveCompany → EnterCompany(A) → Logout stays consistent")]
        public void FullLifecycle_LoginThroughLogout_TransitionsCorrectly()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            // companyA uses seed C001, which already maps to user '001'. companyB is created and granted here,
            // going through the real `st_company` / `st_user_company` path that the `HasAccess` check requires.
            const string companyA = "C001";
            var companyB = UniqueCompanyId();
            var (companyBRowId, grantBRowId) = InsertCompanyAndGrantForSeedUser(companyB);

            try
            {
                // 1. Login uses `TestableSystemBusinessObject` to bypass the default `AuthenticateUser=false`.
                // The user ID must match seed user '001', otherwise the `HasAccess` join finds no mapping.
                var loginBo = new TestableSystemBusinessObject(
                    TestPolhemContext.Create(_fx),
                    Guid.Empty,
                    _ => (true, "Integration User"));
                var loginResult = loginBo.Login(new LoginArgs { UserId = "001", Password = "pwd" });
                Assert.NotEqual(Guid.Empty, loginResult.AccessToken);
                var accessToken = loginResult.AccessToken;
                Assert.Null(sessionService.Get(accessToken)!.CompanyId);

                var bo = new SystemBusinessObject(TestPolhemContext.Create(_fx), accessToken, SysProgIds.System);

                // 2. EnterCompany(A): the first company entry.
                var enterA = bo.EnterCompany(new EnterCompanyArgs { CompanyId = companyA });
                Assert.Equal(companyA, enterA.Company.CompanyId);
                Assert.Equal(companyA, sessionService.Get(accessToken)!.CompanyId);

                // 3. EnterCompany(B): a switch that overwrites directly.
                var enterB = bo.EnterCompany(new EnterCompanyArgs { CompanyId = companyB });
                Assert.Equal(companyB, enterB.Company.CompanyId);
                Assert.Equal(companyB, sessionService.Get(accessToken)!.CompanyId);

                // 4. LeaveCompany: back to the state of no company entered.
                bo.LeaveCompany(new LeaveCompanyArgs());
                Assert.Null(sessionService.Get(accessToken)!.CompanyId);

                // 5. EnterCompany(A) again: re-entering after leaving leaves no residual state.
                var enterAAgain = bo.EnterCompany(new EnterCompanyArgs { CompanyId = companyA });
                Assert.Equal(companyA, enterAAgain.Company.CompanyId);
                Assert.Equal(companyA, sessionService.Get(accessToken)!.CompanyId);

                // 6. Logout implies the LeaveCompany cleanup, and the whole session disappears from the cache.
                bo.Logout(new LogoutArgs());
                Assert.Null(sessionService.Get(accessToken));
            }
            finally
            {
                DeleteGrantAndCompany(grantBRowId, companyBRowId);
            }
        }

        // BO integration tests bind only the `common` database ID (SQL Server), so the helper writes the SQL Server dialect.
        private (Guid companyRowId, Guid grantRowId) InsertCompanyAndGrantForSeedUser(string companyId)
        {
            var dbAccess = _fx.NewDbAccess("common");
            var companyRowId = Guid.NewGuid();
            var grantRowId = Guid.NewGuid();

            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                "INSERT INTO st_company (sys_rowid, sys_id, sys_name, company_database_id, number_formats_xml, default_currency, cash_rounding_xml, allowed_currencies_xml, enabled, sys_insert_time) " +
                "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, 1, GETDATE())",
                companyRowId, companyId, "Lifecycle B", s_companyDbId, string.Empty, "USD", string.Empty, string.Empty));

            var userLookup = dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT sys_rowid FROM st_user WHERE sys_id = {0}", "001"));
            var userRowId = (Guid)userLookup.Scalar!;

            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                "INSERT INTO st_user_company (sys_rowid, user_rowid, company_rowid, sys_insert_time) " +
                "VALUES ({0}, {1}, {2}, GETDATE())",
                grantRowId, userRowId, companyRowId));

            return (companyRowId, grantRowId);
        }

        private void DeleteGrantAndCompany(Guid grantRowId, Guid companyRowId)
        {
            var dbAccess = _fx.NewDbAccess("common");
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                "DELETE FROM st_user_company WHERE sys_rowid = {0}", grantRowId));
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                "DELETE FROM st_company WHERE sys_rowid = {0}", companyRowId));
        }

        [Fact]
        [DisplayName("EnterCompany after Logout throws UnauthorizedAccessException")]
        public void AfterLogout_EnterCompany_ThrowsUnauthorized()
        {
            var companyService = _fx.GetRequiredService<ICompanyInfoService>();
            var companyId = UniqueCompanyId();
            companyService.Set(new CompanyInfo { CompanyId = companyId, CompanyName = "Acme" });

            try
            {
                var loginBo = new TestableSystemBusinessObject(
                    TestPolhemContext.Create(_fx), Guid.Empty,
                    _ => (true, "User"));
                var accessToken = loginBo.Login(new LoginArgs { UserId = "u", Password = "p" }).AccessToken;
                var bo = new SystemBusinessObject(TestPolhemContext.Create(_fx), accessToken, SysProgIds.System);
                bo.Logout(new LogoutArgs());

                Assert.Throws<UnauthorizedAccessException>(
                    () => bo.EnterCompany(new EnterCompanyArgs { CompanyId = companyId }));
            }
            finally
            {
                companyService.Remove(companyId);
            }
        }

        [Fact]
        [DisplayName("Logout directly after Login (without entering a company) succeeds idempotently")]
        public void Login_DirectLogout_WithoutEnteringCompany_Succeeds()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            var loginBo = new TestableSystemBusinessObject(
                TestPolhemContext.Create(_fx), Guid.Empty,
                _ => (true, "User"));
            var accessToken = loginBo.Login(new LoginArgs { UserId = "u", Password = "p" }).AccessToken;
            var bo = new SystemBusinessObject(TestPolhemContext.Create(_fx), accessToken, SysProgIds.System);

            bo.Logout(new LogoutArgs());

            Assert.Null(sessionService.Get(accessToken));
        }

        [Fact]
        [DisplayName("LeaveCompany after Login (without entering a company) is idempotent and SessionInfo.CompanyId stays null")]
        public void Login_LeaveCompanyWithoutEntering_Idempotent()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            var loginBo = new TestableSystemBusinessObject(
                TestPolhemContext.Create(_fx), Guid.Empty,
                _ => (true, "User"));
            var accessToken = loginBo.Login(new LoginArgs { UserId = "u", Password = "p" }).AccessToken;
            var bo = new SystemBusinessObject(TestPolhemContext.Create(_fx), accessToken, SysProgIds.System);

            try
            {
                bo.LeaveCompany(new LeaveCompanyArgs());
                Assert.Null(sessionService.Get(accessToken)!.CompanyId);
            }
            finally
            {
                sessionService.Remove(accessToken);
            }
        }
    }
}
