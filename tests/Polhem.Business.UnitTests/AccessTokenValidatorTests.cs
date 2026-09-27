using System.ComponentModel;
using Polhem.Business.Validator;
using Polhem.Definition.Identity;
using Polhem.Tests.Shared;
using Polhem.Base.Exceptions;
using Polhem.Definition.Database;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Behavior tests for <see cref="AccessTokenValidator"/>.
    /// </summary>
    public class AccessTokenValidatorTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public AccessTokenValidatorTests(SharedDbFixture fx) { _fx = fx; }
        private AccessTokenValidator CreateValidator()
            => new(_fx.GetRequiredService<ISessionInfoService>());

        [Fact]
        [DisplayName("Validate(Guid.Empty) throws AuthenticationRequiredException")]
        public void Validate_Empty_ThrowsUnauthorized()
        {
            var provider = CreateValidator();
            Assert.Throws<AuthenticationRequiredException>(() => provider.Validate(Guid.Empty));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Validate with an unknown AccessToken throws AuthenticationRequiredException")]
        public void Validate_UnknownToken_ThrowsUnauthorized()
        {
            var provider = CreateValidator();
            var token = Guid.NewGuid();

            Assert.Throws<AuthenticationRequiredException>(() => provider.Validate(token));
        }

        [Fact]
        [DisplayName("Validate with an expired session throws AuthenticationRequiredException")]
        public void Validate_ExpiredSession_ThrowsUnauthorized()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            var provider = new AccessTokenValidator(sessionService);
            var token = Guid.NewGuid();
            var expired = new SessionInfo
            {
                AccessToken = token,
                UserId = "u01",
                UserName = "U01",
                ExpiredAt = DateTime.UtcNow.AddMinutes(-5),
                ApiEncryptionKey = new byte[64]
            };
            sessionService.Set(expired);

            try
            {
                Assert.Throws<AuthenticationRequiredException>(() => provider.Validate(token));
            }
            finally
            {
                sessionService.Remove(token);
            }
        }

        [Fact]
        [DisplayName("Validate with a valid session returns true")]
        public void Validate_ValidSession_ReturnsTrue()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            var provider = new AccessTokenValidator(sessionService);
            var token = Guid.NewGuid();
            var session = new SessionInfo
            {
                AccessToken = token,
                UserId = "u01",
                UserName = "U01",
                ExpiredAt = DateTime.UtcNow.AddHours(1),
                ApiEncryptionKey = new byte[64]
            };
            sessionService.Set(session);

            try
            {
                Assert.True(provider.Validate(token));
            }
            finally
            {
                sessionService.Remove(token);
            }
        }
    }
}
