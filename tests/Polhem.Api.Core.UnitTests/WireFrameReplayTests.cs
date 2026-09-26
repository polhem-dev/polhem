using System.ComponentModel;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages;
using Polhem.Api.Core.Messages.System;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Logging;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;
using Polhem.Tests.Shared;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Behavior tests for the replay protection frame going through the full payload pipeline.
    /// </summary>
    /// <remarks>
    /// These tests change the process-wide static switch <see cref="ApiServiceOptions.RequireWireFrame"/>,
    /// so the class carries the ApiServiceOptionsState collection marker and always restores it with try/finally.
    /// <para>
    /// This class **never touches the database**. Tests that need a token get it from
    /// <see cref="TestSessionFactory.CreateAccessToken"/>, which writes the SessionInfo straight into the
    /// session cache, so the server finds it without taking the rebuild path that queries <c>st_session</c>.
    /// What is verified here is the replay sequence check (<c>ReplayWindowStore</c>, in memory only), which does not
    /// depend on where the session comes from.
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
    [Collection("ApiServiceOptionsState")]
    public class WireFrameReplayTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public WireFrameReplayTests(PolhemTestFixture fx)
        {
            _fx = fx;
        }

        private static byte[] MakeKey()
        {
            var key = new byte[64];
            for (int i = 0; i < key.Length; i++) key[i] = (byte)i;
            return key;
        }

        private static void WithFrameRequired(bool value, Action action)
        {
            bool original = ApiServiceOptions.RequireWireFrame;
            ApiServiceOptions.RequireWireFrame = value;
            try { action(); }
            finally { ApiServiceOptions.RequireWireFrame = original; }
        }

        [Fact]
        [DisplayName("Encrypted round-trip produces no frame when the switch is off")]
        public void RestoreFrom_FrameNotRequired_LeavesFrameNull()
        {
            WithFrameRequired(false, () =>
            {
                var key = MakeKey();
                var payload = new JsonRpcParams { Value = new PingRequest { ClientName = "a" } };

                ApiPayloadConverter.TransformTo(payload, PayloadFormat.Encrypted, key);
                ApiPayloadConverter.RestoreFrom(payload, PayloadFormat.Encrypted, key);

                Assert.Null(payload.Frame);
                Assert.IsType<PingRequest>(payload.Value);
            });
        }

        [Fact]
        [DisplayName("Encrypted round-trip restores the frame and the body when the switch is on")]
        public void RestoreFrom_FrameRequired_RoundTripsFrameAndBody()
        {
            WithFrameRequired(true, () =>
            {
                var key = MakeKey();
                var payload = new JsonRpcParams { Value = new PingRequest { ClientName = "a" } };

                ApiPayloadConverter.TransformTo(payload, PayloadFormat.Encrypted, key);
                ApiPayloadConverter.RestoreFrom(payload, PayloadFormat.Encrypted, key);

                Assert.NotNull(payload.Frame);
                Assert.Equal(ApiPayloadFrame.CurrentVersion, payload.Frame!.Version);
                Assert.Equal("a", Assert.IsType<PingRequest>(payload.Value).ClientName);
            });
        }

        [Fact]
        [DisplayName("Plain format carries no frame even when the switch is on")]
        public void TransformTo_PlainWithFrameRequired_WritesNoFrame()
        {
            // Plain has no envelope, so a frame would be plaintext that an attacker can rewrite freely and would protect nothing.
            WithFrameRequired(true, () =>
            {
                var payload = new JsonRpcParams { Value = "hello" };

                ApiPayloadConverter.TransformTo(payload, PayloadFormat.Plain);

                Assert.Null(payload.Frame);
                Assert.Equal("hello", payload.Value);
            });
        }

        [Fact]
        [DisplayName("Decoding fails when the writer adds a frame the reader does not expect (both ends must agree)")]
        public void RestoreFrom_FrameWrittenButNotExpected_FailsToDecode()
        {
            // Whether a frame is present is not declared by the packet itself (that would be a downgrade attack
            // surface), so mismatched settings on the two ends fail. This is expected, and it is why an upgrade
            // deploys both ends before turning the switch on.
            var key = MakeKey();
            var payload = new JsonRpcParams { Value = new PingRequest { ClientName = "a" } };

            bool original = ApiServiceOptions.RequireWireFrame;
            try
            {
                ApiServiceOptions.RequireWireFrame = true;
                ApiPayloadConverter.TransformTo(payload, PayloadFormat.Encrypted, key);

                ApiServiceOptions.RequireWireFrame = false;
                Assert.Throws<InvalidOperationException>(() =>
                    ApiPayloadConverter.RestoreFrom(payload, PayloadFormat.Encrypted, key));
            }
            finally
            {
                ApiServiceOptions.RequireWireFrame = original;
            }
        }

        [Fact]
        [DisplayName("A frame timestamp outside the allowed window returns ReplayRejected")]
        public void Execute_FrameTimestampOutsideWindow_ReturnsReplayRejected()
        {
            WithFrameRequired(true, () =>
            {
                var staleMs = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds();

                var response = ExecutePing(new ApiPayloadFrame(staleMs, sequence: 0));

                Assert.NotNull(response.Error);
                Assert.Equal((int)JsonRpcErrorCode.ReplayRejected, response.Error!.Code);
            });
        }

        [Fact]
        [DisplayName("A frame timestamp within the allowed window executes normally")]
        public void Execute_FrameTimestampWithinWindow_Succeeds()
        {
            WithFrameRequired(true, () =>
            {
                var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                var response = ExecutePing(new ApiPayloadFrame(nowMs, sequence: 0));

                Assert.Null(response.Error);
            });
        }

        [Fact]
        [DisplayName("A repeated sequence on a method that declares UniqueSequence returns ReplayRejected")]
        public void Execute_RepeatedSequenceOnGuardedMethod_ReturnsReplayRejected()
        {
            WithFrameRequired(true, () =>
            {
                // Each test uses its own token so that its window does not interfere with other tests.
                var token = TestSessionFactory.CreateAccessToken(_fx);

                var first = Execute("ExecFunc", new ExecFuncRequest("noop"), FrameWith(1), token);
                var replay = Execute("ExecFunc", new ExecFuncRequest("noop"), FrameWith(1), token);
                var nextSequence = Execute("ExecFunc", new ExecFuncRequest("noop"), FrameWith(2), token);

                // The first call goes all the way into the BO (the custom method "noop" does not exist, hence
                // InternalError). What matters is that it is not ReplayRejected, meaning the sequence check let it through.
                Assert.Equal((int)JsonRpcErrorCode.InternalError, first.Error!.Code);
                Assert.Equal((int)JsonRpcErrorCode.ReplayRejected, replay.Error!.Code);
                // A different sequence still passes, which shows the rejection targets repeats rather than blocking everything.
                Assert.Equal((int)JsonRpcErrorCode.InternalError, nextSequence.Error!.Code);
            });
        }

        [Fact]
        [DisplayName("A repeated sequence on a method without a sequence check executes normally")]
        public void Execute_RepeatedSequenceOnUnguardedMethod_Succeeds()
        {
            // Replaying a query method is harmless, and applying the check everywhere would only add work to every call.
            WithFrameRequired(true, () =>
            {
                var token = TestSessionFactory.CreateAccessToken(_fx);
                var value = new PingRequest { ClientName = "replay-test" };

                Assert.Null(Execute("Ping", value, FrameWith(1), token).Error);
                Assert.Null(Execute("Ping", value, FrameWith(1), token).Error);
            });
        }

        [Fact]
        [DisplayName("Anonymous calls skip the sequence check (there is no session to count against)")]
        public void Execute_RepeatedSequenceAnonymously_Succeeds()
        {
            // Sequences are per session. Anonymous calls all share `Guid.Empty`, so checking them would let
            // different clients use up each other's sequences and cause many false rejections.
            WithFrameRequired(true, () =>
            {
                var first = Execute("ExecFunc", new ExecFuncRequest("noop"), FrameWith(1), Guid.Empty);
                var replay = Execute("ExecFunc", new ExecFuncRequest("noop"), FrameWith(1), Guid.Empty);

                // As above, InternalError means both calls passed the sequence gate and reached the BO.
                Assert.Equal((int)JsonRpcErrorCode.InternalError, first.Error!.Code);
                Assert.Equal((int)JsonRpcErrorCode.InternalError, replay.Error!.Code);
            });
        }

        [Fact]
        [DisplayName("A replay rejection is logged as AnomalyKind.Replay rather than Error")]
        public void Execute_ReplayRejected_IsLoggedAsReplayAnomaly()
        {
            // Folded into the generic Error kind, the signal "one session is rejected repeatedly" would disappear,
            // and that signal is exactly how client clock skew or resent packets are told apart.
            WithFrameRequired(true, () =>
            {
                var staleMs = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds();

                var entries = ExecuteAndCaptureAnomalies(new ApiPayloadFrame(staleMs, sequence: 0));

                var entry = Assert.IsType<ApiAnomalyEntry>(Assert.Single(entries));
                Assert.Equal(AnomalyKind.Replay, entry.Kind);
            });
        }

        /// <summary>
        /// Sends one Encoded Ping call with the given frame (Ping declares no sequence check).
        /// </summary>
        /// <param name="frame">The replay protection frame to attach.</param>
        private JsonRpcResponse ExecutePing(ApiPayloadFrame frame)
            => Execute("Ping", new PingRequest { ClientName = "replay-test" }, frame, Guid.Empty);

        /// <summary>
        /// Sends one Encoded SystemBO call with the given frame and token.
        /// </summary>
        /// <param name="action">The action name.</param>
        /// <param name="value">The value passed in.</param>
        /// <param name="frame">The replay protection frame to attach.</param>
        /// <param name="accessToken">The access token; <see cref="Guid.Empty"/> means an anonymous call.</param>
        private JsonRpcResponse Execute(string action, object value, ApiPayloadFrame frame, Guid accessToken)
        {
            var executor = new JsonRpcExecutor(
                _fx.GetRequiredService<IBusinessObjectFactory>(),
                _fx.GetRequiredService<IAccessTokenValidator>(),
                _fx.GetRequiredService<IApiEncryptionKeyProvider>())
            {
                AccessToken = accessToken,
                // A local call skips token validation, so the test need not create a session first (that would
                // touch the database). The sequence check does not depend on this; it only looks at whether the token is empty.
                IsLocalCall = true,
            };

            var request = new JsonRpcRequest
            {
                Method = $"{SysProgIds.System}.{action}",
                Params = new JsonRpcParams { Value = value, Frame = frame },
                Id = Guid.NewGuid().ToString(),
            };

            // Encoded rather than Encrypted: reading the frame is unrelated to encryption, and Encoded needs no transport key.
            ApiPayloadConverter.TransformTo(request.Params, PayloadFormat.Encoded);

            return executor.Execute(request);
        }

        private static ApiPayloadFrame FrameWith(long sequence)
            => new(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), sequence);

        /// <summary>
        /// Sends one call through an executor with anomaly logging enabled and returns the captured anomaly entries.
        /// </summary>
        /// <param name="frame">The replay protection frame to attach.</param>
        private List<AnomalyEntry> ExecuteAndCaptureAnomalies(ApiPayloadFrame frame)
        {
            var writer = new CapturingAnomalyLogWriter();
            var executor = new JsonRpcExecutor(
                _fx.GetRequiredService<IBusinessObjectFactory>(),
                _fx.GetRequiredService<IAccessTokenValidator>(),
                _fx.GetRequiredService<IApiEncryptionKeyProvider>(),
                writer,
                new AuditLogOptions { Enabled = true, AnomalyEnabled = true, ApiSlowThresholdMs = 60_000 },
                new StubSessionInfoService())
            {
                AccessToken = Guid.Empty,
                IsLocalCall = true,
            };

            var request = new JsonRpcRequest
            {
                Method = $"{SysProgIds.System}.Ping",
                Params = new JsonRpcParams
                {
                    Value = new PingRequest { ClientName = "replay-test" },
                    Frame = frame,
                },
                Id = Guid.NewGuid().ToString(),
            };
            ApiPayloadConverter.TransformTo(request.Params, PayloadFormat.Encoded);
            executor.Execute(request);

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
