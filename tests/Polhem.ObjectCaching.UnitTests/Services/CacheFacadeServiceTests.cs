using System.ComponentModel;
using Polhem.Core.Serialization;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Logging;
using Polhem.Definition.Organization;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.ObjectCaching.Services;

namespace Polhem.ObjectCaching.UnitTests.Services
{
    /// <summary>
    /// The thin services over the cache container (<see cref="RolePermissionService"/>,
    /// <see cref="ApiKeyGateStateProvider"/>) and the permission-model cache's load-time validation. Each test uses its
    /// own prefixed <see cref="CacheContainerService"/> over its own define folder.
    /// </summary>
    public sealed class CacheFacadeServiceTests : IDisposable
    {
        private readonly string _root;
        private readonly PathOptions _paths;

        public CacheFacadeServiceTests()
        {
            _root = Path.Combine(Path.GetTempPath(), $"polhem-facade-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_root);
            _paths = new PathOptions { DefinePath = _root };
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch (IOException) { /* best effort */ }
        }

        private sealed class StubDataSource : ICacheDataSourceProvider
        {
            public int RolePermissionReads { get; private set; }
            public bool GateInForce { get; set; } = true;

            public CompanyRolePermissions? GetCompanyRolePermissions(string companyId)
            {
                RolePermissionReads++;
                return new CompanyRolePermissions(companyId, [], []);
            }

            public ApiKeyGateState GetApiKeyGateState() => new() { InForce = GateInForce };
            public ApiKeyInfo? GetApiKey(string sysId) => null;
            public SessionInfo? GetSessionInfo(Guid accessToken) => null;
            public CompanyInfo? GetCompanyInfo(string companyId) => null;
            public CompanyAuditRules? GetCompanyAuditRules(string companyId) => null;
            public DepartmentTree? GetDepartmentTree(string companyId) => null;
        }

        private CacheContainerService NewCache(ICacheDataSourceProvider dataSource)
            => new(new FileDefineStorage(_paths), _paths, "facade_" + Guid.NewGuid().ToString("N"), () => dataSource);

        [Fact]
        [DisplayName("RolePermissionService refuses a null cache container")]
        public void RolePermissionService_NullCache_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RolePermissionService(null!));
        }

        [Fact]
        [DisplayName("RolePermissionService reads a company's permissions once and reloads them after Remove")]
        public void RolePermissionService_GetAndRemove_ReloadsAfterRemove()
        {
            var source = new StubDataSource();
            var service = new RolePermissionService(NewCache(source));

            var first = service.Get("co1");
            var second = service.Get("co1");
            service.Remove("co1");
            service.Get("co1");

            Assert.NotNull(first);
            Assert.Same(first, second);
            Assert.Equal(2, source.RolePermissionReads);
        }

        [Fact]
        [DisplayName("ApiKeyGateStateProvider refuses a null cache container")]
        public void ApiKeyGateStateProvider_NullCache_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ApiKeyGateStateProvider(null!));
        }

        [Fact]
        [DisplayName("ApiKeyGateStateProvider returns the gate state the data source reports")]
        public void ApiKeyGateStateProvider_GetState_ReturnsSourceState()
        {
            var provider = new ApiKeyGateStateProvider(NewCache(new StubDataSource { GateInForce = false }));

            var state = provider.GetState();

            Assert.NotNull(state);
            Assert.False(state.InForce);
        }

        [Fact]
        [DisplayName("Loading a permission model registry that gives an egress action its own scope fails")]
        public void PermissionModels_InvalidRegistry_Throws()
        {
            var models = new PermissionModels();
            models.Models!.Add("M_Bad", "Bad").Rules!.Add(PermissionActions.Export, ScopeStrategy.Own);
            XmlCodec.SerializeToFile(models, _paths.GetPermissionModelsFilePath());

            var cache = NewCache(new StubDataSource());

            Assert.Throws<InvalidOperationException>(() => cache.PermissionModels.Get());
        }

        [Fact]
        [DisplayName("GetDefine with PermissionModels returns the registry from the define folder")]
        public void GetDefine_PermissionModels_ReturnsRegistry()
        {
            var models = new PermissionModels();
            models.Models!.Add("M_Good", "Good").Rules!.Add(PermissionActions.Read, ScopeStrategy.Own);
            XmlCodec.SerializeToFile(models, _paths.GetPermissionModelsFilePath());
            var access = new CacheDefineAccess(new FileDefineStorage(_paths), _paths, NewCache(new StubDataSource()), []);

            var loaded = Assert.IsType<PermissionModels>(access.GetDefine(DefineType.PermissionModels));

            Assert.True(loaded.Models!.Contains("M_Good"));
        }
    }
}
