using System.ComponentModel;
using System.Text.Json;
using Polhem.Api.Core.Dispatch;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages;
using Polhem.Api.Core.Messages.System;
using Polhem.Definition;
using Polhem.Definition.Security;
using Polhem.JsonRpc.Server;
using Polhem.Tests.Shared;

namespace Polhem.Api.Core.UnitTests.Dispatch
{
    /// <summary>
    /// The checks that ran in the controller before the executor, now run by <see cref="PolhemObjectFactory"/> for a
    /// call that arrives over HTTP: the API key gate and the <c>Authorization</c> header. A call they refuse is
    /// answered with <see cref="JsonRpcErrorCode.InvalidRequest"/> and the validator's message; the controller sent the
    /// same error with HTTP 401.
    /// </summary>
    public class HttpAuthorizationTests : IClassFixture<SharedDbFixture>
    {
        private const string ApiKeyRejected = "Missing or invalid API key.";
        private const string LocalOnlyRefusal = "restricted to local calls";

        private readonly SharedDbFixture _fx;

        public HttpAuthorizationTests(SharedDbFixture fx) { _fx = fx; }

        private sealed class ThrowingApiKeyValidator : IApiKeyValidator
        {
            public ApiKeyValidationResult Validate(string? apiKey) => throw new InvalidOperationException("store unreachable");
        }

        private sealed class FixedApiKeyValidator(ApiKeyValidationResult result) : IApiKeyValidator
        {
            public ApiKeyValidationResult Validate(string? apiKey) => result;
        }

        /// <summary>
        /// Posts a Ping body to <paramref name="method"/> over HTTP. The <see cref="IApiKeyValidator"/> override is
        /// registered even when null, which is the only way to reach the path of a host without a validator:
        /// AddPolhemFramework always registers one.
        /// </summary>
        private async Task<JsonElement> PostAsync(string method, IApiKeyValidator? validator, string? apiKey, string? authorization = "session")
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (apiKey != null) { headers[ApiHeaders.ApiKey] = apiKey; }
            if (authorization == "session") { authorization = "Bearer " + TestSessionFactory.CreateAccessToken(_fx); }
            if (authorization != null) { headers[ApiHeaders.Authorization] = authorization; }

            var services = new TestOverrideServiceProvider(_fx.Provider, (typeof(IApiKeyValidator), validator));
            var transport = new JsonRpcTransportInfo(JsonRpcTransportKind.Http, services, headers, "127.0.0.1");
            var request = new TestRpcRequest
            {
                Method = method,
                Params = new TestPayload { Value = new PingRequest { ClientName = "unit", TraceId = "T-1" } },
                Id = Guid.NewGuid().ToString(),
            };

            var result = await new JsonRpcDispatcher(PolhemJsonRpc.CreateServerOptions())
                .DispatchMessageAsync(TestDispatcher.Serialize(request), transport);
            using var document = JsonDocument.Parse(result.Serialize()!);
            return document.RootElement.Clone();
        }

        private static void AssertRefusedByGate(JsonElement answer, string message)
        {
            var error = answer.GetProperty("error");
            Assert.Equal((int)JsonRpcErrorCode.InvalidRequest, error.GetProperty("code").GetInt32());
            Assert.Equal(message, error.GetProperty("message").GetString());
        }

        [Fact]
        [DisplayName("A throwing key validator fails closed instead of degrading to the lenient mode")]
        public async Task ValidatorThrows_FailsClosed()
        {
            var answer = await PostAsync($"{SysProgIds.System}.GetCommonConfiguration", new ThrowingApiKeyValidator(), "any-key");

            AssertRefusedByGate(answer, ApiKeyRejected);
        }

        [Fact]
        [DisplayName("System.Ping still answers when the key validator throws (health checks need no key)")]
        public async Task ValidatorThrows_PingStillAnswers()
        {
            var answer = await PostAsync($"{SysProgIds.System}.Ping", new ThrowingApiKeyValidator(), "any-key");

            Assert.True(answer.TryGetProperty("result", out _));
        }

        [Fact]
        [DisplayName("In strict mode an invalid key is refused with a message that does not reveal the rejection reason")]
        public async Task InvalidKey_RefusedWithMergedMessage()
        {
            var validator = new FixedApiKeyValidator(new ApiKeyValidationResult(ApiKeyStatus.Invalid, "some-app", string.Empty));

            var answer = await PostAsync($"{SysProgIds.System}.GetCommonConfiguration", validator, "some-app.bad");

            AssertRefusedByGate(answer, ApiKeyRejected);
        }

