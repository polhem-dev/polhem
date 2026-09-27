using System.ComponentModel;
using Polhem.Base.Exceptions;
using Polhem.Business.System;
using Polhem.Db;
using Polhem.Definition.Database;
using Polhem.Definition.Identity;
using Polhem.Tests.Shared;

using Polhem.Definition;
namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Behavior tests for <see cref="SystemBusinessObject.EnterCompany"/>. They use seed user '001' and
    /// seed company 'C001' for the real DB mapping path; other scenarios create companies and mappings with SQL helpers
    /// and clean them up in finally.
    /// </summary>
    public class SystemBusinessObjectEnterCompanyTests : IClassFixture<SharedDbFixture>
    {
        private const string SeedUserId = "001";
        private const string SeedCompanyId = "C001";
        // BO tests are bound to SQL Server. The company permission tables (`st_role_grant` / `st_user_role`) live in the
        // company-category DB, so `company_database_id` must point there for `EnterCompany` to load the role snapshot.
        private static readonly string s_companyDbId = TestDbConventions.GetDatabaseId(DatabaseType.SQLServer, "company");
        private readonly SharedDbFixture _fx;

        public SystemBusinessObjectEnterCompanyTests(SharedDbFixture fx) { _fx = fx; }

        #region Helpers (SQL Server only — BO tests bind to `common` databaseId which points to SQL Server)

        private DbAccess Common() => _fx.NewDbAccess("common");

        private Guid InsertCompany(string companyId, bool enabled)
        {
            var rowId = Guid.NewGuid();
            string enabledLiteral = enabled ? "1" : "0";
            var insert = new DbCommandSpec(DbCommandKind.NonQuery,
                "INSERT INTO st_company (sys_rowid, sys_id, sys_name, company_database_id, number_formats_xml, default_currency, cash_rounding_xml, allowed_currencies_xml, enabled, sys_insert_time) " +
                $"VALUES ({{0}}, {{1}}, {{2}}, {{3}}, {{4}}, {{5}}, {{6}}, {{7}}, {enabledLiteral}, GETDATE())",
                rowId, companyId, "BO 測試公司", s_companyDbId, string.Empty, "USD", string.Empty, string.Empty);
            Common().Execute(insert);
            return rowId;
        }

        private Guid InsertCompanyWithCustomize(string companyId, string customizeId)
        {
            var rowId = Guid.NewGuid();
            var insert = new DbCommandSpec(DbCommandKind.NonQuery,
                "INSERT INTO st_company (sys_rowid, sys_id, sys_name, company_database_id, customize_id, number_formats_xml, default_currency, cash_rounding_xml, allowed_currencies_xml, enabled, sys_insert_time) " +
                "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, 1, GETDATE())",
                rowId, companyId, "BO 客製測試公司", s_companyDbId, customizeId, string.Empty, "USD", string.Empty, string.Empty);
            Common().Execute(insert);
            return rowId;
        }

        private Guid InsertGrant(Guid userRowId, Guid companyRowId)
        {
            var rowId = Guid.NewGuid();
            var insert = new DbCommandSpec(DbCommandKind.NonQuery,
                "INSERT INTO st_user_company (sys_rowid, user_rowid, company_rowid, sys_insert_time) " +
                "VALUES ({0}, {1}, {2}, GETDATE())",
                rowId, userRowId, companyRowId);
            Common().Execute(insert);
            return rowId;
        }

        private void DeleteCompany(Guid companyRowId)
        {
            var delete = new DbCommandSpec(DbCommandKind.NonQuery,
                "DELETE FROM st_company WHERE sys_rowid = {0}", companyRowId);
            Common().Execute(delete);
        }

        private void DeleteGrant(Guid grantRowId)
        {
            var delete = new DbCommandSpec(DbCommandKind.NonQuery,
                "DELETE FROM st_user_company WHERE sys_rowid = {0}", grantRowId);
            Common().Execute(delete);
        }

        private Guid LookupUserRowId(string userId)
        {
            var spec = new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT sys_rowid FROM st_user WHERE sys_id = {0}", userId);
            var result = Common().Execute(spec);
            var value = result.Scalar;
            if (value is Guid g) return g;
            if (value is byte[] b && b.Length == 16) return new Guid(b);
            if (value is string s && Guid.TryParse(s, out var parsed)) return parsed;
            throw new InvalidOperationException($"Cannot resolve user rowid for '{userId}'.");
        }

        // st_employee lives in the company database (s_companyDbId), not common.
        private DbAccess CompanyDb() => _fx.NewDbAccess(s_companyDbId);

        private void InsertEmployee(Guid empRowId, string empId, Guid deptRowId, Guid userRowId)
        {
            var insert = new DbCommandSpec(DbCommandKind.NonQuery,
                "INSERT INTO st_employee (sys_rowid, sys_id, sys_name, dept_rowid, user_rowid) " +
                "VALUES ({0}, {1}, {2}, {3}, {4})",
                empRowId, empId, "BO 測試員工", deptRowId, userRowId);
            CompanyDb().Execute(insert);
        }

        private void DeleteEmployee(Guid empRowId)
        {
            var delete = new DbCommandSpec(DbCommandKind.NonQuery,
                "DELETE FROM st_employee WHERE sys_rowid = {0}", empRowId);
            CompanyDb().Execute(delete);
        }

        #endregion

        [Fact]
        [DisplayName("EnterCompany returns the CompanyInfo and sets SessionInfo.CompanyId when the seed mapping exists")]
        public void EnterCompany_ValidCompany_BindsAndReturns()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            var accessToken = TestSessionFactory.CreateAccessToken(_fx, userId: SeedUserId);
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), accessToken, SysProgIds.System);

            try
            {
                var result = bo.EnterCompany(new EnterCompanyArgs { CompanyId = SeedCompanyId });

                Assert.NotNull(result);
                Assert.Equal(SeedCompanyId, result.Company.CompanyId);

                var session = sessionService.Get(accessToken);
                Assert.NotNull(session);
                Assert.Equal(SeedCompanyId, session.CompanyId);
                // Seed company 'C001' ships no customization → standard (empty) code.
                Assert.Equal(string.Empty, session.CustomizeId);
            }
            finally
            {
                sessionService.Remove(accessToken);
            }
        }

        [Fact]
        [DisplayName("EnterCompany writes SessionInfo.CustomizeId when the company has a customize_id, and LeaveCompany clears it")]
        public void EnterCompany_CustomizedCompany_SetsThenClearsSessionCustomizeId()
        {
            const string customizeId = "ACME";
            var companyId = "CUST_" + Guid.NewGuid().ToString("N")[..6];
            var companyRowId = InsertCompanyWithCustomize(companyId, customizeId);
            var userRowId = LookupUserRowId(SeedUserId);
            var grantRowId = InsertGrant(userRowId, companyRowId);
            try
            {
                var sessionService = _fx.GetRequiredService<ISessionInfoService>();
                var accessToken = TestSessionFactory.CreateAccessToken(_fx, userId: SeedUserId);
                var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), accessToken, SysProgIds.System);

                try
                {
                    var result = bo.EnterCompany(new EnterCompanyArgs { CompanyId = companyId });
                    Assert.Equal(customizeId, result.Company.CustomizeId);
                    Assert.Equal(customizeId, sessionService.Get(accessToken)!.CustomizeId);

                    bo.LeaveCompany(new LeaveCompanyArgs());
                    Assert.Equal(string.Empty, sessionService.Get(accessToken)!.CustomizeId);
                }
                finally
                {
                    sessionService.Remove(accessToken);
                }
            }
            finally
            {
                DeleteGrant(grantRowId);
                DeleteCompany(companyRowId);
            }
        }

        [Fact]
        [DisplayName("EnterCompany resolves and snapshots the user/employee/dept rowids, and LeaveCompany clears them")]
        public void EnterCompany_SnapshotsEmployeeContext_ThenClears()
        {
            var userRowId = LookupUserRowId(SeedUserId);
            var empRowId = Guid.NewGuid();
            var deptRowId = Guid.NewGuid();
            var empId = "EMP_" + Guid.NewGuid().ToString("N")[..6];
            InsertEmployee(empRowId, empId, deptRowId, userRowId);
            try
            {
                var sessionService = _fx.GetRequiredService<ISessionInfoService>();
                var accessToken = TestSessionFactory.CreateAccessToken(_fx, userId: SeedUserId);
                var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), accessToken, SysProgIds.System);

                try
                {
                    bo.EnterCompany(new EnterCompanyArgs { CompanyId = SeedCompanyId });

                    var session = sessionService.Get(accessToken)!;
                    Assert.Equal(userRowId, session.UserRowId);
                    Assert.Equal(empRowId, session.EmployeeRowId);
                    Assert.Equal(deptRowId, session.DeptRowId);

                    bo.LeaveCompany(new LeaveCompanyArgs());
                    var cleared = sessionService.Get(accessToken)!;
                    Assert.Equal(Guid.Empty, cleared.UserRowId);
                    Assert.Equal(Guid.Empty, cleared.EmployeeRowId);
                    Assert.Equal(Guid.Empty, cleared.DeptRowId);
                }
                finally
                {
                    sessionService.Remove(accessToken);
                }
            }
            finally
            {
                DeleteEmployee(empRowId);
            }
        }

        [Fact]
        [DisplayName("EnterCompany without a matching employee still snapshots the user rowid and leaves employee/dept empty")]
        public void EnterCompany_NoEmployee_SnapshotsUserRowIdOnly()
        {
            var userRowId = LookupUserRowId(SeedUserId);
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            var accessToken = TestSessionFactory.CreateAccessToken(_fx, userId: SeedUserId);
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), accessToken, SysProgIds.System);

            try
            {
                bo.EnterCompany(new EnterCompanyArgs { CompanyId = SeedCompanyId });

                // Seed user '001' has no st_employee row → user rowid resolves, employee/dept empty.
                var session = sessionService.Get(accessToken)!;
                Assert.Equal(userRowId, session.UserRowId);
                Assert.Equal(Guid.Empty, session.EmployeeRowId);
                Assert.Equal(Guid.Empty, session.DeptRowId);
            }
            finally
            {
                sessionService.Remove(accessToken);
            }
        }

        [Fact]
        [DisplayName("EnterCompany with a CompanyId that does not exist throws Company access denied")]
        public void EnterCompany_UnknownCompany_ThrowsAccessDenied()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            var accessToken = TestSessionFactory.CreateAccessToken(_fx, userId: SeedUserId);
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), accessToken, SysProgIds.System);
            var unknown = "UNK_" + Guid.NewGuid().ToString("N")[..6];

            try
            {
                var ex = Assert.Throws<CompanyAccessDeniedException>(
                    () => bo.EnterCompany(new EnterCompanyArgs { CompanyId = unknown }));
                Assert.Contains("Company access denied", ex.Message);

                var session = sessionService.Get(accessToken);
                Assert.NotNull(session);
                Assert.Null(session.CompanyId);
            }
            finally
            {
                sessionService.Remove(accessToken);
            }
        }

        [Fact]
        [DisplayName("EnterCompany throws Company access denied when the company exists but the user has no grant")]
        public void EnterCompany_NoAccess_ThrowsAccessDenied()
        {
            var companyId = "NOGRANT_" + Guid.NewGuid().ToString("N")[..6];
            var companyRowId = InsertCompany(companyId, enabled: true);
            try
            {
                var sessionService = _fx.GetRequiredService<ISessionInfoService>();
                var accessToken = TestSessionFactory.CreateAccessToken(_fx, userId: SeedUserId);
                var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), accessToken, SysProgIds.System);

                try
                {
                    var ex = Assert.Throws<CompanyAccessDeniedException>(
                        () => bo.EnterCompany(new EnterCompanyArgs { CompanyId = companyId }));
                    Assert.Contains("Company access denied", ex.Message);
                }
                finally
                {
                    sessionService.Remove(accessToken);
                }
            }
            finally
            {
                DeleteCompany(companyRowId);
            }
        }

        [Fact]
        [DisplayName("EnterCompany throws Company access denied when the company is disabled even though the user has a grant")]
        public void EnterCompany_DisabledCompany_ThrowsAccessDenied()
        {
            var companyId = "DIS_" + Guid.NewGuid().ToString("N")[..6];
            var companyRowId = InsertCompany(companyId, enabled: false);
            var userRowId = LookupUserRowId(SeedUserId);
            var grantRowId = InsertGrant(userRowId, companyRowId);
            try
            {
                var sessionService = _fx.GetRequiredService<ISessionInfoService>();
                var accessToken = TestSessionFactory.CreateAccessToken(_fx, userId: SeedUserId);
                var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), accessToken, SysProgIds.System);

                try
                {
                    var ex = Assert.Throws<CompanyAccessDeniedException>(
                        () => bo.EnterCompany(new EnterCompanyArgs { CompanyId = companyId }));
                    Assert.Contains("Company access denied", ex.Message);
                }
                finally
                {
                    sessionService.Remove(accessToken);
                }
            }
            finally
            {
                DeleteGrant(grantRowId);
                DeleteCompany(companyRowId);
            }
        }

        [Fact]
        [DisplayName("EnterCompany switching to another granted company overwrites SessionInfo.CompanyId")]
        public void EnterCompany_SwitchToAnotherCompany_Overwrites()
        {
            var companyB = "ALT_" + Guid.NewGuid().ToString("N")[..6];
            var companyBRowId = InsertCompany(companyB, enabled: true);
            var userRowId = LookupUserRowId(SeedUserId);
            var grantRowId = InsertGrant(userRowId, companyBRowId);
            try
            {
                var sessionService = _fx.GetRequiredService<ISessionInfoService>();
                var accessToken = TestSessionFactory.CreateAccessToken(_fx, userId: SeedUserId);
                var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), accessToken, SysProgIds.System);

                try
                {
                    bo.EnterCompany(new EnterCompanyArgs { CompanyId = SeedCompanyId });
                    Assert.Equal(SeedCompanyId, sessionService.Get(accessToken)!.CompanyId);

                    bo.EnterCompany(new EnterCompanyArgs { CompanyId = companyB });
                    Assert.Equal(companyB, sessionService.Get(accessToken)!.CompanyId);
                }
                finally
                {
                    sessionService.Remove(accessToken);
                }
            }
            finally
            {
                DeleteGrant(grantRowId);
                DeleteCompany(companyBRowId);
            }
        }

        [Fact]
        [DisplayName("EnterCompany called repeatedly with the same CompanyId is idempotent")]
        public void EnterCompany_SameCompany_Idempotent()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            var accessToken = TestSessionFactory.CreateAccessToken(_fx, userId: SeedUserId);
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), accessToken, SysProgIds.System);

            try
            {
                bo.EnterCompany(new EnterCompanyArgs { CompanyId = SeedCompanyId });
                bo.EnterCompany(new EnterCompanyArgs { CompanyId = SeedCompanyId });
                Assert.Equal(SeedCompanyId, sessionService.Get(accessToken)!.CompanyId);
            }
            finally
            {
                sessionService.Remove(accessToken);
            }
        }

        [Fact]
        [DisplayName("EnterCompany throws UserMessageException for an empty CompanyId")]
        public void EnterCompany_EmptyCompanyId_ThrowsUserMessageException()
        {
            var accessToken = TestSessionFactory.CreateAccessToken(_fx, userId: SeedUserId);
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), accessToken, SysProgIds.System);
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();

            try
            {
                Assert.Throws<UserMessageException>(
                    () => bo.EnterCompany(new EnterCompanyArgs { CompanyId = string.Empty }));
            }
            finally
            {
                sessionService.Remove(accessToken);
            }
        }

        [Fact]
        [DisplayName("EnterCompany throws ArgumentNullException for null args")]
        public void EnterCompany_NullArgs_ThrowsArgumentNullException()
        {
            var accessToken = TestSessionFactory.CreateAccessToken(_fx, userId: SeedUserId);
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), accessToken, SysProgIds.System);
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();

            try
            {
                Assert.Throws<ArgumentNullException>(() => bo.EnterCompany(null!));
            }
            finally
            {
                sessionService.Remove(accessToken);
            }
        }
    }
}
