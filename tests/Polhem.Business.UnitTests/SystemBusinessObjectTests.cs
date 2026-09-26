using System.ComponentModel;
using Polhem.Base.Security;
using Polhem.Business.System;
using Polhem.Tests.Shared;
using Polhem.Definition.Database;
using Polhem.Definition.Identity;

using Polhem.Definition;
namespace Polhem.Business.UnitTests
{
    public class SystemBusinessObjectTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public SystemBusinessObjectTests(SharedDbFixture fx) { _fx = fx; }
        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("CreateSession with valid arguments returns a result with an AccessToken and an expiry")]
        public void CreateSession_ValidArgs_ReturnsTokenWithExpiry()
        {
            // Arrange
            var business = new SystemBusinessObject(TestPolhemContext.Create(_fx), Guid.Empty, SysProgIds.System);
            var args = new CreateSessionArgs
            {
                UserID = "001",
                ExpiresIn = 600,
                OneTime = false
            };

            // Act
            var result = business.CreateSession(args);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.AccessToken);
            Assert.True(result.ExpiredAt > DateTime.UtcNow);

            // It follows the same construction path as Login: resolve the user name, apply the locale, generate the key, write the seed and write the cache.
            // It used to do a single raw INSERT, so the token had no session in the cache and was unusable.
            var session = _fx.GetRequiredService<ISessionInfoService>().Get(result.AccessToken);
            try
            {
                Assert.NotNull(session);
                Assert.Equal("001", session!.UserId);
                Assert.NotEmpty(session.UserName);
                Assert.NotEmpty(session.ApiEncryptionKey);
                Assert.NotEmpty(session.Culture);
            }
            finally
            {
                _fx.GetRequiredService<ISessionInfoService>().Remove(result.AccessToken);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("CreateSession with a user ID that does not exist throws InvalidOperationException")]
        public void CreateSession_NonExistentUserId_ThrowsInvalidOperation()
        {
            var business = new SystemBusinessObject(TestPolhemContext.Create(_fx), Guid.Empty, SysProgIds.System);
            var args = new CreateSessionArgs { UserID = "__nonexistent_user_xyz__", ExpiresIn = 600 };

            Assert.Throws<InvalidOperationException>(() => business.CreateSession(args));
        }

        [Fact]
        [DisplayName("CreateSession asking for a one-time token throws NotSupportedException instead of silently degrading")]
        public void CreateSession_OneTime_ThrowsNotSupported()
        {
            var business = new SystemBusinessObject(TestPolhemContext.Create(_fx), Guid.Empty, SysProgIds.System);
            var args = new CreateSessionArgs { UserID = "001", ExpiresIn = 600, OneTime = true };

            // Because the session is written to the cache on creation, the first use is a cache hit and delete-on-read never fires,
            // so the one-time semantics have nowhere to take effect. Letting a security guarantee fail silently is the worst option, so it is rejected explicitly.
            Assert.Throws<NotSupportedException>(() => business.CreateSession(args));
        }

        /// <summary>
        /// Logs in and verifies the exchange of the RSA-encrypted key.
        /// </summary>
        // The login flow can only be verified by overriding `SystemBusinessObject.AuthenticateUser` (the base implementation always returns false).
        // Enable this test once a test subclass exists.
#pragma warning disable xUnit1004 // Test methods should not be skipped — placeholder retained as TODO marker; see comment above.
        [Fact(Skip = "Requires a test subclass that overrides AuthenticateUser; not yet in place.")]
#pragma warning restore xUnit1004
        [DisplayName("Login with an RSA key pair returns an encrypted session key that can be decrypted")]
        public void Login_WithRsaKeyPair_ReturnsDecryptableSessionKey()
        {
            // Arrange
            RsaCryptor.GenerateRsaKeyPair(out var publicKey, out var privateKey);

            var sbo = new SystemBusinessObject(TestPolhemContext.Create(_fx), Guid.Empty, SysProgIds.System);
            var args = new LoginArgs
            {
                UserId = "testuser",
                Password = "testpassword",
                ClientPublicKey = publicKey
            };

            // Act
            LoginResult result = sbo.Login(args);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.ApiEncryptionKey);

            string sessionKey = RsaCryptor.DecryptWithPrivateKey(result.ApiEncryptionKey, privateKey);
            Assert.False(string.IsNullOrWhiteSpace(sessionKey));
        }
    }
}
