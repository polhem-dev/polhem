using System.ComponentModel;
using System.Text;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages.System;
using Polhem.Core.Serialization;
using Polhem.Definition;
using Polhem.Definition.Security;
using Polhem.Tests.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Polhem.Api.Core.Conversion;
using Polhem.Api.Core.Messages;

namespace Polhem.Api.AspNetCore.UnitTests
{
        /// <remarks>
    /// Uses <c>SharedDbFixture</c> rather than <c>PolhemTestFixture</c>: a request through the controller makes the
    /// executor touch the common database, and <c>PolhemTestFixture</c> does not create the schema.
    /// <para>
    /// This class checks JSON-RPC execution itself; the key gate is not its subject, so
    /// <see cref="IApiKeyValidator"/> is fixed to <see cref="UnconfiguredApiKeyValidator"/> and
    /// <b>the real key store is not read</b>. It used to pass only because <c>st_api_key</c> happened to be
    /// empty, which this class does not control: as soon as that table holds one enabled key, the gate is in
    /// force, and <c>"valid-api-key"</c>, which is not in key format, becomes <c>ApiKeyStatus.Invalid</c> → 401.
    /// The symptom does not point to the cause at all; it looks like "expected 200, got 401".
    /// </para>
    /// <para>
    /// Such a row can come from two places, and red locally / green in CI is caused by <b>the first</b>:
    /// (1) <b>leftovers</b>: the local container is persistent, so rows survive across test runs, while CI
    /// starts from a fresh container every time and the table is empty, so the same code stayed green in CI.
    /// (2) <b>parallel writes</b>: <c>ApiKeyRepositoryTests</c> writes enabled keys to the same common database
    /// and normally removes them in <c>finally</c>, but there is still a window when it runs in parallel with
    /// this project. (Since <c>21642741</c> only <c>Insert_ThenGet_SqlServer</c> still targets SQL Server and
    /// the rest use their own providers, so the window is narrower but has not gone away.)
    /// </para>
    /// </remarks>
    public class ApiAspNetCoreTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        private Guid _accessToken;

        public ApiAspNetCoreTests(SharedDbFixture fx)
        {
            _fx = fx;
        }

        /// <summary>
        /// The ApiServiceController class used by the tests.
        /// </summary>
        public class ApiServiceController : Controllers.ApiServiceController { }

        /// <summary>
        /// Test <see cref="IHostEnvironment"/>; ApiServiceController.IsDevelopment resolves this service.
        /// </summary>
        private sealed class TestHostEnvironment : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = Environments.Development;
            public string ApplicationName { get; set; } = "Tests";
            public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
            public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
                = new Microsoft.Extensions.FileProviders.NullFileProvider();
        }

        /// <summary>
        /// Always reports "this deployment has not issued any keys", so JSON-RPC execution is the only varying
        /// dimension of this class. That is the state which used to depend on the real <c>st_api_key</c> being
        /// empty; the test now sets it up itself.
        /// </summary>
        private sealed class UnconfiguredApiKeyValidator : IApiKeyValidator
        {
            public ApiKeyValidationResult Validate(string? apiKey)
                => new(ApiKeyStatus.NotConfigured);
        }

        /// <summary>
        /// Gets the JSON string of a JSON-RPC request model.
        /// </summary>
        /// <param name="progId">The program ID.</param>
        /// <param name="action">The action to execute.</param>
        /// <param name="args">The input value.</param>
        private static string GetRpcRequestJson(string progId, string action, object args)
        {
            var request = new JsonRpcRequest()
            {
                Method = $"{progId}.{action}",
                Params = new JsonRpcParams()
                {
                    Value = args
                },
                Id = Guid.NewGuid().ToString()
            };
            return request.ToJson();
        }

        /// <summary>
        /// Runs ApiServiceController and returns the deserialized result.
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="accessToken">The access token.</param>
        /// <param name="progId">The program ID.</param>
        /// <param name="action">The action to execute.</param>
        /// <param name="args">The JSON-RPC input arguments.</param>
        /// <returns>The deserialized execution result.</returns>
        private async Task<TResult> ExecuteRpcAsync<TResult>(Guid accessToken, string progId, string action, object args)
        {
            string json = GetRpcRequestJson(progId, action, args);

            var requestBody = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var context = new DefaultHttpContext
            {
                // ApiServiceController resolves `JsonRpcExecutor` and `IHostEnvironment` through
                // `HttpContext.RequestServices`. The test uses `TestOverrideServiceProvider` to add a fake
                // `IHostEnvironment` on top of the per-class fixture's `IServiceProvider`.
                RequestServices = new TestOverrideServiceProvider(
                    _fx.Provider,
                    (typeof(IHostEnvironment), new TestHostEnvironment()),
                    (typeof(IApiKeyValidator), new UnconfiguredApiKeyValidator()))
            };
            const string apiKey = "valid-api-key";
            var authorization = $"Bearer {accessToken}";
            context.Request.Headers["X-Api-Key"] = apiKey;
            context.Request.Headers.Authorization = authorization;
            context.Request.Headers.ContentType = "application/json";
            context.Request.Body = requestBody;

            var controller = new ApiServiceController
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = context
                }
            };

            var result = await controller.PostAsync(apiKey, authorization);
            var contentResult = Assert.IsType<ContentResult>(result);
            Assert.Equal(StatusCodes.Status200OK, contentResult.StatusCode);
            Assert.Equal("application/json", contentResult.ContentType);
            Assert.False(string.IsNullOrWhiteSpace(contentResult.Content));

            var response = JsonCodec.Deserialize<JsonRpcResponse>(contentResult.Content);
            return ApiOutputConverter.ConvertResultValue<TResult>(response!.Result!.Value!)!;
        }

        /// <summary>
        /// Gets a valid test AccessToken (seeded directly into SessionInfoService, without going through Login).
        /// </summary>
        private Guid GetAccessToken()
        {
            if (_accessToken == Guid.Empty)
                _accessToken = TestSessionFactory.CreateAccessToken(_fx);
            return _accessToken;
        }

        /// <summary>
        /// Tests the Ping method.
        /// </summary>
        [Fact]
        [DisplayName("Ping returns the ok status and the trace ID")]
        public async Task Ping_ValidRequest_ReturnsOkStatus()
        {
            var args = new PingRequest()
            {
                ClientName = "TestClient",
                TraceId = "001",
            };
            var result = await ExecuteRpcAsync<PingResponse>(Guid.Empty, SysProgIds.System, "Ping", args);
            Assert.NotNull(result);
            Assert.Equal("ok", result.Status);
            Assert.Equal("001", result.TraceId);
        }

        [Fact]
        [DisplayName("ExecFunc with Hello returns the greeting of the system-level handler")]
        public async Task ExecFunc_Hello_ReturnsGreeting()
        {
            Guid accessToken = GetAccessToken();
            var args = new ExecFuncRequest("Hello");
            var result = await ExecuteRpcAsync<ExecFuncResponse>(accessToken, SysProgIds.System, "ExecFunc", args);
            Assert.Equal("Hello system-level BusinessObject", result.Parameters!.GetValue<string>("Hello"));
        }
    }
}
