using System.ComponentModel;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages;
using Polhem.Core.Security;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Logging;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;
using Polhem.Tests.Shared;

namespace Polhem.Api.Core.UnitTests.Dispatch
{
    /// <summary>
    /// The anomaly records the framework's JSON-RPC pipeline writes (slow successful calls, failed calls, the
    /// AnomalyEnabled combinations) and the encryption key branch of a remote call.
    /// </summary>
    /// <remarks>
    /// <see cref="PolhemTestFixture"/> is enough because no test here reaches the database. Every call runs under
    /// either <see cref="Guid.Empty"/> (no session to look up) or a token planted with
    /// <see cref="TestSessionFactory.CreateAccessToken"/>. A bare <c>Guid.NewGuid()</c> would miss the session cache,
    /// and both <see cref="IAccessTokenValidator"/> and the business object factory would then rebuild the session
    /// from <c>st_session</c>, which this fixture does not create.
    /// </remarks>
    public class DispatchAnomalyTests : IClassFixture<PolhemTestFixture>
    {
        /// <summary>
        /// A Ping whose body cannot be read into its parameter, so the call fails inside the pipeline, where the
        /// exception mapper sees it. An unknown action is answered by the dispatcher itself and leaves no record.
        /// </summary>
        private const string UnreadablePing =
            """{"jsonrpc":"2.0","method":"System.Ping","params":{"format":0,"value":{"clientName":5}},"id":"1"}""";

        private readonly PolhemTestFixture _fx;

        public DispatchAnomalyTests(PolhemTestFixture fx)
        {
            _fx = fx;
        }

        private IBusinessObjectFactory BoFactory => _fx.GetRequiredService<IBusinessObjectFactory>();

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

        private sealed class FixedApiKeyValidator(ApiKeyValidationResult result) : IApiKeyValidator
        {
            public ApiKeyValidationResult Validate(string? apiKey) => result;
        }

        private static SessionInfo NewSession() => new()
        {
            UserId = "u1",
            UserName = "User One",
            CompanyId = "C1",
        };

        private IServiceProvider AuditServices(IAnomalyLogWriter? writer, AuditLogOptions? options, ISessionInfoService? session)
            => new TestOverrideServiceProvider(_fx.Provider,
                (typeof(IAnomalyLogWriter), writer),
                (typeof(AuditLogOptions), options),
                (typeof(ISessionInfoService), session));

        private TestDispatcher NewAuditDispatcher(
            IAnomalyLogWriter? writer,
            AuditLogOptions? options,
            ISessionInfoService? session,
            Guid accessToken)
            => new(AuditServices(writer, options, session)) { AccessToken = accessToken };

        private static TestRpcRequest PingRequest() => new()
        {
            Method = $"{SysProgIds.System}.Ping",
            Params = new JsonRpcParams { Value = new Polhem.Api.Core.Messages.System.PingRequest { ClientName = "C", TraceId = "T" } },
            Id = "1",
        };

        /// <summary>
        /// Delays every business object creation, which happens after the pipeline starts timing the call, so a
        /// successful call reliably exceeds a small slow threshold without depending on how fast the machine is.
        /// </summary>
        private sealed class DelayingBusinessObjectFactory : IBusinessObjectFactory
        {
            private readonly IBusinessObjectFactory _inner;
            private readonly TimeSpan _delay;

            public DelayingBusinessObjectFactory(IBusinessObjectFactory inner, TimeSpan delay)
            {
                _inner = inner;
                _delay = delay;
            }

            public object CreateBusinessObject(Guid accessToken, string progId, bool isLocalCall)
            {
                Thread.Sleep(_delay);
                return _inner.CreateBusinessObject(accessToken, progId, isLocalCall);
            }
        }

        private static AuditLogOptions EnabledOptions(int slowThresholdMs = 3000) => new()
        {
            Enabled = true,
            AnomalyEnabled = true,
            ApiSlowThresholdMs = slowThresholdMs,
        };

        // ---- Anomaly records for failures ----

