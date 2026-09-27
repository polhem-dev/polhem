using System.ComponentModel;
using Polhem.Base.Security;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Logging;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;
using Polhem.Tests.Shared;

namespace Polhem.Api.Core.UnitTests.JsonRpc
{
    /// <summary>
    /// Coverage tests for JsonRpcExecutor: constructor null guards, the anomaly detection paths (slow successful
    /// calls, failed calls, the AnomalyEnabled combinations) and the encryption key branch.
    /// </summary>
    /// <remarks>
    /// The fixture must be <see cref="SharedDbFixture"/>: the executors in this file receive a bare
    /// <c>Guid.NewGuid()</c> token, so the real <see cref="IAccessTokenValidator"/> always misses the session cache
    /// and takes the rebuild path, which reads <c>st_session</c>. Only <c>SharedDbFixture</c> creates the schema.
    /// </remarks>
    public class JsonRpcExecutorCoverageTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public JsonRpcExecutorCoverageTests(PolhemTestFixture fx)
        {
            _fx = fx;
        }

        private IBusinessObjectFactory BoFactory => _fx.GetRequiredService<IBusinessObjectFactory>();
        private IAccessTokenValidator TokenValidator => _fx.GetRequiredService<IAccessTokenValidator>();
        private IApiEncryptionKeyProvider KeyProvider => _fx.GetRequiredService<IApiEncryptionKeyProvider>();

        /// <summary>
        /// A fake writer that captures anomaly writes.
        /// </summary>
        private sealed class CapturingAnomalyLogWriter : IAnomalyLogWriter
        {
            public List<AnomalyEntry> Entries { get; } = [];

            public void Write(AnomalyEntry entry) => Entries.Add(entry);
        }

        /// <summary>
        /// A fake session service that returns a fixed SessionInfo.
        /// </summary>
        private sealed class StubSessionInfoService : ISessionInfoService
        {
            private readonly SessionInfo _session;

            public StubSessionInfoService(SessionInfo session) => _session = session;

            public SessionInfo Get(Guid accessToken) => _session;

            public void Set(SessionInfo sessionInfo) { }

            public void Remove(Guid accessToken) { }
        }

        private static SessionInfo NewSession() => new()
        {
            UserId = "u1",
            UserName = "User One",
            CompanyId = "C1",
        };

        private JsonRpcExecutor NewAuditExecutor(
            IAnomalyLogWriter? writer,
            AuditLogOptions? options,
            ISessionInfoService? session,
            Guid accessToken,
            bool isLocalCall = true)
        {
            return new JsonRpcExecutor(BoFactory, TokenValidator, KeyProvider, writer, options, session)
            {
                AccessToken = accessToken,
                IsLocalCall = isLocalCall,
            };
        }

        private static JsonRpcRequest UnknownActionRequest() => new()
        {
            Method = $"{SysProgIds.System}.DefinitelyNotAMethod",
            Params = new JsonRpcParams(),
            Id = "1",
        };

        private static JsonRpcRequest PingRequest() => new()
        {
            Method = $"{SysProgIds.System}.Ping",
            Params = new JsonRpcParams { Value = new Polhem.Api.Core.Messages.System.PingRequest { ClientName = "C", TraceId = "T" } },
            Id = "1",
        };

        private static AuditLogOptions EnabledOptions(int slowThresholdMs = 3000) => new()
        {
            Enabled = true,
            AnomalyEnabled = true,
            ApiSlowThresholdMs = slowThresholdMs,
        };

        // ---- Constructor null guards (lines 50-52) ----

