using System.ComponentModel;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Organization;
using Polhem.Definition.Security;
using Polhem.Definition.Storage;
using Polhem.ObjectCaching.Services;

namespace Polhem.ObjectCaching.UnitTests.Services
{
    /// <summary>
    /// Unit tests of <see cref="DepartmentTreeService"/>. Each test uses its own
    /// <see cref="CacheContainerService"/> with a unique prefix.
    /// </summary>
    public class DepartmentTreeServiceTests
    {
        private sealed class StubCacheDataSourceProvider : ICacheDataSourceProvider
        {
            private readonly Func<string, DepartmentTree?> _resolver;
            public int GetDepartmentTreeCallCount { get; private set; }
            public StubCacheDataSourceProvider(Func<string, DepartmentTree?> resolver) { _resolver = resolver; }

            public DepartmentTree? GetDepartmentTree(string companyId)
            {
                GetDepartmentTreeCallCount++;
                return _resolver(companyId);
            }

            public SessionInfo? GetSessionInfo(Guid accessToken) => null;
            public CompanyInfo? GetCompanyInfo(string companyId) => null;
            public CompanyRolePermissions? GetCompanyRolePermissions(string companyId) => null;
            public Polhem.Definition.Logging.CompanyAuditRules? GetCompanyAuditRules(string companyId) => null;
            public ApiKeyInfo? GetApiKey(string sysId) => null;
            public ApiKeyGateState GetApiKeyGateState() => new();
        }

        private static CacheContainerService NewCache(ICacheDataSourceProvider? dataSource = null)
        {
            var paths = new PathOptions { DefinePath = Path.GetTempPath() };
            var storage = new FileDefineStorage(paths);
            string prefix = "dept_svc_" + Guid.NewGuid().ToString("N");
            return dataSource == null
                ? new CacheContainerService(storage, paths, prefix)
                : new CacheContainerService(storage, paths, prefix, () => dataSource);
        }

        [Fact]
        [DisplayName("Constructor throws ArgumentNullException for a null cache")]
        public void Constructor_NullCache_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new DepartmentTreeService(null!));
        }

        [Fact]
        [DisplayName("Get on a cache hit returns the cached DepartmentTree without calling the data source")]
        public void Get_CacheHit_ReturnsCachedTree()
        {
            var dataSource = new StubCacheDataSourceProvider(
                _ => throw new InvalidOperationException("should not be called"));
            var cache = NewCache(dataSource);
            var companyId = "C001";
            var cachedTree = new DepartmentTree(companyId, []);
            cache.DepartmentTree.Set(cachedTree);

            var service = new DepartmentTreeService(cache);

            var result = service.Get(companyId);

            Assert.Same(cachedTree, result);
            Assert.Equal(0, dataSource.GetDepartmentTreeCallCount);
        }

        [Fact]
        [DisplayName("Get on a cache miss returns null when the company does not exist")]
        public void Get_CacheMiss_CompanyNotFound_ReturnsNull()
        {
            var dataSource = new StubCacheDataSourceProvider(_ => null);
            var cache = NewCache(dataSource);
            var service = new DepartmentTreeService(cache);

            var result = service.Get("MISSING_COMPANY");

            Assert.Null(result);
        }

        [Fact]
        [DisplayName("Get on a cache miss loads the tree from the data source and caches it, so the second call hits the cache")]
        public void Get_CacheMiss_CompanyFound_LoadsAndCachesTree()
        {
            var companyId = "C002";
            var deptRowId = Guid.NewGuid();
            var rows = new[] { new DepartmentRow(deptRowId, "D001", "Sales", Guid.Empty, Guid.Empty) };
            var dataSource = new StubCacheDataSourceProvider(id => new DepartmentTree(id, rows));
            var cache = NewCache(dataSource);
            var service = new DepartmentTreeService(cache);

            var result = service.Get(companyId);

            Assert.NotNull(result);
            Assert.Equal(companyId, result!.CompanyId);
            Assert.NotNull(result.Roots);
            Assert.Single(result.Roots!);
            Assert.Equal(1, dataSource.GetDepartmentTreeCallCount);

            var second = service.Get(companyId);
            Assert.Same(result, second);
            Assert.Equal(1, dataSource.GetDepartmentTreeCallCount);
        }

        [Fact]
        [DisplayName("Get returns null when no data source is provided (existing behavior)")]
        public void Get_NoDataSource_ReturnsNull()
        {
            var cache = NewCache();
            var service = new DepartmentTreeService(cache);

            Assert.Null(service.Get("ANY"));
        }

        [Fact]
        [DisplayName("Remove evicts the company's DepartmentTree from the cache")]
        public void Remove_EvictsFromCache()
        {
            var dataSource = new StubCacheDataSourceProvider(_ => null);
            var cache = NewCache(dataSource);
            var companyId = "C003";
            var tree = new DepartmentTree(companyId, []);
            cache.DepartmentTree.Set(tree);

            var service = new DepartmentTreeService(cache);

            service.Remove(companyId);

            Assert.Null(cache.DepartmentTree.Get(companyId));
        }
    }
}
