using System.ComponentModel;
using Polhem.Api.Core.Messages.System;
using Polhem.Core.Serialization;
using Polhem.Definition;
using Polhem.Definition.Settings;
using Polhem.Tests.Shared;
using Polhem.Api.Core.Messages;

namespace Polhem.Api.Core.UnitTests.Dispatch
{
    /// <summary>
    /// Calls through the framework's JSON-RPC pipeline: the system actions, cancellation, and Plain bodies written by
    /// hand as a JavaScript client sends them.
    /// </summary>
    public class DispatchTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;
        private Guid _accessToken;

        public DispatchTests(PolhemTestFixture fx)
        {
            _fx = fx;
        }

        private TestDispatcher NewExecutor(Guid accessToken) => new(_fx.Provider) { AccessToken = accessToken };

        [Fact]
        [DisplayName("ExecuteAsync with a cancelled token throws OperationCanceledException instead of returning an error envelope")]
        public async Task ExecuteAsync_CancelledToken_ThrowsOperationCanceled()
        {
            var request = new TestRpcRequest()
            {
                Method = $"{SysProgIds.System}.{SystemActions.Ping}",
                Params = new TestPayload() { Value = new PingRequest() },
                Id = Guid.NewGuid().ToString()
            };
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // A caller that cancelled is not waiting for an answer, so the dispatcher must not turn the cancellation
            // into an ordinary failure response the way it does for every other exception.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => NewExecutor(Guid.Empty).ExecuteAsync(request, cts.Token));
        }

        [Fact]
        [DisplayName("ExecuteAsync with a token that is never cancelled still answers normally")]
        public async Task ExecuteAsync_LiveToken_ReturnsResult()
        {
            var request = new TestRpcRequest()
            {
                Method = $"{SysProgIds.System}.{SystemActions.Ping}",
                Params = new TestPayload() { Value = new PingRequest() },
                Id = Guid.NewGuid().ToString()
            };
            using var cts = new CancellationTokenSource();

            var response = await NewExecutor(Guid.Empty).ExecuteAsync(request, cts.Token);

            Assert.Null(response.Error);
            Assert.IsType<PingResponse>(response.Result!.Value);
        }

        /// <summary>
        /// Executes an API method.
        /// </summary>
        /// <param name="accessToken">The access token.</param>
        /// <param name="progId">The program ID.</param>
        /// <param name="action">The action to execute.</param>
        /// <param name="value">The value passed in.</param>
        private async Task<T> ApiExecute<T>(Guid accessToken, string progId, string action, object value)
        {
            var request = new TestRpcRequest()
            {
                Method = $"{progId}.{action}",
                Params = new TestPayload()
                {
                    Value = value
                },
                Id = Guid.NewGuid().ToString()
            };

            var executor = NewExecutor(accessToken);
            var response = await executor.ExecuteAsync(request);
            return (T)response.Result!.Value!;
        }

        /// <summary>
        /// Gets a valid test AccessToken (planted directly in SessionInfoService, without going through Login).
        /// </summary>
        private Guid GetAccessToken()
        {
            if (_accessToken == Guid.Empty)
                _accessToken = TestSessionFactory.CreateAccessToken(_fx);
            return _accessToken;
        }

        /// <summary>
        /// Executes the Ping method through the API.
        /// </summary>
        [Fact]
        [DisplayName("Ping returns the correct status and trace ID")]
        public async Task Ping_ValidRequest_ReturnsOkStatus()
        {
            var args = new PingRequest()
            {
                ClientName = "TestClient",
                TraceId = "001",
            };
            var result = await ApiExecute<PingResponse>(Guid.Empty, SysProgIds.System, "Ping", args);
            Assert.NotNull(result);
            Assert.Equal("ok", result.Status);
            Assert.Equal("001", result.TraceId);
        }

        /// <summary>
        /// Tests the GetCommonConfiguration method.
        /// </summary>
        [Fact]
        [DisplayName("GetCommonConfiguration returns the CommonConfiguration of SystemSettings as XML")]
        public async Task GetCommonConfiguration_ValidRequest_ReturnsConfigurationXml()
        {
            var args = new GetCommonConfigurationRequest();
            var result = await ApiExecute<GetCommonConfigurationResponse>(Guid.Empty, SysProgIds.System, SystemActions.GetCommonConfiguration, args);

            // Values from `tests/Define/SystemSettings.xml`.
            var configuration = XmlCodec.Deserialize<CommonConfiguration>(result.CommonConfiguration)!;
            Assert.Equal("1.0.0", configuration.Version);
            Assert.Equal("gzip", configuration.ApiPayloadOptions.Compressor);
        }