        [Fact]
        [DisplayName("A failed call with anomaly logging enabled writes an Error anomaly")]
        public async Task AnomalyEnabledFailure_WritesErrorAnomaly()
        {
            var writer = new CapturingAnomalyLogWriter();
            // A planted session instead of a bare `Guid`: a bare token makes token validation take the rebuild path
            // and read `st_session`, which would turn a test about the anomaly fields into one that needs a database container.
            var token = TestSessionFactory.CreateAccessToken(_fx);
            var dispatcher = NewAuditDispatcher(writer, EnabledOptions(), new StubSessionInfoService(NewSession()), token);

            var response = await dispatcher.ExecuteJsonAsync(UnreadablePing);

            Assert.NotNull(response.Error);
            var entry = Assert.Single(writer.Entries);
            var anomaly = Assert.IsType<ApiAnomalyEntry>(entry);
            Assert.Equal(AnomalyKind.Error, anomaly.Kind);
            Assert.Equal(nameof(InvalidParamsException), anomaly.ErrorType);
            Assert.Equal($"{SysProgIds.System}.Ping", anomaly.Method);
            Assert.NotNull(anomaly.ErrorMessage);
            Assert.Null(anomaly.ThresholdMs);
            // A non-empty access token is recorded as its fingerprint, never as the token itself.
            Assert.Equal(AccessTokenHasher.ComputeFingerprint(token), anomaly.TokenFingerprint);
            Assert.DoesNotContain(token.ToString("N"), anomaly.TokenFingerprint!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [DisplayName("A failure record with anomaly logging enabled and an empty AccessToken keeps no fingerprint")]
        public async Task AnomalyEnabledFailureEmptyToken_WritesNullFingerprint()
        {
            var writer = new CapturingAnomalyLogWriter();
            var dispatcher = NewAuditDispatcher(writer, EnabledOptions(), new StubSessionInfoService(NewSession()), Guid.Empty);

            var response = await dispatcher.ExecuteJsonAsync(UnreadablePing);

            Assert.NotNull(response.Error);
            var anomaly = Assert.IsType<ApiAnomalyEntry>(Assert.Single(writer.Entries));
            // An empty access token means "no session" and leaves the fingerprint empty.
            Assert.Null(anomaly.TokenFingerprint);
            Assert.Equal("u1", anomaly.UserId);
            Assert.Equal("C1", anomaly.CompanyId);
        }

        [Fact]
        [DisplayName("The anomaly record carries the caller's application identity (api_key_id / api_key_name)")]
        public async Task AnomalyEnabled_CarriesApiKeyIdentity()
        {
            var writer = new CapturingAnomalyLogWriter();
            var dispatcher = new TestDispatcher(AuditServices(writer, EnabledOptions(), new StubSessionInfoService(NewSession())))
            {
                IsLocalCall = false,
                ApiKeyValidator = new FixedApiKeyValidator(new ApiKeyValidationResult(
                    ApiKeyStatus.Valid, "northwind-desktop", "Northwind Desktop")),
            };

            await dispatcher.ExecuteJsonAsync(UnreadablePing);

            var anomaly = Assert.IsType<ApiAnomalyEntry>(Assert.Single(writer.Entries));
            // Pins the failure to the unreadable body, not to a session lookup that reached a database.
            Assert.Equal(nameof(InvalidParamsException), anomaly.ErrorType);
            Assert.Equal("northwind-desktop", anomaly.ApiKeyId);
            Assert.Equal("Northwind Desktop", anomaly.ApiKeyName);
        }

        [Fact]
        [DisplayName("For a call that did not pass the API key gate, the anomaly record's application identity is null")]
        public async Task AnomalyEnabledWithoutApiKey_LeavesIdentityNull()
        {
            var writer = new CapturingAnomalyLogWriter();
            var dispatcher = NewAuditDispatcher(writer, EnabledOptions(), new StubSessionInfoService(NewSession()),
                TestSessionFactory.CreateAccessToken(_fx));

            await dispatcher.ExecuteJsonAsync(UnreadablePing);

            var anomaly = Assert.IsType<ApiAnomalyEntry>(Assert.Single(writer.Entries));
            Assert.Equal(nameof(InvalidParamsException), anomaly.ErrorType);
            Assert.Null(anomaly.ApiKeyId);
            Assert.Null(anomaly.ApiKeyName);
        }

        // ---- Anomaly records for successful calls ----

        [Fact]
        [DisplayName("A successful call under the slow threshold with anomaly logging enabled writes no record")]
        public async Task AnomalyEnabledFastSuccess_WritesNoAnomaly()
        {
            var writer = new CapturingAnomalyLogWriter();
            var dispatcher = NewAuditDispatcher(writer, EnabledOptions(slowThresholdMs: 3000), new StubSessionInfoService(NewSession()), Guid.Empty);

            var response = await dispatcher.ExecuteAsync(PingRequest());

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
            Assert.Empty(writer.Entries);
        }

        [Fact]
        [DisplayName("A successful call that exceeds the slow threshold writes exactly one Slow anomaly carrying the threshold and elapsed time")]
        public async Task AnomalyEnabledSlowSuccess_WritesSlowAnomaly()
        {
            const int thresholdMs = 10;
            var writer = new CapturingAnomalyLogWriter();
            var dispatcher = new TestDispatcher(
                AuditServices(writer, EnabledOptions(slowThresholdMs: thresholdMs), new StubSessionInfoService(NewSession())),
                new DelayingBusinessObjectFactory(BoFactory, TimeSpan.FromMilliseconds(thresholdMs * 5)));

            var response = await dispatcher.ExecuteAsync(PingRequest());

            Assert.Null(response.Error);
            var anomaly = Assert.IsType<ApiAnomalyEntry>(Assert.Single(writer.Entries));
            Assert.Equal(AnomalyKind.Slow, anomaly.Kind);
            Assert.Equal(thresholdMs, anomaly.ThresholdMs);
            Assert.True(anomaly.ElapsedMs > thresholdMs, $"ElapsedMs {anomaly.ElapsedMs} should exceed {thresholdMs}.");
            Assert.Equal($"{SysProgIds.System}.Ping", anomaly.Method);
            Assert.Null(anomaly.ErrorType);
            Assert.Equal("u1", anomaly.UserId);
        }

        // ---- AnomalyEnabled combinations ----

        [Fact]
        [DisplayName("A failure writes no record when a writer exists but auditOptions is disabled")]
        public async Task AuditOptionsDisabled_WritesNoAnomaly()
        {
            var writer = new CapturingAnomalyLogWriter();
            var options = new AuditLogOptions { Enabled = false, AnomalyEnabled = true };
            var dispatcher = NewAuditDispatcher(writer, options, new StubSessionInfoService(NewSession()), Guid.Empty);

            var response = await dispatcher.ExecuteJsonAsync(UnreadablePing);

            Assert.NotNull(response.Error);
            Assert.Empty(writer.Entries);
        }

        [Fact]
        [DisplayName("A failure writes no record when a writer exists but AnomalyEnabled is false")]
        public async Task AnomalyFlagDisabled_WritesNoAnomaly()
        {
            var writer = new CapturingAnomalyLogWriter();
            var options = new AuditLogOptions { Enabled = true, AnomalyEnabled = false };
            var dispatcher = NewAuditDispatcher(writer, options, new StubSessionInfoService(NewSession()), Guid.Empty);

            var response = await dispatcher.ExecuteJsonAsync(UnreadablePing);

            Assert.NotNull(response.Error);
            Assert.Empty(writer.Entries);
        }

        [Fact]
        [DisplayName("A failure writes no record when a writer exists but auditOptions is null")]
        public async Task AuditOptionsNull_WritesNoAnomaly()
        {
            var writer = new CapturingAnomalyLogWriter();
            var dispatcher = NewAuditDispatcher(writer, options: null, session: new StubSessionInfoService(NewSession()), accessToken: Guid.Empty);

            var response = await dispatcher.ExecuteJsonAsync(UnreadablePing);

            Assert.NotNull(response.Error);
            Assert.Empty(writer.Entries);
        }

        [Fact]
        [DisplayName("A failure writes no record when a writer and enabled options exist but sessionService is null")]
        public async Task SessionServiceNull_WritesNoAnomaly()
        {
            var writer = new CapturingAnomalyLogWriter();
            var dispatcher = NewAuditDispatcher(writer, EnabledOptions(), session: null, accessToken: Guid.Empty);

            var response = await dispatcher.ExecuteJsonAsync(UnreadablePing);

            Assert.NotNull(response.Error);
            Assert.Empty(writer.Entries);
        }

        // ---- Encryption key branch ----

        [Fact]
        [DisplayName("A remote call in Encrypted format enters the encryption key branch")]
        public async Task EncryptedFormatRemoteCall_HitsEncryptionKeyBranch()
        {
            // Ping is Public/Anonymous, so an Encrypted request passes access validation and fetches the encryption key.
            // Decrypting the unencrypted payload then fails and an error is returned. The point is to cover the
            // Encrypted branch.
            var request = new TestRpcRequest
            {
                Method = $"{SysProgIds.System}.Ping",
                Params = new JsonRpcParams { Format = PayloadFormat.Encrypted, Value = new Polhem.Api.Core.Messages.System.PingRequest { ClientName = "C", TraceId = "T" } },
                Id = "1",
            };

            var response = await new TestDispatcher(_fx.Provider) { IsLocalCall = false }.ExecuteAsync(request);

            Assert.NotNull(response.Error);
        }
    }
}
