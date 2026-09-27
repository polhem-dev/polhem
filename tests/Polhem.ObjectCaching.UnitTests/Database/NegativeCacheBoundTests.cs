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
    /// Tests the negative caching of the two caches whose keys come from the caller: unknown access tokens
    /// (<see cref="SessionInfoCache"/>) and unknown API key identifiers (<see cref="ApiKeyCache"/>). A miss is
    /// remembered, but in a set capped per cache, so a stream of distinct unknown keys cannot grow memory without bound.
    /// </summary>
    public class NegativeCacheBoundTests
    {
        private sealed class CountingSource : ICacheDataSourceProvider
        {
            public int SessionCalls { get; private set; }
            public int ApiKeyCalls { get; private set; }
            public SessionInfo? Session { get; set; }

            public SessionInfo? GetSessionInfo(Guid accessToken) { SessionCalls++; return Session; }
            public CompanyInfo? GetCompanyInfo(string companyId) => null;
            public CompanyRolePermissions? GetCompanyRolePermissions(string companyId) => null;
            public DepartmentTree? GetDepartmentTree(string companyId) => null;
            public CompanyAuditRules? GetCompanyAuditRules(string companyId) => null;
            public ApiKeyInfo? GetApiKey(string sysId) { ApiKeyCalls++; return null; }
            public ApiKeyGateState GetApiKeyGateState() => new ApiKeyGateState { InForce = true };
        }

        private static string NewPrefix() => "t" + Guid.NewGuid().ToString("N");

        [Fact]
        [DisplayName("An unknown access token reaches the data source once, and a repeat is answered from the miss marker")]
        public void SessionInfoCache_UnknownToken_ReadsOnce()
        {
            var source = new CountingSource();
            var cache = new SessionInfoCache(() => source, NewPrefix());
            var token = Guid.NewGuid();

            Assert.Null(cache.Get(token));
            Assert.Null(cache.Get(token));

            Assert.Equal(1, source.SessionCalls);
        }

        [Fact]
        [DisplayName("Setting a session clears the miss marker for its token")]
        public void SessionInfoCache_SetAfterMiss_ServesTheSession()
        {
            var source = new CountingSource();
            var cache = new SessionInfoCache(() => source, NewPrefix());
            var token = Guid.NewGuid();
            Assert.Null(cache.Get(token));

            cache.Set(new SessionInfo { AccessToken = token, UserId = "u1" });

            Assert.Equal("u1", cache.Get(token)!.UserId);
        }

        [Fact]
        [DisplayName("Removing a token clears its miss marker, so the next lookup reads the data source again")]
        public void SessionInfoCache_RemoveAfterMiss_ReadsAgain()
        {
            var source = new CountingSource();
            var cache = new SessionInfoCache(() => source, NewPrefix());
            var token = Guid.NewGuid();
            Assert.Null(cache.Get(token));

            cache.Remove(token);
            source.Session = new SessionInfo { AccessToken = token, UserId = "u2" };

            Assert.Equal("u2", cache.Get(token)!.UserId);
            Assert.Equal(2, source.SessionCalls);
        }

        [Fact]
        [DisplayName("Miss markers for unknown access tokens are capped at MaxNegativeTokens")]
        public void SessionInfoCache_ManyUnknownTokens_MarkersAreCapped()
        {
            var cache = new SessionInfoCache(() => new CountingSource(), NewPrefix());

            for (int i = 0; i < SessionInfoCache.MaxNegativeTokens + 500; i++)
                cache.Get(Guid.NewGuid());

            Assert.Equal(SessionInfoCache.MaxNegativeTokens, cache.BoundedNegativeCount);
        }

        [Fact]
        [DisplayName("Miss markers for unknown API key identifiers are capped at MaxNegativeIds, and a repeat is not read again")]
        public void ApiKeyCache_ManyUnknownIds_MarkersAreCapped()
        {
            var source = new CountingSource();
            var cache = new ApiKeyCache(() => source, NewPrefix());

            Assert.Null(cache.Get("known-miss"));
            Assert.Null(cache.Get("known-miss"));
            Assert.Equal(1, source.ApiKeyCalls);

            for (int i = 0; i < ApiKeyCache.MaxNegativeIds + 500; i++)
                cache.Get($"probe-{i}");

            Assert.Equal(ApiKeyCache.MaxNegativeIds, cache.BoundedNegativeCount);
        }
    }
}
