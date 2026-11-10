using Polhem.Definition.Identity;

namespace Polhem.Tests.Shared
{
    /// <summary>
    /// Session helpers for tests.
    /// Plants a valid <c>SessionInfo</c> in the <see cref="ISessionInfoService"/> of a given
    /// <see cref="PolhemTestFixture"/>, so tests that need an access token do not have to go through login,
    /// which would need a user row with a stored password hash and a database.
    /// </summary>
    public static class TestSessionFactory
    {
        /// <summary>
        /// Creates a valid test access token and writes the matching <c>SessionInfo</c> into the fixture's
        /// session service.
        /// </summary>
        /// <param name="fixture">The fixture that holds the target session service.</param>
        /// <param name="userId">The user account; defaults to "test".</param>
        /// <param name="expiresIn">How long the session is valid; defaults to one hour.</param>
        /// <param name="companyId">The company the session has entered; empty leaves it outside any company.</param>
        /// <returns>A valid access token.</returns>
        public static Guid CreateAccessToken(PolhemTestFixture fixture, string userId = "test", TimeSpan? expiresIn = null,
            string companyId = "")
        {
            ArgumentNullException.ThrowIfNull(fixture);
            var sessionService = fixture.GetRequiredService<ISessionInfoService>();
            var accessToken = Guid.NewGuid();
            var session = new SessionInfo
            {
                AccessToken = accessToken,
                UserId = userId,
                UserName = userId,
                ExpiredAt = DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromHours(1)),
                ApiEncryptionKey = Array.Empty<byte>()
            };
            if (companyId.Length > 0)
                session.CompanyScope = new SessionCompanyScope(companyId, string.Empty, [], Guid.Empty, Guid.Empty, Guid.Empty);
            sessionService.Set(session);
            return accessToken;
        }
    }
}
