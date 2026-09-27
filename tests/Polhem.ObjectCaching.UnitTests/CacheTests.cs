using System.ComponentModel;
using Polhem.Definition.Identity;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;
using Polhem.Definition.Database;

namespace Polhem.ObjectCaching.UnitTests
{
    /// <summary>
    /// Cache behavior tests run through the fixture's DI container. The cache takes <c>PathOptions</c> by
    /// injection instead of reading process-wide static state.
    /// </summary>
    /// <remarks>
    /// The fixture must be <see cref="SharedDbFixture"/>: after the removal,
    /// <c>SessionInfo_SetAndRemove_BehavesCorrectly</c> calls <c>Get</c> with the same token, which is always a
    /// cache miss and takes the rebuild path that reads <c>st_session</c>. "No such session" holds only because
    /// that query returns nothing. Only <c>SharedDbFixture</c> creates the schema.
    /// </remarks>
    public class CacheTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public CacheTests(SharedDbFixture fx)
        {
            _fx = fx;
        }

        [Fact]
        [DisplayName("GetSystemSettings called repeatedly returns the same cached instance")]
        public void GetSystemSettings_CalledMultipleTimes_ReturnsSameCachedInstance()
        {
            var defineAccess = _fx.GetRequiredService<IDefineAccess>();
            var settings = defineAccess.GetSystemSettings();
            for (int i = 0; i < 10; i++)
            {
                var cache = defineAccess.GetSystemSettings();
                Assert.Equal(settings, cache);
            }
        }

        [Fact]
        [DisplayName("GetDatabaseSettings called repeatedly returns the same cached instance")]
        public void GetDatabaseSettings_CalledMultipleTimes_ReturnsSameCachedInstance()
        {
            var defineAccess = _fx.GetRequiredService<IDefineAccess>();
            var settings = defineAccess.GetDatabaseSettings();
            for (int i = 0; i < 10; i++)
            {
                var cache = defineAccess.GetDatabaseSettings();
                Assert.Equal(settings, cache);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("A cached session can be read after Set and returns null after Remove")]
        public void SessionInfo_SetAndRemove_BehavesCorrectly()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            var cache = _fx.GetRequiredService<ICacheContainer>();
            var sessionInfo = new SessionInfo
            {
                AccessToken = Guid.NewGuid(),
                UserId = "test_user",
                UserName = "Test User"
            };
            sessionService.Set(sessionInfo);

            // Read through the fixture's `ICacheContainer`, which shares the fixture's cache key prefix.
            // A container with a different prefix would not see the entry.
            var sessionInfoFromCache = cache.SessionInfo.Get(sessionInfo.AccessToken);
            Assert.NotNull(sessionInfoFromCache);
            Assert.Equal(sessionInfo.AccessToken, sessionInfoFromCache!.AccessToken);

            sessionService.Remove(sessionInfo.AccessToken);
            Assert.Null(sessionService.Get(sessionInfo.AccessToken));
        }
    }
}