        /// <summary>
        /// Simulates a JS front end sending Plain format with the params.type field omitted entirely, and verifies
        /// that the server still gets the target type through reflection on the BO method and deserializes correctly.
        /// </summary>
        [Fact]
        [DisplayName("The server deserializes Plain format without a type field normally")]
        public async Task Ping_PlainWithoutTypeField_DeserializesAndReturnsOk()
        {
            // The params have no "type" field at all, simulating JSON sent natively from JS.
            const string json = """
                {
                    "jsonrpc": "2.0",
                    "method": "System.Ping",
                    "params": {
                        "format": 0,
                        "value": { "clientName": "JsClient", "traceId": "js-001" }
                    },
                    "id": "js-req-1"
                }
                """;

            var response = await NewExecutor(Guid.Empty).ExecuteJsonAsync(json);

            Assert.Null(response.Error);
            var result = response.Result!.Value as PingResponse;
            Assert.NotNull(result);
            Assert.Equal("ok", result.Status);
            Assert.Equal("js-001", result.TraceId);
        }

        /// <summary>
        /// Simulates a JS front end sending Plain format with an empty params.type, and verifies that the server
        /// behaves the same as when the type is omitted entirely.
        /// </summary>
        [Fact]
        [DisplayName("The server deserializes Plain format with an empty type normally")]
        public async Task Ping_PlainWithEmptyTypeField_DeserializesAndReturnsOk()
        {
            const string json = """
                {
                    "jsonrpc": "2.0",
                    "method": "System.Ping",
                    "params": {
                        "format": 0,
                        "value": { "clientName": "JsClient", "traceId": "js-002" },
                        "type": ""
                    },
                    "id": "js-req-2"
                }
                """;

            var response = await NewExecutor(Guid.Empty).ExecuteJsonAsync(json);

            Assert.Null(response.Error);
            var result = response.Result!.Value as PingResponse;
            Assert.NotNull(result);
            Assert.Equal("js-002", result.TraceId);
        }

        /// <summary>
        /// Simulates a JS front end sending Plain format with a bogus type string in params.type, and verifies that the
        /// Plain path never reads the type field (the payload restore returns early for Plain).
        /// </summary>
        [Fact]
        [DisplayName("The server ignores a bogus type string in Plain format and deserializes normally")]
        public async Task Ping_PlainWithBogusTypeField_IgnoresTypeAndReturnsOk()
        {
            const string json = """
                {
                    "jsonrpc": "2.0",
                    "method": "System.Ping",
                    "params": {
                        "format": 0,
                        "value": { "clientName": "JsClient", "traceId": "js-003" },
                        "type": "NonExistent.Type.That.Should.Be.Ignored, FakeAssembly"
                    },
                    "id": "js-req-3"
                }
                """;

            var response = await NewExecutor(Guid.Empty).ExecuteJsonAsync(json);

            // If the Plain path read the type, this would fail (whitelist rejection or a type that cannot load).
            // Not failing means the framework ignores the type entirely and gets `PingRequest` by reflection on the BO method.
            Assert.Null(response.Error);
            var result = response.Result!.Value as PingResponse;
            Assert.NotNull(result);
            Assert.Equal("js-003", result.TraceId);
        }

        /// <summary>
        /// Executes the Hello method through the API.
        /// </summary>
        [Fact]
        [DisplayName("ExecFunc running Hello returns the greeting of the system-level handler")]
        public async Task ExecFunc_Hello_ReturnsGreeting()
        {
            Guid accessToken = GetAccessToken();

            var request = new TestRpcRequest()
            {
                Method = $"{SysProgIds.System}.ExecFunc",
                Params = new TestPayload()
                {
                    Value = new ExecFuncRequest("Hello")
                },
                Id = Guid.NewGuid().ToString()
            };

            var executor = NewExecutor(accessToken);
            var response = await executor.ExecuteAsync(request);
            var execFuncResult = Assert.IsType<ExecFuncResponse>(response.Result!.Value);
            Assert.Equal("Hello system-level BusinessObject", execFuncResult.Parameters!.GetValue<string>("Hello"));
        }
    }
}
