using System.ComponentModel;
using Polhem.Core.Security;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Organization;
using Polhem.Definition.Security;
using Polhem.Definition.Storage;
using Polhem.ObjectCaching.Services;

namespace Polhem.ObjectCaching.UnitTests.Services
{
    /// <summary>
    /// Unit tests of <see cref="ApiKeyValidator"/>. Each test uses its own
    /// <see cref="CacheContainerService"/> with a unique prefix.
    /// </summary>
    public class ApiKeyValidatorTests
    {
        private const string SysId = "northwind-desktop";
        private const string SysName = "Northwind Desktop";

        private sealed class StubCacheDataSourceProvider : ICacheDataSourceProvider
        {
            private readonly Func<string, ApiKeyInfo?>? _keyResolver;
            private readonly Func<ApiKeyGateState>? _gateResolver;

            public StubCacheDataSourceProvider(
                Func<string, ApiKeyInfo?>? keyResolver = null,
                Func<ApiKeyGateState>? gateResolver = null)
            {
                _keyResolver = keyResolver;
                _gateResolver = gateResolver;
            }

            public ApiKeyInfo? GetApiKey(string sysId) => _keyResolver?.Invoke(sysId);

            public ApiKeyGateState GetApiKeyGateState()
                => _gateResolver?.Invoke() ?? new ApiKeyGateState { InForce = true };

            public SessionInfo? GetSessionInfo(Guid accessToken) => null;
            public CompanyInfo? GetCompanyInfo(string companyId) => null;
            public CompanyRolePermissions? GetCompanyRolePermissions(string companyId) => null;
            public Polhem.Definition.Logging.CompanyAuditRules? GetCompanyAuditRules(string companyId) => null;
            public DepartmentTree? GetDepartmentTree(string companyId) => null;
        }

        private static CacheContainerService NewCache(ICacheDataSourceProvider? dataSource = null)
        {
            var paths = new PathOptions { DefinePath = Path.GetTempPath() };
            var storage = new FileDefineStorage(paths);
            string prefix = "apikey_val_" + Guid.NewGuid().ToString("N");
            return dataSource == null
                ? new CacheContainerService(storage, paths, prefix)
                : new CacheContainerService(storage, paths, prefix, () => dataSource);
        }

        /// <summary>
        /// Creates a validator whose gate is in force and whose given sys_id matches the secret.
        /// </summary>
        private static ApiKeyValidator NewValidatorWithKey(string secret,
            DateTime? expiredAt = null, string sysId = SysId)
        {
            var info = new ApiKeyInfo
            {
                SysId = sysId,
                SysName = SysName,
                HashedKey = ApiKeyHasher.HashSecret(secret),
                ExpiredAt = expiredAt,
            };
            var dataSource = new StubCacheDataSourceProvider(
                keyResolver: id => id == sysId ? info : null);
            return new ApiKeyValidator(NewCache(dataSource));
        }

