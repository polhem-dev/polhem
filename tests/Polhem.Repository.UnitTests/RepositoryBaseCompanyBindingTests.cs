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
        [DisplayName("A company-bound repository refuses to be built when the session enters another company while it is routed")]
        public void Construct_CompanyChangesDuringRouting_Throws()
        {
            var sessions = new SwitchingSessions("c001", "c002");

            Assert.Throws<InvalidOperationException>(() => new ProbeRepository(Context(sessions)));
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