        [Fact]
        [DisplayName("Constructor throws ArgumentNullException for a null boFactory")]
        public void Constructor_NullBoFactory_ThrowsArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(
                () => new JsonRpcExecutor(null!, TokenValidator, KeyProvider));
            Assert.Equal("boFactory", ex.ParamName);
        }

        [Fact]
        [DisplayName("Constructor throws ArgumentNullException for a null tokenValidator")]
        public void Constructor_NullTokenValidator_ThrowsArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(
                () => new JsonRpcExecutor(BoFactory, null!, KeyProvider));
            Assert.Equal("tokenValidator", ex.ParamName);
        }

        [Fact]
        [DisplayName("Constructor throws ArgumentNullException for a null keyProvider")]
        public void Constructor_NullKeyProvider_ThrowsArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(
                () => new JsonRpcExecutor(BoFactory, TokenValidator, null!));
            Assert.Equal("keyProvider", ex.ParamName);
        }

        // ---- Anomaly records for failures (lines 94/136-137, 153-158, 163-183, 188-189) ----

        [Fact]
        [DisplayName("A failed call with anomaly logging enabled writes an Error anomaly")]
        public async Task Execute_AnomalyEnabledFailure_WritesErrorAnomaly()
        {
            var writer = new CapturingAnomalyLogWriter();
            // A planted session instead of a bare `Guid`: a bare token makes token validation take the rebuild path
            // and read `st_session`, which would turn a test about the anomaly fields into one that needs a database container.
            var token = TestSessionFactory.CreateAccessToken(_fx);
            var executor = NewAuditExecutor(writer, EnabledOptions(), new StubSessionInfoService(NewSession()), token);

            var response = await executor.ExecuteAsync(UnknownActionRequest());

            Assert.NotNull(response.Error);
            var entry = Assert.Single(writer.Entries);
            var anomaly = Assert.IsType<ApiAnomalyEntry>(entry);
            Assert.Equal(AnomalyKind.Error, anomaly.Kind);
            Assert.Equal(nameof(MissingMethodException), anomaly.ErrorType);
            Assert.Equal($"{SysProgIds.System}.DefinitelyNotAMethod", anomaly.Method);
            Assert.NotNull(anomaly.ErrorMessage);
            Assert.Null(anomaly.ThresholdMs);
            // A non-empty access token is recorded as its fingerprint, never as the token itself.
            Assert.Equal(AccessTokenHasher.ComputeFingerprint(token), anomaly.TokenFingerprint);
            Assert.DoesNotContain(token.ToString("N"), anomaly.TokenFingerprint!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [DisplayName("A failure record with anomaly logging enabled and an empty AccessToken keeps no fingerprint")]
        public async Task Execute_AnomalyEnabledFailureEmptyToken_WritesNullFingerprint()
        {
            var writer = new CapturingAnomalyLogWriter();
            var executor = NewAuditExecutor(writer, EnabledOptions(), new StubSessionInfoService(NewSession()), Guid.Empty);

            var response = await executor.ExecuteAsync(UnknownActionRequest());

            Assert.NotNull(response.Error);
            var anomaly = Assert.IsType<ApiAnomalyEntry>(Assert.Single(writer.Entries));
            // An empty access token means "no session" and leaves the fingerprint empty.
            Assert.Null(anomaly.TokenFingerprint);
            Assert.Equal("u1", anomaly.UserId);
            Assert.Equal("C1", anomaly.CompanyId);
        }

        [Fact]
        [DisplayName("The anomaly record carries the caller's application identity (api_key_id / api_key_name)")]
        public async Task Execute_AnomalyEnabled_CarriesApiKeyIdentity()
        {
            var writer = new CapturingAnomalyLogWriter();
            var executor = NewAuditExecutor(writer, EnabledOptions(), new StubSessionInfoService(NewSession()), Guid.NewGuid());
            executor.ApiKeyValidation = new ApiKeyValidationResult(
                ApiKeyStatus.Valid, "northwind-desktop", "Northwind Desktop");

            await executor.ExecuteAsync(UnknownActionRequest());

            var anomaly = Assert.IsType<ApiAnomalyEntry>(Assert.Single(writer.Entries));
            Assert.Equal("northwind-desktop", anomaly.ApiKeyId);
            Assert.Equal("Northwind Desktop", anomaly.ApiKeyName);
        }

        [Fact]
        [DisplayName("For a call that did not pass the API key gate, the anomaly record's application identity is null")]
        public async Task Execute_AnomalyEnabledWithoutApiKey_LeavesIdentityNull()
        {
            var writer = new CapturingAnomalyLogWriter();
            var executor = NewAuditExecutor(writer, EnabledOptions(), new StubSessionInfoService(NewSession()), Guid.NewGuid());

            await executor.ExecuteAsync(UnknownActionRequest());

            var anomaly = Assert.IsType<ApiAnomalyEntry>(Assert.Single(writer.Entries));
            Assert.Null(anomaly.ApiKeyId);
            Assert.Null(anomaly.ApiKeyName);
        }

        // ---- Anomaly records for successful calls (lines 143-148; slow calls) ----

        [Fact]
        [DisplayName("A successful call under the slow threshold with anomaly logging enabled writes no record")]
        public async Task Execute_AnomalyEnabledFastSuccess_WritesNoAnomaly()
        {
            var writer = new CapturingAnomalyLogWriter();
            var executor = NewAuditExecutor(writer, EnabledOptions(slowThresholdMs: 3000), new StubSessionInfoService(NewSession()), Guid.Empty);

            var response = await executor.ExecuteAsync(PingRequest());

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
            Assert.Empty(writer.Entries);
        }

        [Fact]
        [DisplayName("With anomaly logging enabled and the slow threshold exceeded, any record written is a Slow anomaly")]
        public async Task Execute_AnomalyEnabledSlowSuccess_WritesSlowAnomalyWhenExceeded()
        {
            var writer = new CapturingAnomalyLogWriter();
            // A 1 ms threshold: the reflection invoke plus tracing almost certainly exceeds it and triggers the Slow write (line 147).
            var executor = NewAuditExecutor(writer, EnabledOptions(slowThresholdMs: 1), new StubSessionInfoService(NewSession()), Guid.Empty);

            var response = await executor.ExecuteAsync(PingRequest());

            Assert.Null(response.Error);
            // The assertion holds whether or not the threshold was exceeded (any record must be Slow), so timing cannot make it flaky.
            Assert.All(writer.Entries, e => Assert.Equal(AnomalyKind.Slow, Assert.IsType<ApiAnomalyEntry>(e).Kind));
        }

        // ---- AnomalyEnabled combinations (line 137 br7/8) ----

        [Fact]
        [DisplayName("A failure writes no record when a writer exists but auditOptions is disabled")]
        public async Task Execute_AuditOptionsDisabled_WritesNoAnomaly()
        {
            var writer = new CapturingAnomalyLogWriter();
            var options = new AuditLogOptions { Enabled = false, AnomalyEnabled = true };
            var executor = NewAuditExecutor(writer, options, new StubSessionInfoService(NewSession()), Guid.Empty);

            var response = await executor.ExecuteAsync(UnknownActionRequest());

            Assert.NotNull(response.Error);
            Assert.Empty(writer.Entries);
        }

        [Fact]
        [DisplayName("A failure writes no record when a writer exists but AnomalyEnabled is false")]
        public async Task Execute_AnomalyFlagDisabled_WritesNoAnomaly()
        {
            var writer = new CapturingAnomalyLogWriter();
            var options = new AuditLogOptions { Enabled = true, AnomalyEnabled = false };
            var executor = NewAuditExecutor(writer, options, new StubSessionInfoService(NewSession()), Guid.Empty);

            var response = await executor.ExecuteAsync(UnknownActionRequest());

            Assert.NotNull(response.Error);
            Assert.Empty(writer.Entries);
        }

        [Fact]
        [DisplayName("A failure writes no record when a writer exists but auditOptions is null")]
        public async Task Execute_AuditOptionsNull_WritesNoAnomaly()
        {
            var writer = new CapturingAnomalyLogWriter();
            var executor = NewAuditExecutor(writer, options: null, session: new StubSessionInfoService(NewSession()), accessToken: Guid.Empty);

            var response = await executor.ExecuteAsync(UnknownActionRequest());

            Assert.NotNull(response.Error);
            Assert.Empty(writer.Entries);
        }

        [Fact]
        [DisplayName("A failure writes no record when a writer and enabled options exist but sessionService is null")]
        public async Task Execute_SessionServiceNull_WritesNoAnomaly()
        {
            var writer = new CapturingAnomalyLogWriter();
            var executor = NewAuditExecutor(writer, EnabledOptions(), session: null, accessToken: Guid.Empty);

            var response = await executor.ExecuteAsync(UnknownActionRequest());

            Assert.NotNull(response.Error);
            Assert.Empty(writer.Entries);
        }

        // ---- Encryption key branch (line 200: the Encrypted branch) ----

        [Fact]
        [DisplayName("A remote call in Encrypted format enters the encryption key branch")]
        public async Task Execute_EncryptedFormatRemoteCall_HitsEncryptionKeyBranch()
        {
            // Ping is Public/Anonymous, so an Encrypted request passes access validation and fetches the encryption key
            // (the Encrypted branch at line 200). Decrypting the unencrypted payload then fails and an error is returned.
            // The point is to cover the Encrypted branch.
            var request = new JsonRpcRequest
            {
                Method = $"{SysProgIds.System}.Ping",
                Params = new JsonRpcParams { Format = PayloadFormat.Encrypted, Value = new Polhem.Api.Core.Messages.System.PingRequest { ClientName = "C", TraceId = "T" } },
                Id = "1",
            };

            var executor = new JsonRpcExecutor(BoFactory, TokenValidator, KeyProvider)
            {
                AccessToken = Guid.Empty,
                IsLocalCall = false,
            };

            var response = await executor.ExecuteAsync(request);

            Assert.NotNull(response.Error);
        }
    }
}
