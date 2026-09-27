using System.ComponentModel;
using Polhem.Base.Security;
using Polhem.Business.System;
using Polhem.Business.UnitTests.Fakes;
using Polhem.Definition.Identity;
using Polhem.Definition.Database;
using Polhem.Definition.Security;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;
using Polhem.Base.Exceptions;

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
            public int FailureCount { get; private set; }
            public int ResetCount { get; private set; }

            public bool IsLockedOut(string userId) => LockedOut;
            public void RecordFailure(string userId) => FailureCount++;
            public void Reset(string userId) => ResetCount++;
        }

        [Fact]
        [DisplayName("Login with successful authentication produces an AccessToken and expiry and creates a SessionInfo")]
        public void Login_Authenticated_ReturnsValidSessionToken()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            var bo = new TestableSystemBusinessObject(
                TestPolhemContext.Create(_fx),
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

        [Fact]
        [DisplayName("Login with a ClientPublicKey encrypts ApiEncryptionKey with RSA")]
        public void Login_WithClientPublicKey_EncryptsApiKey()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            RsaCryptor.GenerateRsaKeyPair(out var publicKey, out var privateKey);
            var bo = new TestableSystemBusinessObject(
                TestPolhemContext.Create(_fx),
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
                TestPolhemContext.Create(_fx),
                Guid.Empty,
                _ => (true, "測試管理員"));

            var result = bo.Login(new LoginArgs { UserId = "001", Password = "x" });

            try
            {
                var session = sessionService.Get(result.AccessToken);
                Assert.NotNull(session);
                Assert.Equal("Asia/Taipei", session!.TimeZone);
                Assert.Equal("zh-TW", session.Culture);
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
            var backend = _fx.GetRequiredService<IDefineAccess>()
                .GetSystemSettings().BackendConfiguration;
            var bo = new TestableSystemBusinessObject(
                TestPolhemContext.Create(_fx),
                Guid.Empty,
                _ => (true, "No Row"));

            var result = bo.Login(new LoginArgs { UserId = "no-such-user", Password = "x" });

            try
            {
                var session = sessionService.Get(result.AccessToken);
                Assert.NotNull(session);
                Assert.Equal(backend.DefaultTimeZone, session!.TimeZone);
                Assert.Equal(backend.DefaultLanguage, session.Culture);
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
            var ctx = TestPolhemContext.CreateWithOverrides(_fx, (typeof(ILoginAttemptTracker), tracker));
            var bo = new TestableSystemBusinessObject(
                ctx,
                Guid.Empty,
                _ => (false, string.Empty));
            var args = new LoginArgs { UserId = "bad", Password = "bad" };

            Assert.Throws<UserMessageException>(() => bo.Login(args));
            Assert.Equal(1, tracker.FailureCount);
            Assert.Equal(0, tracker.ResetCount);
        }

        [Fact]
        [DisplayName("Login for a locked-out account throws UserMessageException without authenticating")]
        public void Login_AccountLockedOut_ThrowsBeforeAuthenticate()
        {
            var tracker = new RecordingTracker { LockedOut = true };
            var authCalls = 0;
            var ctx = TestPolhemContext.CreateWithOverrides(_fx, (typeof(ILoginAttemptTracker), tracker));
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

        [Fact]
        [DisplayName("Login with successful authentication and a non-null tracker calls Reset")]
        public void Login_SuccessWithTracker_CallsReset()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            var tracker = new RecordingTracker();
            var ctx = TestPolhemContext.CreateWithOverrides(_fx, (typeof(ILoginAttemptTracker), tracker));
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
