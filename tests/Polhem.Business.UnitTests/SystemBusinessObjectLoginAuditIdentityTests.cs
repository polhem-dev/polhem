using System.ComponentModel;
using Polhem.Business.System;
using Polhem.Business.UnitTests.Fakes;
using Polhem.Definition.Logging;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;
using Polhem.Tests.Shared;
using Polhem.Base.Exceptions;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Caller identification in the login trail: `st_log_login` must tell which application is trying to log in. A batch
    /// of failures from one application and the same failures spread across several are entirely different signals.
    /// </summary>
    public class SystemBusinessObjectLoginAuditIdentityTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public SystemBusinessObjectLoginAuditIdentityTests(SharedDbFixture fx) { _fx = fx; }

        private sealed class CapturingAuditLogWriter : IAuditLogWriter
        {
            public List<AuditEntry> Entries { get; } = [];

            public void Write(AuditEntry entry) => Entries.Add(entry);
        }

        private static readonly AuditLogOptions s_loginAuditEnabled = new()
        {
            Enabled = true,
            LoginEnabled = true,
        };

        /// <summary>
        /// Triggers the login trail with a failed authentication. It needs no real credentials, and a failure is exactly where identifying the caller matters most.
        /// </summary>
        private LoginAuditEntry RunFailedLogin(ApiKeyValidationResult validation, CapturingAuditLogWriter writer)
        {
            var ctx = TestPolhemContext.CreateWithOverrides(_fx,
                (typeof(IAuditLogWriter), writer),
                (typeof(AuditLogOptions), s_loginAuditEnabled));
            var bo = new TestableSystemBusinessObject(ctx, Guid.Empty, _ => (false, string.Empty));
            ((IApiKeyContextAware)bo).ApiKeyValidation = validation;

            Assert.Throws<UserMessageException>(
                () => bo.Login(new LoginArgs { UserId = "user01", Password = "wrong" }));

            return Assert.IsType<LoginAuditEntry>(Assert.Single(writer.Entries));
        }

        [Fact]
        [DisplayName("The login trail records the calling application's key ID and name")]
        public void Login_WithApiKey_RecordsCallingApplication()
        {
            var writer = new CapturingAuditLogWriter();

            var entry = RunFailedLogin(
                new ApiKeyValidationResult(ApiKeyStatus.Valid, "northwind-desktop", "Northwind Desktop"),
                writer);

            Assert.Equal(LoginEvent.LoginFailed, entry.Event);
            Assert.Equal("northwind-desktop", entry.ApiKeyId);
            Assert.Equal("Northwind Desktop", entry.ApiKeyName);
        }

        [Fact]
        [DisplayName("A login that did not pass the key gate leaves the application identity null rather than an empty string")]
        public void Login_WithoutApiKey_LeavesIdentityNull()
        {
            var writer = new CapturingAuditLogWriter();

            var entry = RunFailedLogin(ApiKeyValidationResult.NotChecked, writer);

            Assert.Null(entry.ApiKeyId);
            Assert.Null(entry.ApiKeyName);
        }
    }
}
