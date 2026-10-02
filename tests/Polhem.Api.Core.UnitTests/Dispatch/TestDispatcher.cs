using System.Text;
using System.Text.Json;
using Polhem.Api.Core.Conversion;
using Polhem.Api.Core.Dispatch;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages;
using Polhem.Core.Serialization;
using Polhem.Definition;
using Polhem.Definition.Security;
using Polhem.JsonRpc;
using Polhem.JsonRpc.Server;
using Polhem.Tests.Shared;

namespace Polhem.Api.Core.UnitTests.Dispatch
{
    /// <summary>
    /// Sends one request through the framework's JSON-RPC pipeline, as a host's dispatcher does, and reads the
    /// answer back into Polhem's envelope types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The request goes in as JSON and the answer comes back as JSON, so a test passes the same serialization
    /// boundary a client does. A <see cref="PayloadFormat.Plain"/> result is converted to the type of the message the
    /// server sent, the way the client connector converts it, so a test can assert on the typed value.
    /// </para>
    /// <para>
    /// A call is in-process by default. With <see cref="IsLocalCall"/> off it arrives over HTTP instead: it carries
    /// <see cref="ApiKey"/> in <c>X-Api-Key</c> and the access token as a Bearer header, and its key goes through
    /// <see cref="ApiKeyValidator"/>; with none, any non-empty key passes.
    /// </para>
    /// </remarks>
    internal sealed class TestDispatcher
    {
        private readonly IServiceProvider _services;

        /// <summary>
        /// Initializes a new instance over <paramref name="services"/>.
        /// </summary>
        /// <param name="services">The backend's services.</param>
        /// <param name="businessObjectFactory">A factory that replaces the registered one, or <c>null</c>.</param>
        public TestDispatcher(IServiceProvider services, IBusinessObjectFactory? businessObjectFactory = null)
        {
            _services = businessObjectFactory == null
                ? services
                : new TestOverrideServiceProvider(services, (typeof(IBusinessObjectFactory), businessObjectFactory));
        }

        /// <summary>Gets the access token the call carries.</summary>
        public Guid AccessToken { get; init; }

        /// <summary>Gets a value indicating whether the call is in-process; otherwise it arrives over HTTP.</summary>
        public bool IsLocalCall { get; init; } = true;

        /// <summary>Gets the API key an HTTP call carries.</summary>
        public string ApiKey { get; init; } = "unit-test";

        /// <summary>
        /// Gets the validator an HTTP call's API key goes through, or <c>null</c> for none, which accepts any
        /// non-empty key.
        /// </summary>
        public IApiKeyValidator? ApiKeyValidator { get; init; }

        /// <summary>
        /// Sends the request and returns the answer.
        /// </summary>
        public Task<TestRpcResponse> ExecuteAsync(TestRpcRequest request, CancellationToken cancellationToken = default)
            => DispatchAsync(Serialize(request), cancellationToken);

        /// <summary>
        /// Sends a request written as JSON, as a client in another language sends it, and returns the answer.
        /// </summary>
        public Task<TestRpcResponse> ExecuteJsonAsync(string requestJson, CancellationToken cancellationToken = default)
            => DispatchAsync(Encoding.UTF8.GetBytes(requestJson), cancellationToken);

        private async Task<TestRpcResponse> DispatchAsync(byte[] body, CancellationToken cancellationToken)
        {
            var resultType = new ResultTypeFilter();
            var options = PolhemJsonRpc.CreateServerOptions();
            options.Filters.Insert(0, resultType);

            var result = await new JsonRpcDispatcher(options).DispatchMessageAsync(body, CreateTransport(), cancellationToken);
            var json = Encoding.UTF8.GetString(result.Serialize() ?? []);
            return TestRpcResponse.Read(json, resultType.ResultType);
        }

