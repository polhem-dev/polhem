using System.ComponentModel;
using Polhem.Db.Manager;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Identity;
using Polhem.Tests.Shared;

namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// How a company-bound <see cref="RepositoryBase"/> takes its company from the session.
    /// </summary>
    public class RepositoryBaseCompanyBindingTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public RepositoryBaseCompanyBindingTests(SharedDbFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("A company-bound repository takes the session's company in its lowercase form")]
        public void Construct_CompanyScope_BindsSessionCompany()
        {
            var repository = new ProbeRepository(Context(new SwitchingSessions("C001")));

            Assert.Equal("c001", repository.BoundCompanyId);
        }

        [Fact]
        [DisplayName("A company-bound repository reads the session once, so a company entered while it is built does not reach it")]
        public void Construct_CompanyChangesDuringConstruction_KeepsTheFirstRead()
        {
            var sessions = new SwitchingSessions("c001", "c002");

            var repository = new ProbeRepository(Context(sessions));

            Assert.Equal("c001", repository.BoundCompanyId);
        }

        [Fact]
        [DisplayName("A company-bound repository built with the request's snapshot takes its company and database from it, not from the session")]
        public void Construct_ContextCarriesSnapshot_UsesItInsteadOfTheSession()
        {
            var sessions = new SwitchingSessions("c002");
            var router = new RepositoryDatabaseRouter(sessions, new TwoCompanies());
            var ctx = ((RepositoryContext)TestRepositoryContext.Create(router: router, services: sessions))
                .WithCompanyScope(new SessionCompanyScope("c001", string.Empty, [], Guid.Empty, Guid.Empty, Guid.Empty));

            var repository = new ProbeRepository(ctx);

            Assert.Equal("c001", repository.BoundCompanyId);
            Assert.Equal(TwoCompanies.DatabaseOf("c001"), repository.OwnDatabaseId);
        }

        [Fact]
        [DisplayName("A repository that is not company-bound reads no company from the session")]
        public void Construct_CommonScope_BindsNoCompany()
        {
            var repository = new ProbeRepository(Context(new SwitchingSessions("c001")), DbScope.Common);

            Assert.Equal(string.Empty, repository.BoundCompanyId);
        }

        [Fact]
        [DisplayName("A company-bound repository refuses to be built when the session has entered no company")]
        public void Construct_CompanyScopeWithoutCompany_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => new ProbeRepository(Context(new SwitchingSessions(string.Empty))));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a company-bound repository binds a database access to its own database to the company, and not one to another database")]
        public void CreateDbAccess_ByDatabaseId_BindsOnlyItsOwnDatabase()
        {
            var ctx = TestRepositoryContext.Create(
                _fx.GetRequiredService<IDbConnectionManager>(),
                router: new ProviderScopedRouter(DatabaseType.SQLite),
                services: new SwitchingSessions("c001"));
            var repository = new ProbeRepository(ctx);

            Assert.Equal("c001", repository.Open(repository.OwnDatabaseId).BoundCompanyId);
            Assert.Null(repository.Open(TestDbConventions.GetDatabaseId(DatabaseType.SQLite, "common")).BoundCompanyId);
        }

        [Fact]
        [DisplayName("A company-bound repository routes to the database of the company it binds to, even when the session switches away and back while it is built")]
        public void Construct_CompanySwitchesAwayAndBack_RoutesToTheBoundCompany()
        {
            // A concurrent EnterCompany(B) and EnterCompany(A) on the same token, interleaved with the reads of one
            // repository construction. Reading the company before and after routing sees A both times, while the
            // router in between reads B, which would pair company A's id with company B's database.
            var sessions = new SwitchingSessions("c001", "c002", "c001");
            var router = new RepositoryDatabaseRouter(sessions, new TwoCompanies());

            var repository = new ProbeRepository(TestRepositoryContext.Create(router: router, services: sessions));

            Assert.Equal(TwoCompanies.DatabaseOf(repository.BoundCompanyId), repository.OwnDatabaseId);
        }

        private static IRepositoryContext Context(SwitchingSessions sessions)
            => TestRepositoryContext.Create(services: sessions);

        private sealed class ProbeRepository(IRepositoryContext ctx, DbScope scope = DbScope.Company)
            : RepositoryBase(ctx, Guid.NewGuid(), string.Empty, scope)
        {
            public string BoundCompanyId => CompanyId;
            public string OwnDatabaseId => DatabaseId;
            public Polhem.Db.DbAccess Open(string databaseId) => CreateDbAccess(databaseId);
        }

        /// <summary>
        /// Answers the first read with the first company and every later read with the last one,
        /// as a session does when EnterCompany runs between two reads.
        /// </summary>
        private sealed class TwoCompanies : ICompanyInfoService
        {
            public static string DatabaseOf(string companyId) => "db_" + companyId;

            public CompanyInfo? Get(string companyId) => new() { CompanyId = companyId, CompanyDatabaseId = DatabaseOf(companyId) };

            public void Set(CompanyInfo companyInfo) => throw new NotSupportedException();

            public void Remove(string companyId) => throw new NotSupportedException();
        }

        private sealed class SwitchingSessions(params string[] companies) : IServiceProvider, ISessionInfoService
        {
            private int _reads;

            public object? GetService(Type serviceType) => serviceType == typeof(ISessionInfoService) ? this : null;

            public SessionInfo Get(Guid accessToken)
            {
                var companyId = companies[Math.Min(_reads++, companies.Length - 1)];
                return new SessionInfo
                {
                    AccessToken = accessToken,
                    CompanyScope = new SessionCompanyScope(companyId, string.Empty, [], Guid.Empty, Guid.Empty, Guid.Empty),
                };
            }

            public void Set(SessionInfo sessionInfo) => throw new NotSupportedException();

            public void Remove(Guid accessToken) => throw new NotSupportedException();
        }
    }
}
