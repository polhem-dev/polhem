using System.ComponentModel;
using Polhem.Business.System;
using Polhem.Business.UnitTests.Fakes;
using Polhem.Definition.Database;
using Polhem.Definition.Identity;
using Polhem.Tests.Shared;

using Polhem.Definition;
namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Verifies how SessionInfo is rebuilt from the <c>st_session</c> seed after the cache entry is gone.
    /// </summary>
    /// <remarks>
    /// Rebuilding reruns the derivation instead of restoring a snapshot: the seed carries only the token, user, expiry and company,
    /// while roles, the customize ID and the record scope are always recomputed, and the key is derived again by the provider.
    /// This is why a revoked permission does not linger in an old snapshot.
    /// </remarks>
    public class SessionRebuildTests : IClassFixture<SharedDbFixture>
    {
        private const string SeedCompanyId = "C001";
        private readonly SharedDbFixture _fx;

        public SessionRebuildTests(SharedDbFixture fx) { _fx = fx; }

        private ISessionInfoService SessionService => _fx.GetRequiredService<ISessionInfoService>();

        private Guid LoginAsSeedUser()
        {
            var bo = new TestableSystemBusinessObject(
                TestBusinessObjectContext.Create(_fx), Guid.Empty, _ => (true, "Seed User"));
            return bo.Login(new LoginArgs { UserId = "001", Password = "pwd" }).AccessToken;
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("After the cache is cleared, an equivalent SessionInfo is rebuilt from the seed")]
        public void Get_AfterCacheEviction_RebuildsFromSeed()
        {
            var accessToken = LoginAsSeedUser();
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), accessToken, SysProgIds.System);
            try
            {
                bo.EnterCompany(new EnterCompanyArgs { CompanyId = SeedCompanyId });
                var original = SessionService.Get(accessToken);

                // Simulates the 20-minute sliding eviction or a process restart: only the cache is cleared, and the seed remains.
                SessionService.Remove(accessToken);

                var rebuilt = SessionService.Get(accessToken);

                Assert.NotNull(rebuilt);
                Assert.Equal(accessToken, rebuilt!.AccessToken);
                Assert.Equal("001", rebuilt.UserId);
                Assert.Equal(SeedCompanyId, rebuilt.CompanyId);
                // The key is derived again from the access token and matches the one from login, so the Encrypted API still works.
                Assert.Equal(original!.ApiEncryptionKey, rebuilt.ApiEncryptionKey);
                // The record scope snapshotted by `EnterCompany` is recomputed, not restored.
                Assert.Equal(original.UserRowId, rebuilt.UserRowId);
                Assert.Equal(original.Culture, rebuilt.Culture);
                Assert.Equal(original.TimeZone, rebuilt.TimeZone);
            }
            finally
            {
                new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), accessToken, SysProgIds.System).Logout(new LogoutArgs());
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("A session that has not entered a company is rebuilt without a company after the cache is cleared")]
        public void Get_AfterCacheEviction_WithoutCompany_RebuildsCompanyLess()
        {
            var accessToken = LoginAsSeedUser();
            try
            {
                SessionService.Remove(accessToken);

                var rebuilt = SessionService.Get(accessToken);

                Assert.NotNull(rebuilt);
                Assert.Null(rebuilt!.CompanyId);
            }
            finally
            {
                new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), accessToken, SysProgIds.System).Logout(new LogoutArgs());
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("A token is not revived from the seed after logout")]
        public void Get_AfterLogout_DoesNotRebuild()
        {
            var accessToken = LoginAsSeedUser();
            new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), accessToken, SysProgIds.System).Logout(new LogoutArgs());

            Assert.Null(SessionService.Get(accessToken));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("A token that does not exist does not rebuild a session")]
        public void Get_UnknownToken_ReturnsNull()
        {
            Assert.Null(SessionService.Get(Guid.NewGuid()));
        }
    }
}