        private JsonRpcTransportInfo CreateTransport()
        {
            if (IsLocalCall)
            {
                return new JsonRpcTransportInfo(
                    JsonRpcTransportKind.InProcess,
                    _services,
                    items: new Dictionary<string, object?> { [PolhemJsonRpc.AccessTokenItem] = AccessToken });
            }

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [ApiHeaders.ApiKey] = ApiKey };
            if (AccessToken != Guid.Empty)
            {
                headers[ApiHeaders.Authorization] = $"Bearer {AccessToken}";
            }
            var services = new TestOverrideServiceProvider(_services, (typeof(IApiKeyValidator), ApiKeyValidator));
            return new JsonRpcTransportInfo(JsonRpcTransportKind.Http, services, headers, "127.0.0.1");
        }

        internal static byte[] Serialize(TestRpcRequest request)
        {
            using var parameters = JsonDocument.Parse(JsonCodec.Serialize(request.Params));
            var id = request.Id == null ? JsonRpcId.Null : JsonRpcId.FromString(request.Id);
            return JsonRpcSerializer.SerializeRequest(
                new Polhem.JsonRpc.JsonRpcRequest(request.Method, parameters.RootElement.Clone(), id));
        }

        /// <summary>
        /// Records the type of the message the server sends for the action's return value.
        /// </summary>
        private sealed class ResultTypeFilter : IJsonRpcFilter
        {
            public Type? ResultType { get; private set; }

            public async ValueTask InvokeAsync(JsonRpcRequestContext context, JsonRpcFilterDelegate next)
            {
                await next(context);
                ResultType = context.ReturnValue is { } value ? ApiOutputConverter.Convert(value)?.GetType() : null;
            }
        }
    }

    /// <summary>
    /// A request for <see cref="TestDispatcher"/>.
    /// </summary>
    internal sealed class TestRpcRequest
    {
        /// <summary>Gets or sets the method, <c>ProgId.Action</c>.</summary>
        public string Method { get; set; } = string.Empty;

        /// <summary>Gets or sets the parameters.</summary>
        public JsonRpcParams Params { get; set; } = new();

        /// <summary>Gets or sets the request id, or <c>null</c> for a JSON <c>null</c> id.</summary>
        public string? Id { get; set; } = "test";
    }

    /// <summary>
    /// An error answer from <see cref="TestDispatcher"/>.
    /// </summary>
    internal sealed record TestRpcError(int Code, string Message);

    /// <summary>
    /// An answer from <see cref="TestDispatcher"/>.
    /// </summary>
    internal sealed class TestRpcResponse
    {
        /// <summary>Gets the answer as it was written.</summary>
        public string Json { get; private init; } = string.Empty;

        /// <summary>Gets the result, or <c>null</c> for an error.</summary>
        public JsonRpcResult? Result { get; private init; }

        /// <summary>Gets the error, or <c>null</c> for a result.</summary>
        public TestRpcError? Error { get; private init; }

        /// <summary>Gets the <c>method</c> member the answer echoes, or <c>null</c>.</summary>
        public string? Method { get; private init; }

        /// <summary>Gets the id of the answer, or <c>null</c> for a JSON <c>null</c> id.</summary>
        public string? Id { get; private init; }

        internal static TestRpcResponse Read(string json, Type? resultType)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            JsonRpcResult? result = null;
            if (root.TryGetProperty("result", out var resultElement) && resultElement.ValueKind != JsonValueKind.Null)
            {
                result = JsonCodec.Deserialize<JsonRpcResult>(resultElement.GetRawText());
                if (result is { Format: PayloadFormat.Plain, Value: JsonElement } && resultType != null)
                {
                    result.Value = ConvertPlain(result.Value, resultType);
                }
            }

            TestRpcError? error = null;
            if (root.TryGetProperty("error", out var errorElement))
            {
                error = new TestRpcError(
                    errorElement.GetProperty("code").GetInt32(),
                    errorElement.GetProperty("message").GetString() ?? string.Empty);
            }

            return new TestRpcResponse
            {
                Json = json,
                Result = result,
                Error = error,
                Method = root.TryGetProperty("method", out var method) ? method.GetString() : null,
                Id = root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null,
            };
        }

        private static object? ConvertPlain(object value, Type resultType)
            => typeof(ApiOutputConverter).GetMethod(nameof(ApiOutputConverter.ConvertResultValue))!
                .MakeGenericMethod(resultType)
                .Invoke(null, [value]);
    }
}
