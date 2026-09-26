using System.ComponentModel;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Repository.Abstractions.System;
using Polhem.Repository.System;
using Polhem.Tests.Shared;

namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// Verifies reading <c>st_user.time_zone</c> and <c>st_user.culture</c>, including the fallback when no value
    /// is set.
    /// </summary>
    /// <remarks>
    /// <see cref="UserRepository"/> resolves the <c>common</c> category internally and the caller cannot choose the
    /// database, so this is **not** a per-provider matrix: the tests run on whichever provider the common category
    /// is actually bound to. <c>[DbFact]</c> only skips the tests when that provider is unreachable.
    ///
    /// An empty value is an expected state, not an exceptional one: rows that existed before the columns were added
    /// have no value, and a deployment with custom authentication (not using <c>st_user</c>) has no matching row at
    /// all. The caller decides the fallback from that.
    /// For the design background see docs/adr/adr-032-datetime-timezone.md (D12).
    /// </remarks>
    public class UserRepositoryLocaleTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        public UserRepositoryLocaleTests(SharedDbFixture fx) { _fx = fx; }

        private UserRepository CreateRepo()
            => new UserRepository(TestRepositoryContext.Create(_fx.GetRequiredService<IDbConnectionManager>()), Guid.Empty, string.Empty);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("GetLocale('001') returns the seed user's time zone and culture")]
        public void GetLocale_SeedUser_ReturnsSeededValues()
        {
            var locale = CreateRepo().GetLocale("001");

            Assert.Equal("Asia/Taipei", locale.TimeZone);
            Assert.Equal("zh-TW", locale.Culture);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("GetLocale with an unknown user returns an empty value instead of throwing")]
        public void GetLocale_UnknownUser_ReturnsEmpty()
        {
            Assert.Equal(UserLocale.Empty, CreateRepo().GetLocale("no-such-user"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("GetLocale with a blank userId returns an empty value without querying the database")]
        public void GetLocale_BlankUserId_ReturnsEmpty(string userId)
        {
            Assert.Equal(UserLocale.Empty, CreateRepo().GetLocale(userId));
        }
    }
}
