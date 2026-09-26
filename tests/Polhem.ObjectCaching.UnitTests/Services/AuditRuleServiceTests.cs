using System.ComponentModel;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Logging;
using Polhem.Definition.Organization;
using Polhem.Definition.Security;
using Polhem.Definition.Storage;
using Polhem.ObjectCaching.Services;

namespace Polhem.ObjectCaching.UnitTests.Services
{
    /// <summary>
    /// Unit tests of <see cref="AuditRuleService"/>. Each test uses its own
    /// <see cref="CacheContainerService"/> with a unique prefix.
    /// </summary>
    public class AuditRuleServiceTests
    {
        private sealed class StubCacheDataSourceProvider : ICacheDataSourceProvider
        {
            private readonly Func<string, CompanyAuditRules?> _resolver;
            public int GetCompanyAuditRulesCallCount { get; private set; }
            public StubCacheDataSourceProvider(Func<string, CompanyAuditRules?> resolver) { _resolver = resolver; }

            public CompanyAuditRules? GetCompanyAuditRules(string companyId)
            {
                GetCompanyAuditRulesCallCount++;
                return _resolver(companyId);
            }

            public SessionInfo? GetSessionInfo(Guid accessToken) => null;
            public CompanyInfo? GetCompanyInfo(string companyId) => null;
            public CompanyRolePermissions? GetCompanyRolePermissions(string companyId) => null;
            public DepartmentTree? GetDepartmentTree(string companyId) => null;
            public ApiKeyInfo? GetApiKey(string sysId) => null;
            public ApiKeyGateState GetApiKeyGateState() => new();
        }

        private static CacheContainerService NewCache(ICacheDataSourceProvider dataSource)
        {
            var paths = new PathOptions { DefinePath = Path.GetTempPath() };
            var storage = new FileDefineStorage(paths);
            string prefix = "audit_rule_svc_" + Guid.NewGuid().ToString("N");
            return new CacheContainerService(storage, paths, prefix, () => dataSource);
        }

        private static CompanyAuditRules RulesFor(string companyId)
            => new(companyId, [new AuditRule("Order", AuditRuleMode.On, AuditRuleMode.Off, true)]);

        [Fact]
        [DisplayName("Get reads through to the data source on a cache miss and returns the snapshot")]
        public void Get_CacheMiss_ReadsThroughDataSource()
        {
            var stub = new StubCacheDataSourceProvider(RulesFor);
            var service = new AuditRuleService(NewCache(stub));

            var rules = service.Get("C001");

            Assert.NotNull(rules);
            Assert.Equal("C001", rules.CompanyId);
            Assert.Equal(AuditRuleMode.On, rules.Find("Order")!.ChangeMode);
            Assert.Equal(1, stub.GetCompanyAuditRulesCallCount);
        }

        [Fact]
        [DisplayName("The second Get hits the cache and does not read the data source again")]
        public void Get_SecondCall_DoesNotHitDataSource()
        {
            var stub = new StubCacheDataSourceProvider(RulesFor);
            var service = new AuditRuleService(NewCache(stub));

            service.Get("C001");
            service.Get("C001");

            Assert.Equal(1, stub.GetCompanyAuditRulesCallCount);
        }

        [Fact]
        [DisplayName("Get after Remove reads through to the data source again")]
        public void Remove_ThenGet_ReloadsFromDataSource()
        {
            var stub = new StubCacheDataSourceProvider(RulesFor);
            var service = new AuditRuleService(NewCache(stub));

            service.Get("C001");
            service.Remove("C001");
            service.Get("C001");

            Assert.Equal(2, stub.GetCompanyAuditRulesCallCount);
        }

        [Fact]
        [DisplayName("Different companies are cached separately without contaminating each other")]
        public void Get_DifferentCompanies_CachedSeparately()
        {
            var stub = new StubCacheDataSourceProvider(RulesFor);
            var service = new AuditRuleService(NewCache(stub));

            var first = service.Get("C001");
            var second = service.Get("C002");

            Assert.Equal("C001", first!.CompanyId);
            Assert.Equal("C002", second!.CompanyId);
            Assert.Equal(2, stub.GetCompanyAuditRulesCallCount);
        }

        [Fact]
        [DisplayName("Get returns null when the data source returns null (the company does not exist)")]
        public void Get_UnknownCompany_ReturnsNull()
        {
            var stub = new StubCacheDataSourceProvider(_ => null);
            var service = new AuditRuleService(NewCache(stub));

            Assert.Null(service.Get("nope"));
        }
    }
}
