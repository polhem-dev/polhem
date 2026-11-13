using System.ComponentModel;
using Polhem.Business.Providers;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Identity;
using Polhem.Definition.Logging;
using Polhem.Definition.Organization;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.Repository.Abstractions.AuditLog;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.Form;
using Polhem.Repository.Abstractions.System;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// <see cref="CacheDataSourceProvider"/> over stub repositories: how each cache's data is shaped from what the
    /// repositories return, and the "no store" answers when no <c>common</c> database is configured.
    /// </summary>
    public class CacheDataSourceProviderStubTests : IClassFixture<PolhemTestFixture>
    {
        private const string CompanyId = "co1";
        private const string CompanyDb = "co1_db";

        private readonly PolhemTestFixture _fx;

        public CacheDataSourceProviderStubTests(PolhemTestFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("The constructor refuses a null service provider")]
        public void Constructor_NullServices_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new CacheDataSourceProvider(new StubFactory(), null!));
        }

        [Fact]
        [DisplayName("GetSessionInfo returns null when the key provider cannot rebuild a session")]
        public void GetSessionInfo_KeyProviderWithoutRebuild_ReturnsNull()
        {
            var factory = new StubFactory();
            var provider = Create(factory, keyProvider: new StubKeyProvider(supportsRebuild: false));

            Assert.Null(provider.GetSessionInfo(Guid.NewGuid()));
            Assert.Equal(0, factory.Sessions.Reads);
        }

        [Fact]
        [DisplayName("GetSessionInfo returns null without reading a seed when no common database is configured")]
        public void GetSessionInfo_NoCommonDatabase_ReturnsNull()
        {
            var factory = new StubFactory();
            var provider = Create(factory, hasCommon: false);

            Assert.Null(provider.GetSessionInfo(Guid.NewGuid()));
            Assert.Equal(0, factory.Sessions.Reads);
        }

        [Fact]
        [DisplayName("GetSessionInfo returns null for a token without a stored seed")]
        public void GetSessionInfo_NoSeed_ReturnsNull()
        {
            var factory = new StubFactory();

            Assert.Null(Create(factory).GetSessionInfo(Guid.NewGuid()));
            Assert.Equal(1, factory.Sessions.Reads);
        }

        [Fact]
        [DisplayName("GetSessionInfo rebuilds a session outside any company with the user's own time zone and culture")]
        public void GetSessionInfo_SeedWithLocale_UsesUserLocale()
        {
            var token = Guid.NewGuid();
            var end = DateTime.UtcNow.AddMinutes(30);
            var factory = new StubFactory();
            factory.Sessions.Seed = new SessionUser { AccessToken = token, UserId = "u1", UserName = "User One", EndTime = end };
            factory.Users.Locale = new UserLocale("Asia/Taipei", "zh-TW");

            var session = Create(factory).GetSessionInfo(token);

            Assert.NotNull(session);
            Assert.Equal(token, session.AccessToken);
            Assert.Equal("u1", session.UserId);
            Assert.Equal("User One", session.UserName);
            Assert.Equal(end, session.ExpiredAt);
            Assert.Equal("Asia/Taipei", session.TimeZone);
            Assert.Equal("zh-TW", session.Culture);
            Assert.Equal(StubKeyProvider.Key, session.ApiEncryptionKey);
        }

        [Fact]
        [DisplayName("GetSessionInfo falls back to the system default time zone and language when the user has none")]
        public void GetSessionInfo_SeedWithoutLocale_UsesSystemDefaults()
        {
            var token = Guid.NewGuid();
            var factory = new StubFactory();
            factory.Sessions.Seed = new SessionUser { AccessToken = token, UserId = "u1", UserName = "User One" };
            factory.Users.Locale = UserLocale.Empty;
            var settings = _fx.GetRequiredService<IDefineAccess>().GetSystemSettings();

            var session = Create(factory).GetSessionInfo(token);

            Assert.NotNull(session);
            Assert.Equal(settings.BackendConfiguration.DefaultTimeZone, session.TimeZone);
            Assert.Equal(settings.CommonConfiguration.DefaultLanguage, session.Culture);
        }

        [Fact]
        [DisplayName("GetCompanyInfo returns what the company repository returns")]
        public void GetCompanyInfo_ReturnsRepositoryValue()
        {
            var factory = new StubFactory();

            Assert.Same(factory.Companies.Company, Create(factory).GetCompanyInfo(CompanyId));
            Assert.Null(Create(factory).GetCompanyInfo("other"));
        }

        [Fact]
        [DisplayName("GetCompanyRolePermissions reads grants and user roles from the company's own database")]
        public void GetCompanyRolePermissions_KnownCompany_ReadsCompanyDatabase()
        {
            var factory = new StubFactory();

            var permissions = Create(factory).GetCompanyRolePermissions(CompanyId);

            Assert.NotNull(permissions);
            Assert.Equal([CompanyDb, CompanyDb], factory.Roles.DatabaseIds);
        }

        [Fact]
        [DisplayName("GetDepartmentTree reads the departments from the company's own database")]
        public void GetDepartmentTree_KnownCompany_ReadsCompanyDatabase()
        {
            var factory = new StubFactory();

            var tree = Create(factory).GetDepartmentTree(CompanyId);

            Assert.NotNull(tree);
            Assert.Equal(CompanyDb, factory.Departments.DatabaseId);
        }

        [Fact]
        [DisplayName("GetCompanyAuditRules returns the company's rules, and null for an unknown company")]
        public void GetCompanyAuditRules_ReadsCompanyRules()
        {
            var factory = new StubFactory();
            factory.AuditRules.Rules = [new AuditRule("Orders", AuditRuleMode.On, AuditRuleMode.Off, IsSensitive: true)];
            var provider = Create(factory);

            var rules = provider.GetCompanyAuditRules(CompanyId);

            Assert.NotNull(rules);
            Assert.True(rules.Find("Orders")!.IsSensitive);
            Assert.Equal(CompanyDb, factory.AuditRules.DatabaseId);
            Assert.Null(provider.GetCompanyAuditRules("other"));
        }

        [Fact]
        [DisplayName("GetApiKey reads an enabled key when a common database is configured")]
        public void GetApiKey_WithCommonDatabase_ReadsKey()
        {
            var factory = new StubFactory();
            factory.ApiKeys.Key = new ApiKeyInfo { SysId = "k1" };

            Assert.Same(factory.ApiKeys.Key, Create(factory).GetApiKey("k1"));
        }

        [Fact]
        [DisplayName("GetApiKey answers no key when no common database is configured")]
        public void GetApiKey_NoCommonDatabase_ReturnsNull()
        {
            var factory = new StubFactory();
            factory.ApiKeys.Key = new ApiKeyInfo { SysId = "k1" };

            Assert.Null(Create(factory, hasCommon: false).GetApiKey("k1"));
        }

        [Fact]
        [DisplayName("GetApiKeyGateState reads the store's state when a common database is configured")]
        public void GetApiKeyGateState_WithCommonDatabase_ReadsState()
        {
            var factory = new StubFactory();

            Assert.True(Create(factory).GetApiKeyGateState().InForce);
        }

        [Fact]
        [DisplayName("GetApiKeyGateState reports the gate not in force when no common database is configured")]
        public void GetApiKeyGateState_NoCommonDatabase_NotInForce()
        {
            var factory = new StubFactory();

            Assert.False(Create(factory, hasCommon: false).GetApiKeyGateState().InForce);
        }

        // -------- Fixtures --------

        private CacheDataSourceProvider Create(StubFactory factory, bool hasCommon = true, IApiEncryptionKeyProvider? keyProvider = null)
        {
            var settings = new DatabaseSettings();
            if (hasCommon) { settings.Items!.Add(new DatabaseItem { Id = DbCategoryIds.Common }); }
            var services = new TestOverrideServiceProvider(_fx.Provider,
                (typeof(IApiEncryptionKeyProvider), keyProvider ?? new StubKeyProvider(supportsRebuild: true)),
                (typeof(IDatabaseSettingsProvider), new StubDatabaseSettingsProvider(settings)));
            return new CacheDataSourceProvider(factory, services);
        }

        private sealed class StubKeyProvider : IApiEncryptionKeyProvider
        {
            public static readonly byte[] Key = new byte[64];
            public StubKeyProvider(bool supportsRebuild) { SupportsSessionRebuild = supportsRebuild; }
            public bool SupportsSessionRebuild { get; }
            public byte[] GetKey(Guid accessToken) => Key;
            public byte[] GenerateKeyForLogin(Guid accessToken) => Key;
        }

        private sealed class StubDatabaseSettingsProvider : IDatabaseSettingsProvider
        {
            private readonly DatabaseSettings _settings;
            public StubDatabaseSettingsProvider(DatabaseSettings settings) { _settings = settings; }
            public DatabaseSettings Get() => _settings;
            public DatabaseItem GetItem(string databaseId) => throw new NotSupportedException();
            public void ValidateRequired() { }
        }

        private sealed class StubFactory : IRepositoryFactory
        {
            public StubSessions Sessions { get; } = new();
            public StubUsers Users { get; } = new();
            public StubCompanies Companies { get; } = new();
            public StubRoles Roles { get; } = new();
            public StubDepartments Departments { get; } = new();
            public StubAuditRules AuditRules { get; } = new();
            public StubApiKeys ApiKeys { get; } = new();

            public T Create<T>(Guid accessToken = default) where T : class
            {
                object repo = typeof(T) switch
                {
                    var t when t == typeof(ISessionRepository) => Sessions,
                    var t when t == typeof(IUserRepository) => Users,
                    var t when t == typeof(ICompanyRepository) => Companies,
                    var t when t == typeof(IRolePermissionRepository) => Roles,
                    var t when t == typeof(IDepartmentRepository) => Departments,
                    var t when t == typeof(IAuditRuleRepository) => AuditRules,
                    var t when t == typeof(IApiKeyRepository) => ApiKeys,
                    _ => throw new NotSupportedException(typeof(T).Name),
                };
                return (T)repo;
            }

            public T CreateFormRepository<T>(Guid accessToken, string progId) where T : class, IDataFormRepository
                => throw new NotSupportedException();
            public T CreateFormRepository<T>(Guid accessToken, string progId, SessionCompanyScope companyScope) where T : class, IDataFormRepository
                => throw new NotSupportedException();
        }

        private sealed class StubSessions : ISessionRepository
        {
            public SessionUser? Seed { get; set; }
            public int Reads { get; private set; }
            public SessionUser? GetSession(Guid accessToken)
            {
                Reads++;
                return Seed?.AccessToken == accessToken ? Seed : null;
            }
            public void InsertSession(SessionUser sessionUser) => throw new NotSupportedException();
            public void UpdateSession(SessionUser sessionUser) => throw new NotSupportedException();
            public void DeleteSession(Guid accessToken) => throw new NotSupportedException();
            public int DeleteExpiredSessions() => throw new NotSupportedException();
        }

        private sealed class StubUsers : IUserRepository
        {
            public UserLocale Locale { get; set; } = UserLocale.Empty;
            public UserLocale GetLocale(string userId) => Locale;
            public Guid GetRowIdBySysId(string userId) => throw new NotSupportedException();
            public bool VerifyPassword(string userId, string password) => throw new NotSupportedException();
            public string? GetName(string userId) => throw new NotSupportedException();
            public bool IsDeploymentAdmin(string userId) => throw new NotSupportedException();
            public bool SetDeploymentAdmin(string userId, bool isDeploymentAdmin) => throw new NotSupportedException();
        }

        private sealed class StubCompanies : ICompanyRepository
        {
            public CompanyInfo Company { get; } = new() { CompanyId = CompanyId, CompanyName = "Co", CompanyDatabaseId = CompanyDb };
            public CompanyInfo? GetById(string companyId) => companyId == CompanyId ? Company : null;
            public IReadOnlyList<CompanyDatabase> GetCompanyDatabases(string databaseId) => throw new NotSupportedException();
        }

        private sealed class StubRoles : IRolePermissionRepository
        {
            public List<string> DatabaseIds { get; } = [];
            public IReadOnlyList<RoleGrantRow> GetRoleGrants(string databaseId, string companyId)
            {
                DatabaseIds.Add(databaseId);
                return [];
            }
            public IReadOnlyList<UserRoleRow> GetUserRoles(string databaseId, string companyId)
            {
                DatabaseIds.Add(databaseId);
                return [];
            }
        }

        private sealed class StubDepartments : IDepartmentRepository
        {
            public string? DatabaseId { get; private set; }
            public IReadOnlyList<DepartmentRow> GetDepartments(string databaseId, string companyId)
            {
                DatabaseId = databaseId;
                return [];
            }
        }

        private sealed class StubAuditRules : IAuditRuleRepository
        {
            public IReadOnlyList<AuditRule> Rules { get; set; } = [];
            public string? DatabaseId { get; private set; }
            public IReadOnlyList<AuditRule> GetRules(string databaseId, string companyId)
            {
                DatabaseId = databaseId;
                return Rules;
            }
            public void NotifyRulesChanged(string companyId) => throw new NotSupportedException();
        }

        private sealed class StubApiKeys : IApiKeyRepository
        {
            public ApiKeyInfo? Key { get; set; }
            public ApiKeyInfo? GetEnabledById(string sysId) => Key?.SysId == sysId ? Key : null;
            public ApiKeyGateState GetGateState() => new() { InForce = true };
            public bool Exists(string sysId) => throw new NotSupportedException();
            public void Insert(ApiKeyInfo apiKey) => throw new NotSupportedException();
            public IReadOnlyList<ApiKeySummary> GetList() => throw new NotSupportedException();
            public bool SetEnabled(string sysId, bool enabled) => throw new NotSupportedException();
            public bool SetExpiry(string sysId, DateTime? expiredAt) => throw new NotSupportedException();
        }
    }
}
