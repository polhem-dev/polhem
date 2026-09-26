using System.ComponentModel;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Organization;
using Polhem.Definition.Security;
using Polhem.ObjectCaching.Services;

namespace Polhem.ObjectCaching.UnitTests
{
    /// <summary>
    /// Behavior tests of <see cref="CompanyInfoService"/>. Each test builds its own
    /// <see cref="CacheContainerService"/> and does not share the process-wide cache.
    /// </summary>
    public class CompanyInfoServiceTests
    {
        private sealed class StubCacheDataSourceProvider : ICacheDataSourceProvider
        {
            private readonly Func<string, CompanyInfo?> _resolver;
            public int GetCompanyInfoCallCount { get; private set; }
            public StubCacheDataSourceProvider() : this(_ => null) { }
            public StubCacheDataSourceProvider(Func<string, CompanyInfo?> resolver) { _resolver = resolver; }

            public CompanyInfo? GetCompanyInfo(string companyId)
            {
                GetCompanyInfoCallCount++;
                return _resolver(companyId);
            }

            public SessionInfo? GetSessionInfo(Guid accessToken) => null;
            public CompanyRolePermissions? GetCompanyRolePermissions(string companyId) => null;
            public DepartmentTree? GetDepartmentTree(string companyId) => null;
            public ApiKeyInfo? GetApiKey(string sysId) => null;
            public ApiKeyGateState GetApiKeyGateState() => new();
        }

        private static CompanyInfoService NewService(out CacheContainerService container,
            StubCacheDataSourceProvider? dataSource = null)
        {
            var paths = new PathOptions { DefinePath = Path.GetTempPath() };
            var storage = new Polhem.Definition.Storage.FileDefineStorage(paths);
            var provider = dataSource ?? new StubCacheDataSourceProvider();
            container = new CacheContainerService(storage, paths,
                "company_svc_" + Guid.NewGuid().ToString("N"), () => provider);
            return new CompanyInfoService(container);
        }

        [Fact]
        [DisplayName("Set, Get and Remove operate on the company cache correctly")]
        public void Set_Get_Remove_Flow_Works()
        {
            var service = NewService(out _);
            var info = new CompanyInfo
            {
                CompanyId = "C001",
                CompanyName = "Acme",
                CompanyDatabaseId = "biz_shared_01"
            };

            service.Set(info);
            var loaded = service.Get("C001");

            Assert.NotNull(loaded);
            Assert.Equal("C001", loaded.CompanyId);
            Assert.Equal("Acme", loaded.CompanyName);
            Assert.Equal("biz_shared_01", loaded.CompanyDatabaseId);

            service.Remove("C001");
            Assert.Null(service.Get("C001"));
        }

        [Fact]
        [DisplayName("Get returns null when the company is not cached and the data source has no data")]
        public void Get_MissingCompanyId_DataSourceEmpty_ReturnsNull()
        {
            var service = NewService(out _);
            Assert.Null(service.Get("UNKNOWN"));
        }

        [Fact]
        [DisplayName("Get on a cache miss loads from the data source and writes the result back to the cache")]
        public void Get_CacheMiss_LoadsFromDataSource_AndPopulatesCache()
        {
            var dataSource = new StubCacheDataSourceProvider(id => id == "DB_ONLY"
                ? new CompanyInfo { CompanyId = "DB_ONLY", CompanyName = "from-db", CompanyDatabaseId = "common" }
                : null);
            var service = NewService(out _, dataSource);

            var first = service.Get("DB_ONLY");
            Assert.NotNull(first);
            Assert.Equal("from-db", first.CompanyName);
            Assert.Equal(1, dataSource.GetCompanyInfoCallCount);

            var second = service.Get("DB_ONLY");
            Assert.NotNull(second);
            Assert.Equal(1, dataSource.GetCompanyInfoCallCount);
        }

        [Fact]
        [DisplayName("Get returns null when no data source is provided (existing behavior)")]
        public void Get_NoDataSource_ReturnsNull()
        {
            var paths = new PathOptions { DefinePath = Path.GetTempPath() };
            var storage = new Polhem.Definition.Storage.FileDefineStorage(paths);
            var container = new CacheContainerService(storage, paths,
                "company_svc_no_ds_" + Guid.NewGuid().ToString("N"));
            var service = new CompanyInfoService(container);

            Assert.Null(service.Get("ANY"));
        }

        [Fact]
        [DisplayName("Constructor throws ArgumentNullException for a null cache")]
        public void Ctor_NullCache_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new CompanyInfoService(null!));
        }
    }
}