        [Fact]
        [DisplayName("Constructor throws ArgumentNullException for a null cache")]
        public void Constructor_NullCache_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ApiKeyValidator(null!));
        }

        [Fact]
        [DisplayName("Validate returns Valid with the caller identity when the key matches")]
        public void Validate_MatchingKey_ReturnsValidWithCallerIdentity()
        {
            string secret = ApiKeyFormat.CreateSecret();
            var validator = NewValidatorWithKey(secret);

            var result = validator.Validate(ApiKeyFormat.Compose(SysId, secret));

            Assert.Equal(ApiKeyStatus.Valid, result.Status);
            Assert.True(result.IsAccepted);
            Assert.Equal(SysId, result.SysId);
            Assert.Equal(SysName, result.SysName);
        }

        [Fact]
        [DisplayName("Validate returns Invalid when the secret does not match")]
        public void Validate_WrongSecret_ReturnsInvalid()
        {
            var validator = NewValidatorWithKey(ApiKeyFormat.CreateSecret());

            var result = validator.Validate(ApiKeyFormat.Compose(SysId, ApiKeyFormat.CreateSecret()));

            Assert.Equal(ApiKeyStatus.Invalid, result.Status);
            Assert.False(result.IsAccepted);
        }

        [Fact]
        [DisplayName("Validate returns Invalid when the sys_id is not found (the repository also excludes disabled keys)")]
        public void Validate_UnknownSysId_ReturnsInvalid()
        {
            string secret = ApiKeyFormat.CreateSecret();
            var validator = NewValidatorWithKey(secret);

            var result = validator.Validate(ApiKeyFormat.Compose("other-app", secret));

            Assert.Equal(ApiKeyStatus.Invalid, result.Status);
        }

        [Fact]
        [DisplayName("Validate returns Invalid for an expired key (decided at call time, not by cache expiry)")]
        public void Validate_ExpiredKey_ReturnsInvalid()
        {
            string secret = ApiKeyFormat.CreateSecret();
            var validator = NewValidatorWithKey(secret, expiredAt: DateTime.UtcNow.AddMinutes(-1));

            var result = validator.Validate(ApiKeyFormat.Compose(SysId, secret));

            Assert.Equal(ApiKeyStatus.Invalid, result.Status);
        }

        [Fact]
        [DisplayName("Validate returns Valid when the key has not expired yet")]
        public void Validate_NotYetExpiredKey_ReturnsValid()
        {
            string secret = ApiKeyFormat.CreateSecret();
            var validator = NewValidatorWithKey(secret, expiredAt: DateTime.UtcNow.AddHours(1));

            var result = validator.Validate(ApiKeyFormat.Compose(SysId, secret));

            Assert.Equal(ApiKeyStatus.Valid, result.Status);
        }

        [Theory]
        [DisplayName("Validate returns Invalid for a malformed key without querying the data source")]
        [InlineData("no-separator")]
        [InlineData("Bad-SysId.secret")]
        [InlineData(".secret")]
        public void Validate_MalformedKey_ReturnsInvalidWithoutHittingDataSource(string apiKey)
        {
            var dataSource = new StubCacheDataSourceProvider(
                keyResolver: _ => throw new InvalidOperationException("should not be called"));
            var validator = new ApiKeyValidator(NewCache(dataSource));

            var result = validator.Validate(apiKey);

            Assert.Equal(ApiKeyStatus.Invalid, result.Status);
            Assert.Equal(string.Empty, result.SysId);
        }

        [Theory]
        [DisplayName("Validate returns NotProvided when the gate is in force but no key is sent")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_GateInForce_NoKey_ReturnsNotProvided(string? apiKey)
        {
            var validator = new ApiKeyValidator(NewCache(new StubCacheDataSourceProvider()));

            var result = validator.Validate(apiKey);

            Assert.Equal(ApiKeyStatus.NotProvided, result.Status);
            Assert.False(result.IsAccepted);
        }

        [Fact]
        [DisplayName("Validate returns NotConfigured when no key is active (compatibility state)")]
        public void Validate_GateNotInForce_ReturnsNotConfigured()
        {
            var dataSource = new StubCacheDataSourceProvider(
                gateResolver: () => new ApiKeyGateState { InForce = false });
            var validator = new ApiKeyValidator(NewCache(dataSource));

            var result = validator.Validate("anything");

            Assert.Equal(ApiKeyStatus.NotConfigured, result.Status);
            Assert.True(result.IsAccepted);
        }

        [Fact]
        [DisplayName("Validate returns NotConfigured when the cache has no data source (no key store)")]
        public void Validate_NoDataSource_ReturnsNotConfigured()
        {
            var validator = new ApiKeyValidator(NewCache());

            var result = validator.Validate("anything");

            Assert.Equal(ApiKeyStatus.NotConfigured, result.Status);
        }

        [Fact]
        [DisplayName("Validate propagates an exception from the data source so the caller fails closed")]
        public void Validate_DataSourceThrows_PropagatesForFailClosed()
        {
            var dataSource = new StubCacheDataSourceProvider(
                gateResolver: () => throw new InvalidOperationException("store unreachable"));
            var validator = new ApiKeyValidator(NewCache(dataSource));

            Assert.Throws<InvalidOperationException>(() => validator.Validate("anything"));
        }

        [Fact]
        [DisplayName("Validate serves a cached key on repeated calls without querying the data source again")]
        public void Validate_RepeatedCalls_LoadsKeyOnce()
        {
            string secret = ApiKeyFormat.CreateSecret();
            int callCount = 0;
            var info = new ApiKeyInfo
            {
                SysId = SysId,
                SysName = SysName,
                HashedKey = ApiKeyHasher.HashSecret(secret),
            };
            var dataSource = new StubCacheDataSourceProvider(keyResolver: _ =>
            {
                callCount++;
                return info;
            });
            var validator = new ApiKeyValidator(NewCache(dataSource));
            string apiKey = ApiKeyFormat.Compose(SysId, secret);

            Assert.Equal(ApiKeyStatus.Valid, validator.Validate(apiKey).Status);
            Assert.Equal(ApiKeyStatus.Valid, validator.Validate(apiKey).Status);

            Assert.Equal(1, callCount);
        }
    }
}
