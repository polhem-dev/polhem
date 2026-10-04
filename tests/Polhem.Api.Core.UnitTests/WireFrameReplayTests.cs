using System.ComponentModel;
using System.Text.Json;
using Polhem.Api.Core.Dispatch;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages;
using Polhem.Api.Core.Messages.System;
using Polhem.Api.Core.Transformers;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Logging;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;
using Polhem.Api.Core.UnitTests.Dispatch;
using Polhem.JsonRpc.Payload;
using Polhem.Tests.Shared;
using PayloadFormat = Polhem.JsonRpc.Payload.PayloadFormat;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Behavior tests for the replay protection frame going through the full payload pipeline.
    /// </summary>
    /// <remarks>
    /// The frame setting is <see cref="PayloadOptions.RequireFrame"/> on the options the server resolves from its
    /// services; each test hands the dispatcher its own options, so nothing process-wide changes.
    /// <para>
    /// This class **never touches the database**. Tests that need a token get it from
    /// <see cref="TestSessionFactory.CreateAccessToken"/>, which writes the SessionInfo straight into the
    /// session cache, so the server finds it without taking the rebuild path that queries <c>st_session</c>.
    /// What is verified here is the replay sequence check (the replay store, in memory only), which does not
    /// depend on where the session comes from. The sequence tests call over HTTP, because only a remote call has a
    /// replay scope; <see cref="TestDispatcher"/> registers no API key validator for them, so the key gate does not
    /// read <c>st_api_key</c> either.
    /// </para>
    /// <para>
    /// NOTE: Two tests used to pass <c>Guid.NewGuid()</c> as the token, so every call fell into the rebuild path,
    /// and a set of pure logic tests came to need a database container. Without a container they went red with
    /// <c>Connection string for database 'common' is null</c>, not for any reason related to replay.
    /// The fix at the time was to attach <see cref="SharedDbFixture"/> so there was a table to read; the right fix
    /// is not to create that dependency at all. Item 2 of <c>rules/testing.md</c> says the same: pure logic tests
    /// must not be skipped with <c>[DbFact]</c>, and such a red light is to be fixed directly.
    /// </para>
    /// </remarks>
    public class WireFrameReplayTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public WireFrameReplayTests(PolhemTestFixture fx)
        {
            _fx = fx;
        }

        private static readonly string s_pingMethod = $"{SysProgIds.System}.Ping";

        private static byte[] MakeKey()
        {
            var key = new byte[64];
            for (int i = 0; i < key.Length; i++) key[i] = (byte)i;
            return key;
        }

        private static PayloadOptions Options(bool requireFrame)
        {
            var options = PolhemPayload.CreateOptions();
            options.RequireFrame = requireFrame;
            return options;
        }

        [Fact]
        [DisplayName("Encrypted round-trip produces no frame when the switch is off")]
        public void Open_FrameNotRequired_LeavesFrameNull()
        {
            var key = MakeKey();
            var processor = new PayloadProcessor(Options(requireFrame: false));

            var envelope = processor.SealResponse(s_pingMethod, new PingRequest { ClientName = "a" }, PayloadFormat.Encrypted, key: key);
            var value = processor.OpenResult(envelope, key, s_pingMethod, out var frame);

            Assert.Null(frame);
            Assert.IsType<PingRequest>(value);
        }

        [Fact]
        [DisplayName("Encrypted round-trip restores the frame and the body when the switch is on")]
        public void Open_FrameRequired_RoundTripsFrameAndBody()
        {
            var key = MakeKey();
            var processor = new PayloadProcessor(Options(requireFrame: true));

            var envelope = processor.SealResponse(s_pingMethod, new PingRequest { ClientName = "a" }, PayloadFormat.Encrypted, key: key);
            var value = processor.OpenResult(envelope, key, s_pingMethod, out var frame);

            Assert.NotNull(frame);
            Assert.Equal(PayloadFrame.CurrentVersion, frame!.Version);
            Assert.Equal("a", Assert.IsType<PingRequest>(value).ClientName);
        }

        [Fact]
        [DisplayName("Plain format carries no frame even when the switch is on")]
        public void Seal_PlainWithFrameRequired_WritesNoFrame()
        {
            // Plain has no envelope body, so a frame would be plaintext that an attacker can rewrite freely and would
            // protect nothing.
            var envelope = new PayloadProcessor(Options(requireFrame: true)).Seal("hello", PayloadFormat.Plain);

            Assert.Null(envelope.Body);
            Assert.Equal("hello", envelope.Value!.Value.GetString());
        }

        [Fact]
        [DisplayName("Decoding fails when the writer adds a frame the reader does not expect (both ends must agree)")]
        public void Open_FrameWrittenButNotExpected_FailsToDecode()
        {
            // Whether a frame is present is not declared by the packet itself (that would be a downgrade attack
            // surface), so mismatched settings on the two ends fail. This is expected, and it is why an upgrade
            // deploys both ends before turning the switch on.
            var key = MakeKey();
            var envelope = new PayloadProcessor(Options(requireFrame: true))
                .SealResponse(s_pingMethod, new PingRequest { ClientName = "a" }, PayloadFormat.Encrypted, key: key);

            Assert.Throws<InvalidOperationException>(() =>
                new PayloadProcessor(Options(requireFrame: false)).OpenResult(envelope, key, s_pingMethod, out _));
        }

        [Fact]
        [DisplayName("A frame timestamp outside the allowed window returns ReplayRejected")]
        public async Task Execute_FrameTimestampOutsideWindow_ReturnsReplayRejected()
        {
            {
                var staleMs = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds();

                var response = await ExecutePing(new PayloadFrame(staleMs, sequence: 0));

                Assert.NotNull(response.Error);
                Assert.Equal((int)JsonRpcErrorCode.ReplayRejected, response.Error!.Code);
            }
        }

        [Fact]
        [DisplayName("A frame timestamp within the allowed window executes normally")]
        public async Task Execute_FrameTimestampWithinWindow_Succeeds()
        {
            {
                var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                var response = await ExecutePing(new PayloadFrame(nowMs, sequence: 0));

                Assert.Null(response.Error);
            }
        }

        [Fact]
        [DisplayName("A repeated sequence on a method that declares UniqueSequence returns ReplayRejected")]
        public async Task Execute_RepeatedSequenceOnGuardedMethod_ReturnsReplayRejected()
        {
            {
                // Each test uses its own token so that its window does not interfere with other tests.
                var token = TestSessionFactory.CreateAccessToken(_fx);

                var first = await ExecuteEncrypted("ExecFunc", new ExecFuncRequest("noop"), 1, token);
                var replay = await ExecuteEncrypted("ExecFunc", new ExecFuncRequest("noop"), 1, token);
                var nextSequence = await ExecuteEncrypted("ExecFunc", new ExecFuncRequest("noop"), 2, token);

                // The first call goes all the way into the BO (the custom method "noop" does not exist, hence
                // InternalError). What matters is that it is not ReplayRejected, meaning the sequence check let it through.
                Assert.Equal((int)JsonRpcErrorCode.InternalError, first.Error!.Code);
                Assert.Equal((int)JsonRpcErrorCode.ReplayRejected, replay.Error!.Code);
                // A different sequence still passes, which shows the rejection targets repeats rather than blocking everything.
                Assert.Equal((int)JsonRpcErrorCode.InternalError, nextSequence.Error!.Code);
            }
        }

        [Fact]
        [DisplayName("A repeated sequence on a method without a sequence check executes normally")]
        public async Task Execute_RepeatedSequenceOnUnguardedMethod_Succeeds()
        {
            // Replaying a query method is harmless, and applying the check everywhere would only add work to every call.
            {
                var token = TestSessionFactory.CreateAccessToken(_fx);
                var value = new PingRequest { ClientName = "replay-test" };

                Assert.Null((await ExecuteEncrypted("Ping", value, 1, token)).Error);
                Assert.Null((await ExecuteEncrypted("Ping", value, 1, token)).Error);
            }
        }

        [Fact]
        [DisplayName("An encoded remote call to a method that declares UniqueSequence returns InvalidParams when frames are on")]
        public async Task Execute_EncodedRemoteCallOnGuardedMethod_ReturnsInvalidParams()
        {
            // Only an encrypted frame cannot be rewritten by whoever captured the call, so a guarded method refuses an
            // encoded one outright. A session without an encryption key falls into this case.
            var token = TestSessionFactory.CreateAccessToken(_fx);

            var response = await Execute("ExecFunc", new ExecFuncRequest("noop"), FrameWith(1), token, isLocalCall: false);

            Assert.Equal((int)JsonRpcErrorCode.InvalidParams, response.Error!.Code);
        }

        [Fact]
        [DisplayName("A plain local call to a method that declares UniqueSequence executes when frames are on")]
        public async Task Execute_PlainLocalCallOnGuardedMethod_Succeeds()
        {
            // The local provider sends Plain outside debug mode. A local call never crossed a network, so there is
            // nothing to replay, and the policy gives it no replay scope.
            var token = TestSessionFactory.CreateAccessToken(_fx);
            var executor = new TestDispatcher(WithFrames(_fx.Provider)) { AccessToken = token };
            var request = new TestRpcRequest
            {
                Method = $"{SysProgIds.System}.ExecFunc",
                Params = new TestPayload { Format = Polhem.Api.Core.Messages.PayloadFormat.Plain, Value = new ExecFuncRequest("noop") },
            };

            var first = await executor.ExecuteAsync(request);
            var second = await executor.ExecuteAsync(request);

            // InternalError means the call reached the BO, past the payload filter.
            Assert.Equal((int)JsonRpcErrorCode.InternalError, first.Error!.Code);
            Assert.Equal((int)JsonRpcErrorCode.InternalError, second.Error!.Code);
        }

        [Fact]
        [DisplayName("A replay rejection is logged as AnomalyKind.Replay rather than Error")]
        public async Task Execute_ReplayRejected_IsLoggedAsReplayAnomaly()
        {
            // Folded into the generic Error kind, the signal "one session is rejected repeatedly" would disappear,
            // and that signal is exactly how client clock skew or resent packets are told apart.
            {
                var staleMs = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds();

                var entries = await ExecuteAndCaptureAnomalies(new PayloadFrame(staleMs, sequence: 0));

                var entry = Assert.IsType<ApiAnomalyEntry>(Assert.Single(entries));
                Assert.Equal(AnomalyKind.Replay, entry.Kind);
            }
        }

        /// <summary>
        /// Sends one Encoded Ping call with the given frame (Ping declares no sequence check).
        /// </summary>
        /// <param name="frame">The replay protection frame to attach.</param>
        private Task<TestRpcResponse> ExecutePing(PayloadFrame frame)
            => Execute("Ping", new PingRequest { ClientName = "replay-test" }, frame, Guid.Empty);

        /// <summary>
        /// Sends one Encoded SystemBO call with the given frame and token, to a server that requires frames.
        /// </summary>
        /// <param name="action">The action name.</param>
        /// <param name="value">The value passed in.</param>
        /// <param name="frame">The replay protection frame to attach.</param>
        /// <param name="accessToken">The access token; <see cref="Guid.Empty"/> means an anonymous call.</param>
        /// <param name="isLocalCall">Whether the call is in-process; otherwise it arrives over HTTP.</param>
        private async Task<TestRpcResponse> Execute(string action, object value, PayloadFrame frame, Guid accessToken, bool isLocalCall = true)
        {
            var executor = new TestDispatcher(WithFrames(_fx.Provider))
            {
                AccessToken = accessToken,
                IsLocalCall = isLocalCall,
            };

            var request = new TestRpcRequest
            {
                Method = $"{SysProgIds.System}.{action}",
                RawParams = EncodedWithFrame(value, frame),
                Id = Guid.NewGuid().ToString(),
            };

            return await executor.ExecuteAsync(request);
        }

        /// <summary>
        /// Sends one Encrypted SystemBO call over HTTP with the given sequence number and token, to a server that
        /// requires frames. Only a remote call with a session has a replay scope, and only an encrypted one may reach
        /// a guarded method.
        /// </summary>
        /// <param name="action">The action name.</param>
        /// <param name="value">The value passed in.</param>
        /// <param name="sequence">The sequence number written to the frame, which carries the current time.</param>
        /// <param name="accessToken">The access token of a session planted in the cache.</param>
        private async Task<TestRpcResponse> ExecuteEncrypted(string action, object value, long sequence, Guid accessToken)
        {
            var executor = new TestDispatcher(WithFrames(_fx.Provider)) { AccessToken = accessToken, IsLocalCall = false };
            var request = new TestRpcRequest
            {
                Method = $"{SysProgIds.System}.{action}",
                Params = new TestPayload
                {
                    Format = Polhem.Api.Core.Messages.PayloadFormat.Encrypted,
                    Value = value,
                    // The key the server's payload policy looks up for this token.
                    Key = _fx.GetRequiredService<IApiEncryptionKeyProvider>().GetKey(accessToken),
                    Sequence = sequence,
                },
                Id = Guid.NewGuid().ToString(),
            };

            return await executor.ExecuteAsync(request);
        }

        private static PayloadFrame FrameWith(long sequence)
            => new(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), sequence);

        /// <summary>The services, with payload options that require a frame.</summary>
        private static TestOverrideServiceProvider WithFrames(IServiceProvider services)
            => new TestOverrideServiceProvider(services, (typeof(PayloadOptions), Options(requireFrame: true)));

        /// <summary>
        /// Builds an Encoded envelope carrying <paramref name="frame"/> as it is, which the processor cannot do because
        /// it always stamps the current time. Encoded rather than Encrypted: reading the frame is unrelated to
        /// encryption, and Encoded needs no transport key.
        /// </summary>
        private static JsonElement EncodedWithFrame(object value, PayloadFrame frame)
        {
            var options = PolhemPayload.CreateOptions();
            var body = options.Compressor.Compress(options.ResolveCodec(null).Serialize(value, value.GetType()));
            return new PayloadEnvelope
            {
                Format = PayloadFormat.Encoded,
                Body = frame.Prepend(body),
                TypeName = PolhemPayloadTypeResolver.Instance.GetTypeName(value.GetType()),
            }.ToElement();
        }

        /// <summary>
        /// Sends one call with anomaly logging enabled and returns the captured anomaly entries.
        /// </summary>
        /// <param name="frame">The replay protection frame to attach.</param>
        private async Task<List<AnomalyEntry>> ExecuteAndCaptureAnomalies(PayloadFrame frame)
        {
            var writer = new CapturingAnomalyLogWriter();
            var services = new TestOverrideServiceProvider(WithFrames(_fx.Provider),
                (typeof(IAnomalyLogWriter), writer),
                (typeof(AuditLogOptions), new AuditLogOptions { Enabled = true, AnomalyEnabled = true, ApiSlowThresholdMs = 60_000 }),
                (typeof(ISessionInfoService), new StubSessionInfoService()));
            var executor = new TestDispatcher(services);

            var request = new TestRpcRequest
            {
                Method = $"{SysProgIds.System}.Ping",
                RawParams = EncodedWithFrame(new PingRequest { ClientName = "replay-test" }, frame),
                Id = Guid.NewGuid().ToString(),
            };
            await executor.ExecuteAsync(request);

            return writer.Entries;
        }

        private sealed class CapturingAnomalyLogWriter : IAnomalyLogWriter
        {
            public List<AnomalyEntry> Entries { get; } = [];

            public void Write(AnomalyEntry entry) => Entries.Add(entry);
        }

        private sealed class StubSessionInfoService : ISessionInfoService
        {
            public SessionInfo Get(Guid accessToken) => new() { UserId = "u1", UserName = "User One" };

            public void Set(SessionInfo sessionInfo) { }

            public void Remove(Guid accessToken) { }
        }
    }
}
