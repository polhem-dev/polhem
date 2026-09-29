using System.ComponentModel;
using Polhem.Business.Providers;
using Polhem.Definition.Identity;
using Polhem.Tests.Shared;
using Polhem.Core.Exceptions;
using Polhem.Definition.Database;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Behavior tests for <see cref="DynamicApiEncryptionKeyProvider"/>.
    /// </summary>
    public class DynamicApiEncryptionKeyProviderTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public DynamicApiEncryptionKeyProviderTests(SharedDbFixture fx) { _fx = fx; }
        private DynamicApiEncryptionKeyProvider CreateProvider()
            => new(_fx.GetRequiredService<ISessionInfoService>());

        [Fact]
        [DisplayName("GetKey(Guid.Empty) throws AuthenticationRequiredException")]
        public void GetKey_Empty_ThrowsUnauthorized()
        {
            var provider = CreateProvider();
            Assert.Throws<AuthenticationRequiredException>(() => provider.GetKey(Guid.Empty));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("GetKey with an unknown AccessToken throws AuthenticationRequiredException")]
        public void GetKey_UnknownToken_ThrowsUnauthorized()
        {
            var provider = CreateProvider();
            var unknownToken = Guid.NewGuid();

            Assert.Throws<AuthenticationRequiredException>(() => provider.GetKey(unknownToken));
        }

        [Fact]
        [DisplayName("GetKey with a valid session returns the matching ApiEncryptionKey")]
        public void GetKey_ValidSession_ReturnsKey()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            var provider = new DynamicApiEncryptionKeyProvider(sessionService);
            var token = Guid.NewGuid();
            var key = new byte[64];
            for (int i = 0; i < key.Length; i++) key[i] = (byte)i;

            var session = new SessionInfo
            {
                AccessToken = token,
                UserId = "u01",
                UserName = "U01",
                ExpiredAt = DateTime.UtcNow.AddHours(1),
                ApiEncryptionKey = key
            };
            sessionService.Set(session);

            try
            {
                var actual = provider.GetKey(token);
                Assert.Equal(key, actual);
            }
            finally
            {
                sessionService.Remove(token);
            }
        }

        [Fact]
        [DisplayName("GenerateKeyForLogin returns 64 bytes")]
        public void GenerateKeyForLogin_Returns64Bytes()
        {
            var provider = CreateProvider();

            var key = provider.GenerateKeyForLogin(Guid.NewGuid());

            Assert.NotNull(key);
            Assert.Equal(64, key.Length);
        }
    }
}