        [Fact]
        [DisplayName("A host without a registered IApiKeyValidator keeps the non-empty check (compatibility mode)")]
        public async Task NoValidatorRegistered_UsesPresenceCheck()
        {
            var answer = await PostAsync($"{SysProgIds.System}.GetCommonConfiguration", validator: null, apiKey: "any-key");

            Assert.True(answer.TryGetProperty("result", out _), answer.GetRawText());
        }

        [Fact]
        [DisplayName("Without an API key a call that needs one is refused")]
        public async Task MissingApiKey_Refused()
        {
            var answer = await PostAsync($"{SysProgIds.System}.GetCommonConfiguration", validator: null, apiKey: null);

            AssertRefusedByGate(answer, ApiKeyRejected);
        }

        [Fact]
        [DisplayName("In strict mode Ping with an invalid key reports Invalid and omits the version")]
        public async Task Ping_InvalidKey_ReportsStatusWithoutVersion()
        {
            var validator = new FixedApiKeyValidator(new ApiKeyValidationResult(ApiKeyStatus.Invalid, "some-app", string.Empty));

            var answer = await PostAsync($"{SysProgIds.System}.Ping", validator, "some-app.bad");

            var value = answer.GetProperty("result").GetProperty("value");
            Assert.Equal("ok", value.GetProperty("status").GetString());
            Assert.Equal(nameof(ApiKeyStatus.Invalid), value.GetProperty("apiKeyStatus").GetString());
            Assert.False(value.TryGetProperty("version", out _));
        }

        [Fact]
        [DisplayName("A call without an Authorization header is not refused by the gate; the method's access check decides")]
        public async Task NoAuthorization_NotRefusedByTheGate()
        {
            var answer = await PostAsync($"{SysProgIds.System}.ExecFunc", validator: null, apiKey: "valid-api-key", authorization: null);

            var error = answer.GetProperty("error");
            Assert.NotEqual((int)JsonRpcErrorCode.InvalidRequest, error.GetProperty("code").GetInt32());
        }

        [Fact]
        [DisplayName("A malformed Authorization header is refused")]
        public async Task MalformedAuthorization_Refused()
        {
            var answer = await PostAsync($"{SysProgIds.System}.Ping", validator: null, apiKey: "valid-api-key", authorization: "Bearer not-a-guid");

            AssertRefusedByGate(answer, "Invalid access token.");
        }

        [Fact]
        [DisplayName("A header that claims an in-process call does not make an HTTP call local")]
        public async Task HttpCall_IsNeverLocal()
        {
            // System.CreateSession is LocalOnly: only an in-process caller may run it.
            var answer = await PostAsync($"{SysProgIds.System}.CreateSession", validator: null, apiKey: "any-key");

            // Refused by the LocalOnly check, not for a missing method. The tests run in debug mode, where the real
            // message of the UnauthorizedAccessException is sent instead of "Access denied.".
            var error = answer.GetProperty("error");
            Assert.Equal((int)JsonRpcErrorCode.UserMessage, error.GetProperty("code").GetInt32());
            Assert.Contains(LocalOnlyRefusal, error.GetProperty("message").GetString(), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The same LocalOnly method runs for an in-process caller, so the refusal above comes from the transport")]
        public async Task LocalOnlyMethod_RunsInProcess()
        {
            var transport = new JsonRpcTransportInfo(
                JsonRpcTransportKind.InProcess,
                _fx.Provider,
                items: new Dictionary<string, object?> { [PolhemJsonRpc.AccessTokenItem] = Guid.Empty });
            var request = new TestRpcRequest
            {
                Method = $"{SysProgIds.System}.CreateSession",
                Params = new TestPayload { Value = new CreateSessionRequest { UserId = "local-only-check" } },
                Id = "1",
            };

            var result = await new JsonRpcDispatcher(PolhemJsonRpc.CreateServerOptions())
                .DispatchMessageAsync(TestDispatcher.Serialize(request), transport);
            using var answer = JsonDocument.Parse(result.Serialize()!);

            if (answer.RootElement.TryGetProperty("error", out var error))
            {
                Assert.DoesNotContain(LocalOnlyRefusal, error.GetProperty("message").GetString(), StringComparison.Ordinal);
            }
        }
    }
}
