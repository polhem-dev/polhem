using System.ComponentModel;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Definition.Logging;
using Polhem.Repository.AuditLog;
using Polhem.Tests.Shared;

namespace Polhem.Repository.UnitTests.AuditLog
{
    /// <summary>
    /// <see cref="AuditRuleRepository"/> against a company database that has <c>st_audit_rule</c>: the stored rows of
    /// one company read back as rules, and the cross-process announcement is skipped where no cache-notify service is
    /// configured.
    /// </summary>
    public class AuditRuleRepositoryReadTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public AuditRuleRepositoryReadTests(SharedDbFixture fx) { _fx = fx; }

        private AuditRuleRepository CreateRepo()
            => new(TestRepositoryContext.Create(_fx.GetRequiredService<IDbConnectionManager>(),
                dbAccessFactory: _fx.GetRequiredService<IDbAccessFactory>()), Guid.Empty, string.Empty);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: GetRules returns the stored rules of the requested company only, with their modes and sensitivity")]
        public void GetRules_Sqlite_ReturnsCompanyRules()
        {
            var databaseId = TestDbConventions.GetDatabaseId(DatabaseType.SQLite, "company");
            string company = "ar" + Guid.NewGuid().ToString("N")[..8];
            string other = "ao" + Guid.NewGuid().ToString("N")[..8];
            var dbAccess = _fx.NewDbAccess(databaseId);
            try
            {
                Insert(dbAccess, company, "Orders", AuditRuleMode.On, AuditRuleMode.Off, sensitive: true);
                Insert(dbAccess, company, "Products", AuditRuleMode.Off, AuditRuleMode.Inherit, sensitive: false);
                Insert(dbAccess, other, "Orders", AuditRuleMode.Off, AuditRuleMode.Off, sensitive: false);

                var rules = CreateRepo().GetRules(databaseId, company);

                Assert.Equal(2, rules.Count);
                var orders = Assert.Single(rules, r => r.ProgId == "Orders");
                Assert.Equal(AuditRuleMode.On, orders.ChangeMode);
                Assert.Equal(AuditRuleMode.Off, orders.AccessMode);
                Assert.True(orders.IsSensitive);
                var products = Assert.Single(rules, r => r.ProgId == "Products");
                Assert.Equal(AuditRuleMode.Inherit, products.AccessMode);
                Assert.False(products.IsSensitive);
            }
            finally
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    "DELETE FROM st_audit_rule WHERE sys_company_id = {0} OR sys_company_id = {1}", company, other));
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: GetRules returns an empty list for a company without rules")]
        public void GetRules_Sqlite_CompanyWithoutRules_ReturnsEmpty()
        {
            var databaseId = TestDbConventions.GetDatabaseId(DatabaseType.SQLite, "company");

            Assert.Empty(CreateRepo().GetRules(databaseId, "nr" + Guid.NewGuid().ToString("N")[..8]));
        }

        [Fact]
        [DisplayName("NotifyRulesChanged does nothing when no cache-notify service is configured")]
        public void NotifyRulesChanged_NoCacheNotify_DoesNothing()
        {
            var repo = new AuditRuleRepository(TestRepositoryContext.Create(), Guid.Empty, string.Empty);

            var ex = Record.Exception(() => repo.NotifyRulesChanged("co1"));

            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("NotifyRulesChanged refuses an empty company id")]
        public void NotifyRulesChanged_EmptyCompanyId_Throws()
        {
            var repo = new AuditRuleRepository(TestRepositoryContext.Create(), Guid.Empty, string.Empty);

            Assert.Throws<ArgumentException>(() => repo.NotifyRulesChanged(" "));
        }

        private static void Insert(DbAccess dbAccess, string companyId, string progId, AuditRuleMode change,
            AuditRuleMode access, bool sensitive)
        {
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                "INSERT INTO st_audit_rule (sys_rowid, sys_company_id, sys_id, sys_name, change_mode, access_mode, is_sensitive, sys_insert_time) " +
                "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})",
                Guid.NewGuid(), companyId, progId, progId, (int)change, (int)access, sensitive, DateTime.UtcNow));
        }
    }
}
