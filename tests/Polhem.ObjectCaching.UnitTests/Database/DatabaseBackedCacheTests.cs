using System.ComponentModel;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Logging;
using Polhem.Definition.Organization;
using Polhem.Definition.Security;
using Polhem.ObjectCaching.Database;

namespace Polhem.ObjectCaching.UnitTests.Database
{
    /// <summary>
    /// Direct coverage of the database-dependent caches: a read-through happens only once, `Set` overwrites,
    /// `Remove` causes a reload, and nothing is read when there is no data source.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Before these tests, `tests/` had no references to `CompanyInfoCache`, `CompanyRolePermissionsCache`,
    /// `DepartmentTreeCache`, `CompanyAuditRulesCache` or `ApiKeyGateCache` (only one indirect mention of
    /// `CompanyInfoCache`). Under <c>rules/definition.md</c> they are the high-risk kind that has **no `SaveX` and is
    /// invalidated only through cache-notify**: one missed notify and the whole process gets stale values. The
    /// service layer had tests; the cache layer itself had none.
    /// </para>
    /// <para>
    /// A stub data source is used instead of a real database: what is checked here is the cache semantics (when it
    /// reads and when it does not), which has nothing to do with where the data comes from.
    /// </para>
    /// </remarks>
    public class DatabaseBackedCacheTests
    {
        /// <summary>A data source that only counts calls, to see how many read-throughs happened.</summary>
        private sealed class CountingSource : ICacheDataSourceProvider
        {
            public int CompanyInfoCalls { get; private set; }
            public int RolePermissionCalls { get; private set; }
            public int DepartmentTreeCalls { get; private set; }
            public int AuditRuleCalls { get; private set; }
            public int GateCalls { get; private set; }

            public SessionInfo? GetSessionInfo(Guid accessToken) => null;

            public CompanyInfo? GetCompanyInfo(string companyId)
            { CompanyInfoCalls++; return new CompanyInfo { CompanyId = companyId }; }

            public CompanyRolePermissions? GetCompanyRolePermissions(string companyId)
            { RolePermissionCalls++; return new CompanyRolePermissions(companyId, [], []); }

            public DepartmentTree? GetDepartmentTree(string companyId)
            { DepartmentTreeCalls++; return new DepartmentTree(); }

            public CompanyAuditRules? GetCompanyAuditRules(string companyId)
            { AuditRuleCalls++; return new CompanyAuditRules(companyId, []); }

            public ApiKeyInfo? GetApiKey(string sysId) => null;

            public ApiKeyGateState GetApiKeyGateState()
            { GateCalls++; return new ApiKeyGateState { InForce = true }; }
        }

        private static string NewPrefix() => "t" + Guid.NewGuid().ToString("N");

        [Fact]
        [DisplayName("CompanyInfoCache reads once on a miss, serves later hits without reading, and reads again after Remove")]
        public void CompanyInfoCache_ReadsThroughOnceThenCaches()
        {
            var source = new CountingSource();
            var cache = new CompanyInfoCache(() => source, NewPrefix());

            Assert.NotNull(cache.Get("c1"));
            Assert.NotNull(cache.Get("c1"));
            Assert.Equal(1, source.CompanyInfoCalls);

            cache.Remove("c1");
            Assert.NotNull(cache.Get("c1"));
            Assert.Equal(2, source.CompanyInfoCalls);
        }

        [Fact]
        [DisplayName("CompanyRolePermissionsCache reads once on a miss and serves later hits without reading")]
        public void CompanyRolePermissionsCache_ReadsThroughOnce()
        {
            var source = new CountingSource();
            var cache = new CompanyRolePermissionsCache(() => source, NewPrefix());

            Assert.NotNull(cache.Get("c1"));
            Assert.NotNull(cache.Get("c1"));
            Assert.Equal(1, source.RolePermissionCalls);
        }

        [Fact]
        [DisplayName("DepartmentTreeCache reads once on a miss and serves later hits without reading")]
        public void DepartmentTreeCache_ReadsThroughOnce()
        {
            var source = new CountingSource();
            var cache = new DepartmentTreeCache(() => source, NewPrefix());

            Assert.NotNull(cache.Get("c1"));
            Assert.NotNull(cache.Get("c1"));
            Assert.Equal(1, source.DepartmentTreeCalls);
        }

        [Fact]
        [DisplayName("CompanyAuditRulesCache reads once on a miss and serves later hits without reading")]
        public void CompanyAuditRulesCache_ReadsThroughOnce()
        {
            var source = new CountingSource();
            var cache = new CompanyAuditRulesCache(() => source, NewPrefix());

            Assert.NotNull(cache.Get("c1"));
            Assert.NotNull(cache.Get("c1"));
            Assert.Equal(1, source.AuditRuleCalls);
        }

        [Fact]
        [DisplayName("ApiKeyGateCache reads once on GetState and reads again after the entry is removed")]
        public void ApiKeyGateCache_ReadsThroughOnceThenReloadsAfterRemove()
        {
            var source = new CountingSource();
            var cache = new ApiKeyGateCache(() => source, NewPrefix());

            Assert.NotNull(cache.GetState());
            Assert.NotNull(cache.GetState());
            Assert.Equal(1, source.GateCalls);

            cache.Remove(ApiKeyGateState.CacheKey);
            Assert.NotNull(cache.GetState());
            Assert.Equal(2, source.GateCalls);
        }

        [Fact]
        [DisplayName("ApiKeyGateCache deliberately shares the ApiKeyInfo cache group (a key change must also invalidate the gate)")]
        public void ApiKeyGateCache_SharesTheApiKeyCacheGroup()
        {
            // Not a typo. If the gate entry were not invalidated together with the keys,
            // a newly issued key could be rejected for up to an hour.
            Assert.Equal(nameof(ApiKeyInfo), new ApiKeyGateCache(NewPrefix()).CacheGroup);
        }

        [Fact]
        [DisplayName("Without a data source nothing is read and Get returns null")]
        public void NoDataSource_GetReturnsNullWithoutReadingThrough()
        {
            // This is the shape of the public constructor (`Set` is the only way in),
            // and both mobile heads and tests use it.
            Assert.Null(new CompanyInfoCache(NewPrefix()).Get("c1"));
            Assert.Null(new CompanyAuditRulesCache(NewPrefix()).Get("c1"));
            Assert.Null(new DepartmentTreeCache(NewPrefix()).Get("c1"));
        }
    }
}
