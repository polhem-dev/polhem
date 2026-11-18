using System.ComponentModel;
using System.Data;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Repository.Form;
using Polhem.Tests.Shared;

namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// Round trips of <see cref="UserQueryRepository"/> against each provider's company database: what a user sees,
    /// the default, overwriting, deleting, and that another company's rows stay apart.
    /// </summary>
    public class UserQueryRepositoryTests : IClassFixture<SharedDbFixture>
    {
        private const string OtherCompanyId = "c002";
        private const string Me = "uq-alice";
        private const string Other = "uq-bob";

        private readonly SharedDbFixture _fx;

        public UserQueryRepositoryTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: user queries round-trip through insert, read, overwrite, default and delete")]
        public void RoundTrip_Sqlite() => RunRoundTrip(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: user queries round-trip through insert, read, overwrite, default and delete")]
        public void RoundTrip_SqlServer() => RunRoundTrip(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: user queries round-trip through insert, read, overwrite, default and delete")]
        public void RoundTrip_PostgreSql() => RunRoundTrip(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: user queries round-trip through insert, read, overwrite, default and delete")]
        public void RoundTrip_MySql() => RunRoundTrip(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: user queries round-trip through insert, read, overwrite, default and delete")]
        public void RoundTrip_Oracle() => RunRoundTrip(DatabaseType.Oracle);

        private void RunRoundTrip(DatabaseType dbType)
        {
            string progId = "Uq" + Guid.NewGuid().ToString("N")[..10];
            var mine = CreateRepo(dbType, SharedDatabaseState.SeedCompanyId);
            var otherCompany = CreateRepo(dbType, OtherCompanyId);
            var minePersonal = Guid.NewGuid();
            var mineShared = Guid.NewGuid();
            var othersShared = Guid.NewGuid();
            var othersPersonal = Guid.NewGuid();
            var otherCompanyShared = Guid.NewGuid();
            string criteria = "<UserQuery version=\"1\"><Condition field=\"status\" operator=\"In\"><Value text=\"草稿\">0</Value></Condition></UserQuery>";

            try
            {
                mine.Insert(minePersonal, progId, Me, "Zeta", false, criteria);
                mine.Insert(mineShared, progId, Me, "Alpha", true, string.Empty);
                mine.Insert(othersShared, progId, Other, "Beta", true, string.Empty);
                mine.Insert(othersPersonal, progId, Other, "Gamma", false, string.Empty);
                otherCompany.Insert(otherCompanyShared, progId, Other, "Elsewhere", true, string.Empty);

                var list = mine.GetQueries(progId, Me);
                var rows = list.Table.Rows.Cast<DataRow>().ToList();
                Assert.Equal([mineShared, minePersonal, othersShared], rows.Select(r => (Guid)r["sys_rowid"]));
                Assert.Equal([true, true, false], rows.Select(r => (bool)r["is_own"]));
                Assert.Equal(criteria, rows[1]["criteria"]);
                Assert.Equal(Guid.Empty, list.DefaultRowId);

                Assert.Equal(new Polhem.Repository.Abstractions.Form.UserQueryOwner(progId, Other, true), mine.GetOwner(othersShared));
                Assert.Null(otherCompany.GetOwner(minePersonal));

                Assert.Equal(1, mine.Update(minePersonal, "Renamed", true, "<UserQuery version=\"1\" />"));
                Assert.Equal(0, otherCompany.Update(minePersonal, "Hijacked", true, string.Empty));
                var renamed = mine.GetQueries(progId, Other).Table.Rows.Cast<DataRow>().Single(r => (Guid)r["sys_rowid"] == minePersonal);
                Assert.Equal(("Renamed", true, "<UserQuery version=\"1\" />"), ((string)renamed["sys_name"], (bool)renamed["is_shared"], (string)renamed["criteria"]));

                mine.SetDefault(progId, Me, othersShared);
                mine.SetDefault(progId, Other, othersShared);
                mine.SetDefault(progId, Me, mineShared);
                Assert.Equal(mineShared, mine.GetQueries(progId, Me).DefaultRowId);

                Assert.Equal(0, otherCompany.Delete(othersShared));
                Assert.Equal(1, mine.Delete(othersShared));
                Assert.Equal(Guid.Empty, mine.GetQueries(progId, Other).DefaultRowId);
                Assert.Equal(mineShared, mine.GetQueries(progId, Me).DefaultRowId);

                mine.SetDefault(progId, Me, Guid.Empty);
                Assert.Equal(Guid.Empty, mine.GetQueries(progId, Me).DefaultRowId);
            }
            finally
            {
                Cleanup(dbType, progId);
            }
        }

        private UserQueryRepository CreateRepo(DatabaseType dbType, string companyId)
            => new(TestRepositoryContext.Create(_fx.GetRequiredService<IDbConnectionManager>(),
                    router: new ProviderScopedRouter(dbType),
                    services: TestRepositoryContext.CreateServices(companyId)),
                Guid.NewGuid(), string.Empty);

        private void Cleanup(DatabaseType dbType, string progId)
        {
            var dbAccess = _fx.NewDbAccess(TestDbConventions.GetDatabaseId(dbType, DbCategoryIds.Company));
            foreach (var table in new[] { "st_user_query_default", "st_user_query" })
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"DELETE FROM {dbType.QuoteIdentifier(table)} WHERE {dbType.QuoteIdentifier("prog_id")} = {{0}}", progId));
            }
        }
    }
}
