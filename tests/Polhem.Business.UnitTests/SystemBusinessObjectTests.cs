using System.ComponentModel;
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
            var business = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.Empty, SysProgIds.System, isLocalCall: true);
            var args = new CreateSessionArgs
            {
                UserId = "001",
                ExpiresIn = 600
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
            var business = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.Empty, SysProgIds.System, isLocalCall: true);
            var args = new CreateSessionArgs { UserId = "__nonexistent_user_xyz__", ExpiresIn = 600 };

            Assert.Throws<InvalidOperationException>(() => business.CreateSession(args));
        }

        [Fact]
        [DisplayName("CreateSession on a business object built for a remote call throws NotSupportedException")]
        public void CreateSession_NotLocalCall_ThrowsNotSupported()
        {
            // The LocalOnly attribute is enforced only on the JSON-RPC dispatch path. A business object constructed
            // directly with isLocalCall: false must refuse to mint a token without a credential.
            var business = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.Empty, SysProgIds.System, isLocalCall: false);
            var args = new CreateSessionArgs { UserId = "001", ExpiresIn = 600 };

            var ex = Assert.Throws<NotSupportedException>(() => business.CreateSession(args));
            Assert.Contains("local calls", ex.Message);
        }
    }
}
