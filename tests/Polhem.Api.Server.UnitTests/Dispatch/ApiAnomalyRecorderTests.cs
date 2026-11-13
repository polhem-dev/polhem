using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Server.Dispatch;
using Polhem.Definition.Identity;
using Polhem.Definition.Logging;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;

namespace Polhem.Api.Server.UnitTests.Dispatch
{
    /// <summary>
    /// The classification, sanitizing and guard paths of <see cref="ApiAnomalyRecorder"/>, called directly with a
    /// call state so each branch is reached without running a call through the pipeline.
    /// </summary>
    public class ApiAnomalyRecorderTests
    {
        private const string Method = "System.Ping";

        private sealed class CapturingAnomalyLogWriter : IAnomalyLogWriter
        {
            public List<AnomalyEntry> Entries { get; } = [];

            public void Write(AnomalyEntry entry) => Entries.Add(entry);
        }

        private sealed class NullSessionInfoService : ISessionInfoService
        {
            public SessionInfo Get(Guid accessToken) => null!;

            public void Set(SessionInfo sessionInfo) { }

            public void Remove(Guid accessToken) { }
        }

        private static IServiceProvider Services(IAnomalyLogWriter? writer, AuditLogOptions? options)
        {
            var services = new ServiceCollection();
            if (writer != null) services.AddSingleton(writer);
            if (options != null) services.AddSingleton(options);
            services.AddSingleton<ISessionInfoService>(new NullSessionInfoService());
            return services.BuildServiceProvider();
        }

        private static PolhemCallState StartedState(ApiKeyValidationResult? apiKey = null) => new()
        {
            AccessToken = Guid.NewGuid(),
            Stopwatch = Stopwatch.StartNew(),
            ApiKeyValidation = apiKey ?? ApiKeyValidationResult.NotChecked,
        };

        private static AnomalyEntry RecordFailure(Exception exception, ApiKeyValidationResult? apiKey = null)
        {
            var writer = new CapturingAnomalyLogWriter();
            ApiAnomalyRecorder.RecordFailure(Services(writer, null), Method, StartedState(apiKey), exception);
            return Assert.Single(writer.Entries);
        }

        [Fact]
        [DisplayName("A TimeoutException is recorded as a Timeout anomaly")]
        public void RecordFailure_TimeoutException_RecordsTimeout()
        {
            Assert.Equal(AnomalyKind.Timeout, RecordFailure(new TimeoutException("expired")).Kind);
        }

        [Fact]
        [DisplayName("An exception whose message mentions a timeout is recorded as a Timeout anomaly")]
        public void RecordFailure_TimeoutInMessage_RecordsTimeout()
        {
            Assert.Equal(AnomalyKind.Timeout, RecordFailure(new InvalidOperationException("Command Timeout reached")).Kind);
        }

        [Fact]
        [DisplayName("Any other exception is recorded as an Error anomaly with its type name")]
        public void RecordFailure_OtherException_RecordsError()
        {
            var entry = RecordFailure(new InvalidOperationException("broken"));

            Assert.Equal(AnomalyKind.Error, entry.Kind);
            Assert.Equal(nameof(InvalidOperationException), entry.ErrorType);
        }

        [Fact]
        [DisplayName("The recorded message is flattened to one line and capped at 1000 characters")]
        public void RecordFailure_LongMultiLineMessage_IsFlattenedAndCapped()
        {
            var entry = RecordFailure(new InvalidOperationException("first\r\nsecond" + new string('x', 1200)));

            Assert.Equal(1000, entry.ErrorMessage!.Length);
            Assert.StartsWith("first  second", entry.ErrorMessage, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A session service that finds no session leaves the user fields empty")]
        public void RecordFailure_NoSession_LeavesUserFieldsNull()
        {
            var entry = RecordFailure(new InvalidOperationException("broken"));

            Assert.Null(entry.UserId);
            Assert.Null(entry.UserName);
            Assert.Null(entry.CompanyId);
        }

        [Fact]
        [DisplayName("The caller's API key identity is carried into the record")]
        public void RecordFailure_ValidatedApiKey_CarriesIdentity()
        {
            var entry = RecordFailure(new InvalidOperationException("broken"),
                new ApiKeyValidationResult(ApiKeyStatus.Valid, "erp-sync", "ERP sync"));

            Assert.Equal("erp-sync", entry.ApiKeyId);
            Assert.Equal("ERP sync", entry.ApiKeyName);
        }

        [Fact]
        [DisplayName("A failure without a stopwatch writes nothing")]
        public void RecordFailure_NoStopwatch_WritesNothing()
        {
            var writer = new CapturingAnomalyLogWriter();

            ApiAnomalyRecorder.RecordFailure(Services(writer, null), Method, new PolhemCallState(), new InvalidOperationException("x"));

            Assert.Empty(writer.Entries);
        }

        [Fact]
        [DisplayName("A failure with no anomaly writer registered writes nothing and does not throw")]
        public void RecordFailure_NoWriter_DoesNotThrow()
        {
            var exception = Record.Exception(() =>
                ApiAnomalyRecorder.RecordFailure(Services(null, null), Method, StartedState(), new InvalidOperationException("x")));

            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("A slow-call check with a zero threshold writes nothing")]
        public void RecordSlow_ZeroThreshold_WritesNothing()
        {
            var writer = new CapturingAnomalyLogWriter();
            var options = new AuditLogOptions { Enabled = true, AnomalyEnabled = true, ApiSlowThresholdMs = 0 };

            ApiAnomalyRecorder.RecordSlow(Services(writer, options), Method, StartedState());

            Assert.Empty(writer.Entries);
        }

        [Fact]
        [DisplayName("A slow-call check without audit options writes nothing")]
        public void RecordSlow_NoOptions_WritesNothing()
        {
            var writer = new CapturingAnomalyLogWriter();

            ApiAnomalyRecorder.RecordSlow(Services(writer, null), Method, StartedState());

            Assert.Empty(writer.Entries);
        }
    }
}
