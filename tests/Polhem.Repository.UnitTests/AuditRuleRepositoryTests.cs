using System.ComponentModel;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Repository.AuditLog;
using Polhem.Tests.Shared;

namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// How <see cref="AuditRuleRepository"/> handles "<c>st_audit_rule</c> does not exist yet": it returns an empty
    /// list instead of throwing.
    /// </summary>
    /// <remarks>
    /// This is the one real regression risk of per-form audit rules: **deployments that existed before the upgrade
    /// do not have this table**, and throwing when it cannot be read would break every Save.
    /// <para>
    /// The probe deliberately targets the <b>common</b> database: <c>st_audit_rule</c> is a company-scope table and
    /// by structure never appears in common. An earlier version targeted company instead and relied on the test
    /// definitions not registering the table. Written that way, the test **silently loses its meaning instead of
    /// failing** as soon as someone adds the table to <c>tests/Define</c>, and that did happen.
    /// </para>
    /// <para>
    /// Each provider is covered separately: checking whether a table exists goes through each provider's own
    /// <c>TableSchemaProvider</c>, so passing on one provider does not mean the others pass.
    /// </para>
    /// </remarks>
    public class AuditRuleRepositoryTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        public AuditRuleRepositoryTests(SharedDbFixture fx) { _fx = fx; }

        private AuditRuleRepository CreateRepo()
            => new AuditRuleRepository(
                TestRepositoryContext.Create(_fx.GetRequiredService<IDbConnectionManager>()),
                Guid.Empty, string.Empty);

        private void RunMissingTable(DatabaseType dbType)
        {
            var databaseId = TestDbConventions.GetDatabaseId(dbType, "common");

            var rules = CreateRepo().GetRules(databaseId);

            Assert.Empty(rules);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Returns an empty list when st_audit_rule does not exist on SQL Server")]
        public void MissingTable_SqlServer() => RunMissingTable(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("Returns an empty list when st_audit_rule does not exist on PostgreSQL")]
        public void MissingTable_PostgreSql() => RunMissingTable(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Returns an empty list when st_audit_rule does not exist on SQLite")]
        public void MissingTable_Sqlite() => RunMissingTable(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("Returns an empty list when st_audit_rule does not exist on MySQL")]
        public void MissingTable_MySql() => RunMissingTable(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Returns an empty list when st_audit_rule does not exist on Oracle")]
        public void MissingTable_Oracle() => RunMissingTable(DatabaseType.Oracle);

        [Fact]
        [DisplayName("An empty databaseId throws instead of quietly returning an empty list")]
        public void GetRules_EmptyDatabaseId_Throws()
        {
            var exception = Record.Exception(() => CreateRepo().GetRules(string.Empty));

            Assert.IsType<ArgumentException>(exception);
        }
    }
}
