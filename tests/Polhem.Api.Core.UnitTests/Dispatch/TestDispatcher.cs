using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Core.Authorization;
using Polhem.Api.Core.Conversion;
using Polhem.Api.Core.Dispatch;
using Polhem.Api.Core.Transformers;
using Polhem.Api.Core.Messages;
using Polhem.Definition;
using Polhem.Definition.Security;
using Polhem.JsonRpc;
using PayloadEnvelope = Polhem.JsonRpc.Payload.PayloadEnvelope;
using PayloadOptions = Polhem.JsonRpc.Payload.PayloadOptions;
using PayloadProcessor = Polhem.JsonRpc.Payload.PayloadProcessor;
using Polhem.JsonRpc.Payload.Server;
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
            => DispatchAsync(Serialize(request, PayloadOptions), request.Params.Key, cancellationToken);

        /// <summary>
        /// Sends a request written as JSON, as a client in another language sends it, and returns the answer.
        /// </summary>
        public Task<TestRpcResponse> ExecuteJsonAsync(string requestJson, CancellationToken cancellationToken = default)
            => DispatchAsync(Encoding.UTF8.GetBytes(requestJson), null, cancellationToken);

        /// <summary>
        /// Registers what the framework's dispatcher resolves from a call's services and <c>AddPolhemFramework</c>
        /// otherwise registers, for a test that builds its own service collection: the request authorization, the
        /// payload options and the replay store.
        /// </summary>
        internal static IServiceCollection AddDispatchDefaults(IServiceCollection services)
        {
            services.AddSingleton<IApiAuthorizationValidator, ApiAuthorizationValidator>();
            services.AddSingleton(PolhemPayload.CreateOptions());
            services.AddSingleton<IPayloadReplayStore>(new MemoryPayloadReplayStore());
            return services;
        }

        /// <summary>Gets the payload options the backend's services carry, which the call is sealed and opened with.</summary>
        private PayloadOptions PayloadOptions => (PayloadOptions?)_services.GetService(typeof(PayloadOptions)) ?? PolhemPayload.CreateOptions();

        private async Task<TestRpcResponse> DispatchAsync(byte[] body, byte[]? key, CancellationToken cancellationToken)
        {
            var resultType = new ResultTypeFilter();
            var options = PolhemJsonRpc.CreateServerOptions();
            options.Filters.Insert(0, resultType);

            var result = await new JsonRpcDispatcher(options).DispatchMessageAsync(body, CreateTransport(), cancellationToken);
            var json = Encoding.UTF8.GetString(result.Serialize() ?? []);
            return TestRpcResponse.Read(json, resultType.ResultType, new PayloadProcessor(PayloadOptions), key);
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

        /// <summary>Writes a request as the client sends it, sealing its payload with <paramref name="options"/>.</summary>
        internal static byte[] Serialize(TestRpcRequest request, PayloadOptions? options = null)
        {
            var payload = request.Params;
            var parameters = request.RawParams ?? new PayloadProcessor(options ?? PolhemPayload.CreateOptions()).Wrap(
                payload.Value, (Polhem.JsonRpc.Payload.PayloadFormat)payload.Format, payload.Codec, payload.Key, payload.Sequence);
            var id = request.Id == null ? JsonRpcId.Null : JsonRpcId.FromString(request.Id);
            return JsonRpcSerializer.SerializeRequest(new Polhem.JsonRpc.JsonRpcRequest(request.Method, parameters, id));
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

        /// <summary>Gets or sets the parameters, sealed into the payload envelope on the way out.</summary>
        public TestPayload Params { get; set; } = new();

        /// <summary>
        /// Gets or sets the <c>params</c> element exactly as it is sent, for a test that builds the envelope itself;
        /// <see cref="Params"/> is ignored then.
        /// </summary>
        public JsonElement? RawParams { get; set; }

        /// <summary>Gets or sets the request id, or <c>null</c> for a JSON <c>null</c> id.</summary>
        public string? Id { get; set; } = "test";
    }

    /// <summary>
    /// A payload for <see cref="TestDispatcher"/>: the value and how it is carried.
    /// </summary>
    internal sealed class TestPayload
    {
        /// <summary>Gets or sets the format.</summary>
        public PayloadFormat Format { get; set; } = PayloadFormat.Plain;

        /// <summary>Gets or sets the value.</summary>
        public object? Value { get; set; }

        /// <summary>Gets or sets the codec to name; blank for the default.</summary>
        public string Codec { get; set; } = string.Empty;

        /// <summary>Gets or sets the key of an encrypted payload.</summary>
        public byte[]? Key { get; set; }

        /// <summary>Gets or sets the sequence number written to the frame when frames are on.</summary>
        public long Sequence { get; set; }
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
        public TestPayload? Result { get; private init; }

        /// <summary>Gets the error, or <c>null</c> for a result.</summary>
        public TestRpcError? Error { get; private init; }

        /// <summary>Gets the <c>method</c> member the answer echoes, or <c>null</c>.</summary>
        public string? Method { get; private init; }

        /// <summary>Gets the id of the answer, or <c>null</c> for a JSON <c>null</c> id.</summary>
        public string? Id { get; private init; }

        internal static TestRpcResponse Read(string json, Type? resultType, PayloadProcessor payload, byte[]? key)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            TestPayload? result = null;
            if (root.TryGetProperty("result", out var resultElement) && resultElement.ValueKind != JsonValueKind.Null)
            {
                var envelope = PayloadEnvelope.Read(resultElement.Clone());
                var value = ResolvePlainValue(payload.Open(envelope, key, out _));
                if (envelope.Format == Polhem.JsonRpc.Payload.PayloadFormat.Plain && value is JsonElement && resultType != null)
                {
                    value = ConvertPlain(value, resultType);
                }
                result = new TestPayload { Format = (PayloadFormat)envelope.Format, Value = value, Codec = envelope.Codec };
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

        // A plain value's JSON primitives read as the .NET values they spell, as the client connector reads them.
        private static object? ResolvePlainValue(object? value) => value is not JsonElement element ? value : element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var number) ? number : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element,
        };

        private static object? ConvertPlain(object value, Type resultType)
            => typeof(ApiOutputConverter).GetMethod(nameof(ApiOutputConverter.ConvertResultValue))!
                .MakeGenericMethod(resultType)
                .Invoke(null, [value]);
    }
}
