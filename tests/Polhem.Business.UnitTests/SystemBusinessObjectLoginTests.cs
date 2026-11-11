using System.ComponentModel;
using Polhem.Core.Security;
using Polhem.Business.System;
using Polhem.Business.UnitTests.Fakes;
using Polhem.Definition.Identity;
using Polhem.Definition.Database;
using Polhem.Definition.Security;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;
using Polhem.Core.Exceptions;
using Polhem.Definition;
using Polhem.Definition.Language;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Branch tests for <see cref="SystemBusinessObject.Login"/>, using <see cref="TestableSystemBusinessObject"/>
    /// to override AuthenticateUser and trigger the success, failure and lockout paths.
    /// </summary>
    public class SystemBusinessObjectLoginTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public SystemBusinessObjectLoginTests(SharedDbFixture fx) { _fx = fx; }
        private sealed class RecordingTracker : ILoginAttemptTracker
        {
            public bool LockedOut { get; set; }
            public int LookupCount { get; private set; }
            public int FailureCount { get; private set; }
            public int ResetCount { get; private set; }

            public bool IsLockedOut(string userId)
            {
                LookupCount++;
                return LockedOut;
            }
            public void RecordFailure(string userId) => FailureCount++;
            public void Reset(string userId) => ResetCount++;
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Login with successful authentication produces an AccessToken and expiry and creates a SessionInfo")]
        public void Login_Authenticated_ReturnsValidSessionToken()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            var bo = new TestableSystemBusinessObject(
                TestBusinessObjectContext.Create(_fx),
                Guid.Empty,
                _ => (true, "User One"));
            var args = new LoginArgs { UserId = "user01", Password = "pwd" };

            var result = bo.Login(args);

            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.AccessToken);
            Assert.True(result.ExpiredAt > DateTime.UtcNow);
            Assert.Equal("user01", result.UserId);
            Assert.Equal("User One", result.UserName);
            // Without a `ClientPublicKey`, the encrypted API encryption key stays an empty string.
            Assert.Equal(string.Empty, result.ApiEncryptionKey);

            var session = sessionService.Get(result.AccessToken);
            try
            {
                Assert.NotNull(session);
                Assert.Equal("user01", session!.UserId);
                Assert.NotEmpty(session.ApiEncryptionKey);
            }
            finally
            {
                sessionService.Remove(result.AccessToken);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Login with a ClientPublicKey encrypts ApiEncryptionKey with RSA")]
        public void Login_WithClientPublicKey_EncryptsApiKey()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            RsaCryptor.GenerateRsaKeyPair(out var publicKey, out var privateKey);
            var bo = new TestableSystemBusinessObject(
                TestBusinessObjectContext.Create(_fx),
                Guid.Empty,
                _ => (true, "RSA User"));
            var args = new LoginArgs
            {
                UserId = "rsa_user",
                Password = "x",
                ClientPublicKey = publicKey
            };

            var result = bo.Login(args);

            try
            {
                Assert.False(string.IsNullOrWhiteSpace(result.ApiEncryptionKey));
                var sessionKeyBase64 = RsaCryptor.DecryptWithPrivateKey(result.ApiEncryptionKey, privateKey);
                Assert.False(string.IsNullOrWhiteSpace(sessionKeyBase64));

                var session = sessionService.Get(result.AccessToken);
                Assert.NotNull(session);
                Assert.Equal(Convert.ToBase64String(session!.ApiEncryptionKey), sessionKeyBase64);
            }
            finally
            {
                sessionService.Remove(result.AccessToken);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Login takes the user's time zone and locale from st_user")]
        public void Login_SeedUser_AppliesLocaleFromUserRow()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            var bo = new TestableSystemBusinessObject(
                TestBusinessObjectContext.Create(_fx),
                Guid.Empty,
                _ => (true, "測試管理員"));

            var result = bo.Login(new LoginArgs { UserId = "001", Password = "x" });

            try
            {
                var session = sessionService.Get(result.AccessToken);
                Assert.NotNull(session);
                Assert.Equal("Asia/Taipei", session!.TimeZone);
                Assert.Equal("zh-TW", session.Culture);
                // The client renders in the account's culture, so the login result carries it.
                Assert.Equal(session.Culture, result.Culture);
                Assert.Equal(session.TimeZone, result.TimeZone);
            }
            finally
            {
                sessionService.Remove(result.AccessToken);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Login falls back to the deployment's default locale and time zone when the user has no st_user row")]
        public void Login_UserWithoutRow_FallsBackToDeploymentDefaults()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            var settings = _fx.GetRequiredService<IDefineAccess>().GetSystemSettings();
            var bo = new TestableSystemBusinessObject(
                TestBusinessObjectContext.Create(_fx),
                Guid.Empty,
                _ => (true, "No Row"));

            var result = bo.Login(new LoginArgs { UserId = "no-such-user", Password = "x" });

            try
            {
                var session = sessionService.Get(result.AccessToken);
                Assert.NotNull(session);
                Assert.Equal(settings.BackendConfiguration.DefaultTimeZone, session!.TimeZone);
                Assert.Equal(settings.CommonConfiguration.DefaultLanguage, session.Culture);
                Assert.Equal(session.Culture, result.Culture);
            }
            finally
            {
                sessionService.Remove(result.AccessToken);
            }
        }

        [Fact]
        [DisplayName("Login with failed authentication throws UserMessageException and records the failure in the tracker")]
        public void Login_AuthenticateFails_ThrowsAndRecordsFailure()
        {
            var tracker = new RecordingTracker();
            var ctx = TestBusinessObjectContext.CreateWithOverrides(_fx, (typeof(ILoginAttemptTracker), tracker));
            var bo = new TestableSystemBusinessObject(
                ctx,
                Guid.Empty,
                _ => (false, string.Empty));
            var args = new LoginArgs { UserId = "bad", Password = "bad" };

            Assert.Throws<UserMessageException>(() => bo.Login(args));
            Assert.Equal(1, tracker.FailureCount);
            Assert.Equal(0, tracker.ResetCount);
        }

        [Theory]
        [InlineData(UserInfo.UserIdMaxLength + 1, 8)]
        [InlineData(1_000_000, 8)]
        [InlineData(8, 1025)]
        [DisplayName("Login refuses an over-long user id or password as invalid credentials before the tracker or the authentication sees it")]
        public void Login_OverLongCredential_RefusedBeforeTrackerAndAuthentication(int userIdLength, int passwordLength)
        {
            var tracker = new RecordingTracker();
            var authCalls = 0;
            var ctx = TestBusinessObjectContext.CreateWithOverrides(_fx, (typeof(ILoginAttemptTracker), tracker));
            var bo = new TestableSystemBusinessObject(ctx, Guid.Empty, _ =>
            {
                authCalls++;
                return (true, "anything");
            });
            var args = new LoginArgs { UserId = new string('u', userIdLength), Password = new string('p', passwordLength) };

            var ex = Assert.Throws<UserMessageException>(() => bo.Login(args));

            Assert.Equal(PolhemMessages.LoginInvalidCredentials, ex.MessageKey);
            Assert.Equal(0, authCalls);
            Assert.Equal(0, tracker.LookupCount);
            Assert.Equal(0, tracker.FailureCount);
        }

        [Fact]
        [DisplayName("Login passes a user id of exactly the maximum length on to the tracker and the authentication")]
        public void Login_UserIdAtMaximum_ReachesAuthentication()
        {
            var tracker = new RecordingTracker();
            var authCalls = 0;
            var ctx = TestBusinessObjectContext.CreateWithOverrides(_fx, (typeof(ILoginAttemptTracker), tracker));
            var bo = new TestableSystemBusinessObject(ctx, Guid.Empty, _ =>
            {
                authCalls++;
                return (false, string.Empty);
            });
            var args = new LoginArgs { UserId = new string('u', UserInfo.UserIdMaxLength), Password = "x" };

            Assert.Throws<UserMessageException>(() => bo.Login(args));

            Assert.Equal(1, authCalls);
            Assert.Equal(1, tracker.FailureCount);
        }

        [Theory]
        [InlineData(UserInfo.UserIdMaxLength, 1)]
        [InlineData(UserInfo.UserIdMaxLength + 1, 0)]
        [DisplayName("Login counts the user id in Unicode characters, so an id outside the BMP is measured by its characters, not its UTF-16 units")]
        public void Login_UserIdOutsideBmp_CountedInCharacters(int characters, int expectedAuthCalls)
        {
            var authCalls = 0;
            var ctx = TestBusinessObjectContext.CreateWithOverrides(_fx, (typeof(ILoginAttemptTracker), new RecordingTracker()));
            var bo = new TestableSystemBusinessObject(ctx, Guid.Empty, _ =>
            {
                authCalls++;
                return (false, string.Empty);
            });
            var args = new LoginArgs { UserId = string.Concat(Enumerable.Repeat("\U0001F600", characters)), Password = "x" };

            Assert.Throws<UserMessageException>(() => bo.Login(args));

            Assert.Equal(expectedAuthCalls, authCalls);
        }

        [Fact]
        [DisplayName("A subclass that raises MaxLoginUserIdLength accepts a longer user id")]
        public void Login_SubclassRaisesLimit_AcceptsLongerUserId()
        {
            var tracker = new RecordingTracker();
            var authCalls = 0;
            var ctx = TestBusinessObjectContext.CreateWithOverrides(_fx, (typeof(ILoginAttemptTracker), tracker));
            var bo = new DirectoryLoginObject(ctx, _ =>
            {
                authCalls++;
                return (false, string.Empty);
            });
            var args = new LoginArgs { UserId = "someone@directory.example.com", Password = "x" };

            Assert.Throws<UserMessageException>(() => bo.Login(args));

            Assert.Equal(1, authCalls);
        }

        private sealed class DirectoryLoginObject(IBusinessObjectContext ctx, Func<LoginArgs, (bool, string)> authenticator)
            : TestableSystemBusinessObject(ctx, Guid.Empty, authenticator)
        {
            protected override int MaxLoginUserIdLength => 256;
        }

        [Fact]
        [DisplayName("Login for a locked-out account throws UserMessageException without authenticating")]
        public void Login_AccountLockedOut_ThrowsBeforeAuthenticate()
        {
            var tracker = new RecordingTracker { LockedOut = true };
            var authCalls = 0;
            var ctx = TestBusinessObjectContext.CreateWithOverrides(_fx, (typeof(ILoginAttemptTracker), tracker));
            var bo = new TestableSystemBusinessObject(
                ctx,
                Guid.Empty,
                _ =>
                {
                    authCalls++;
                    return (true, "anything");
                });
            var args = new LoginArgs { UserId = "locked", Password = "x" };

            Assert.Throws<UserMessageException>(() => bo.Login(args));
            Assert.Equal(0, authCalls);
            Assert.Equal(0, tracker.FailureCount);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Login with successful authentication and a non-null tracker calls Reset")]
        public void Login_SuccessWithTracker_CallsReset()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            var tracker = new RecordingTracker();
            var ctx = TestBusinessObjectContext.CreateWithOverrides(_fx, (typeof(ILoginAttemptTracker), tracker));
            var bo = new TestableSystemBusinessObject(
                ctx,
                Guid.Empty,
                _ => (true, "ok"));
            var args = new LoginArgs { UserId = "u", Password = "p" };

            var result = bo.Login(args);
            try
            {
                Assert.Equal(1, tracker.ResetCount);
                Assert.Equal(0, tracker.FailureCount);
            }
            finally
            {
                sessionService.Remove(result.AccessToken);
            }
        }
    }
}
