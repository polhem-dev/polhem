using System.ComponentModel;
using System.Text;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages.System;
using Polhem.Base.Serialization;
using Polhem.Definition;
using Polhem.Definition.Security;
using Polhem.Tests.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace Polhem.Api.AspNetCore.UnitTests
{
    /// <summary>
    /// How the controller wires the API key gate: when the validator throws, the request must fail closed
    /// (rejected) instead of passing as if "this deployment has not issued any keys".
    /// </summary>
        /// <remarks>
    /// Uses <c>SharedDbFixture</c> rather than <c>PolhemTestFixture</c>: once these requests reach the executor
    /// they still touch the common database, and <c>PolhemTestFixture</c> does not create the schema.
    /// <para>
    /// The key validation dimension is decided by the <see cref="IApiKeyValidator"/> each test supplies,
    /// <b>not by the real key store</b>. This is deliberate: once this class reads <c>st_api_key</c>,
    /// whether the gate is in force depends on whether that table holds a row at the moment, which this
    /// class does not control. The symptom does not point to the cause either: with the gate in force, any
    /// header that is not in key format becomes <c>ApiKeyStatus.Invalid</c> → 401, which looks like
    /// "expected 200, got 401".
    /// </para>
    /// <para>
    /// Such a row can come from two places, and red locally / green in CI is caused by <b>the first</b>:
    /// (1) <b>leftovers</b>: the local container is persistent, so rows survive across test runs, while CI
    /// starts from a fresh container every time and the table is empty. (2) <b>parallel writes</b>:
    /// <c>ApiKeyRepositoryTests</c> writes enabled keys to the same common database and normally removes them in
    /// <c>finally</c>, but there is still a window when it runs in parallel with this project.
    /// </para>
    /// </remarks>
    public class ApiKeyGateControllerTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public ApiKeyGateControllerTests(SharedDbFixture fx) { _fx = fx; }

        private sealed class TestController : Controllers.ApiServiceController { }

        private sealed class TestHostEnvironment : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = Environments.Development;
            public string ApplicationName { get; set; } = "Tests";
            public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
            public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
                = new Microsoft.Extensions.FileProviders.NullFileProvider();
        }

        /// <summary>
        /// Simulates "the key store cannot be read", the shape the validation path throws when the database is
        /// unavailable.
        /// </summary>
        private sealed class ThrowingApiKeyValidator : IApiKeyValidator
        {
            public ApiKeyValidationResult Validate(string? apiKey)
                => throw new InvalidOperationException("store unreachable");
        }

        private sealed class FixedApiKeyValidator : IApiKeyValidator
        {
            private readonly ApiKeyValidationResult _result;
            public FixedApiKeyValidator(ApiKeyValidationResult result) { _result = result; }
            public ApiKeyValidationResult Validate(string? apiKey) => _result;
        }

        private async Task<IActionResult> PostAsync(string method, IApiKeyValidator? validator, string apiKey)
        {
            // Send a valid Bearer token so the API key is the only varying dimension. Otherwise a method that
            // requires authorization is rejected first for the missing `Authorization` header, and the key gate
            // is never reached.
            var accessToken = TestSessionFactory.CreateAccessToken(_fx);
            var request = new JsonRpcRequest
            {
                Method = method,
                Params = new JsonRpcParams { Value = new PingRequest { ClientName = "unit", TraceId = "T-1" } },
                Id = Guid.NewGuid().ToString(),
            };

            // IMPORTANT: register the IApiKeyValidator override unconditionally, `null` included.
            // TestOverrideServiceProvider honours a null instance by short-circuiting the inner
            // provider, and that is the only way to reach the no-validator path: AddPolhemFramework
            // always registers a real ApiKeyValidator, so adding the override only when non-null
            // let the lookup fall through to it. Post_NoValidatorRegistered_UsesPresenceCheck then
            // exercised the live key store rather than the compatibility path it names, and its
            // verdict depended on whether the shared common database happened to hold an enabled key.
            var overrides = new (Type, object?)[]
            {
                (typeof(IHostEnvironment), new TestHostEnvironment()),
                (typeof(IApiKeyValidator), validator),
            };

            var context = new DefaultHttpContext
            {
                RequestServices = new TestOverrideServiceProvider(_fx.Provider, overrides),
            };
            context.Request.Headers["X-Api-Key"] = apiKey;
            context.Request.Headers.Authorization = "Bearer " + accessToken;
            context.Request.Headers.ContentType = "application/json";
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(request.ToJson()));

            var controller = new TestController
            {
                ControllerContext = new ControllerContext { HttpContext = context },
            };
            return await controller.PostAsync(apiKey, "Bearer " + accessToken);
        }

        [Fact]
        [DisplayName("A throwing key validator fails closed with 401 instead of degrading to the lenient mode")]
        public async Task Post_ValidatorThrows_FailsClosed()
        {
            var result = await PostAsync($"{SysProgIds.System}.GetCommonConfiguration",
                new ThrowingApiKeyValidator(), "any-key");

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, objectResult.StatusCode);
        }

        [Fact]
        [DisplayName("System.Ping still answers when the key validator throws (health checks need no key)")]
        public async Task Post_ValidatorThrows_PingStillAnswers()
        {
            var result = await PostAsync($"{SysProgIds.System}.Ping",
                new ThrowingApiKeyValidator(), "any-key");

            var contentResult = Assert.IsType<ContentResult>(result);
            Assert.Equal(StatusCodes.Status200OK, contentResult.StatusCode);
            var response = JsonCodec.Deserialize<JsonRpcResponse>(contentResult.Content!);
            Assert.Null(response!.Error);
        }

        [Fact]
        [DisplayName("In strict mode an invalid key returns 401 with a message that does not reveal the rejection reason")]
        public async Task Post_InvalidKey_ReturnsUnauthorizedWithMergedMessage()
        {
            var validator = new FixedApiKeyValidator(
                new ApiKeyValidationResult(ApiKeyStatus.Invalid, "some-app", string.Empty));

            var result = await PostAsync($"{SysProgIds.System}.GetCommonConfiguration", validator, "some-app.bad");

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, objectResult.StatusCode);
            var response = Assert.IsType<JsonRpcResponse>(objectResult.Value);
            Assert.Equal("Missing or invalid API key.", response.Error!.Message);
        }

        [Fact]
        [DisplayName("A host without a registered IApiKeyValidator keeps the non-empty check (compatibility mode)")]
        public async Task Post_NoValidatorRegistered_UsesPresenceCheck()
        {
            var result = await PostAsync($"{SysProgIds.System}.GetCommonConfiguration", validator: null, apiKey: "any-key");

            var contentResult = Assert.IsType<ContentResult>(result);
            Assert.Equal(StatusCodes.Status200OK, contentResult.StatusCode);
        }

        [Fact]
        [DisplayName("In strict mode Ping with an invalid key reports Invalid and omits the version")]
        public async Task Post_Ping_InvalidKey_ReportsStatusWithoutVersion()
        {
            var validator = new FixedApiKeyValidator(
                new ApiKeyValidationResult(ApiKeyStatus.Invalid, "some-app", string.Empty));

            var result = await PostAsync($"{SysProgIds.System}.Ping", validator, "some-app.bad");

            var contentResult = Assert.IsType<ContentResult>(result);
            var response = JsonCodec.Deserialize<JsonRpcResponse>(contentResult.Content!);
            Assert.Null(response!.Error);
            var ping = Polhem.Api.Core.Conversion.ApiOutputConverter
                .ConvertResultValue<PingResponse>(response.Result!.Value!)!;
            Assert.Equal("ok", ping.Status);
            Assert.Equal(ApiKeyStatus.Invalid, ping.ApiKeyStatus);
            Assert.Null(ping.Version);
        }
    }
}
