using System.Text.Json;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Conversion;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages;
using Polhem.Core.Serialization;
using Polhem.JsonRpc;
using JsonRpcRequest = Polhem.JsonRpc.JsonRpcRequest;
using JsonRpcResponse = Polhem.JsonRpc.JsonRpcResponse;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// A transport that answers in place of a server, for tests that replace <see cref="ApiConnector.Provider"/>.
    /// </summary>
    /// <remarks>
    /// It reads the parameters with <see cref="JsonCodec"/> as the server does, so a test sees the envelope that
    /// went onto the wire. The answer is a <see cref="JsonRpcResult"/>; throwing <see cref="JsonRpcErrorException"/>
    /// answers with an error instead.
    /// </remarks>
    internal sealed class FakeApiTransport : IJsonRpcTransport
    {
        private readonly Func<FakeApiCall, JsonRpcResult> _respond;

        /// <summary>
        /// Initializes a new instance that answers with <paramref name="respond"/>, or echoes <c>"ok"</c>.
        /// </summary>
        public FakeApiTransport(Func<FakeApiCall, JsonRpcResult>? respond = null)
        {
            _respond = respond ?? (call => Answer(call, "ok"));
        }

        /// <summary>Gets the calls received, in order.</summary>
        public List<FakeApiCall> Calls { get; } = [];

        /// <summary>Gets the last call received, or <c>null</c>.</summary>
        public FakeApiCall? LastCall => Calls.Count == 0 ? null : Calls[^1];

        /// <summary>Gets the token the last call received.</summary>
        public CancellationToken LastToken { get; private set; }

        /// <summary>
        /// Answers with <paramref name="value"/> in the format and codec the call was sent in, as the server does.
        /// </summary>
        public static JsonRpcResult Answer(FakeApiCall call, object? value)
        {
            var result = new JsonRpcResult { Value = value, Codec = call.Params.Codec };
            if (call.Params.Format != PayloadFormat.Plain)
            {
                ApiPayloadConverter.TransformTo(result, call.Params.Format);
            }
            return result;
        }

        public Task<JsonRpcResponse?> SendAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
        {
            LastToken = cancellationToken;
            var parameters = request.Params is { } element
                ? JsonCodec.Deserialize<JsonRpcParams>(element.GetRawText()) ?? new JsonRpcParams()
                : new JsonRpcParams();
            var call = new FakeApiCall(request.Method, parameters, request.Id);
            Calls.Add(call);

            JsonRpcResponse response;
            try
            {
                using var document = JsonDocument.Parse(JsonCodec.Serialize(_respond(call)));
                response = JsonRpcResponse.Success(request.Id, document.RootElement.Clone());
            }
            catch (JsonRpcErrorException ex)
            {
                response = JsonRpcResponse.Failure(request.Id, ex.Error);
            }
            return Task.FromResult<JsonRpcResponse?>(response);
        }

        public Task<IReadOnlyList<JsonRpcResponse>> SendBatchAsync(IReadOnlyList<JsonRpcRequest> requests, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The connector does not send batches.");
    }

    /// <summary>
    /// One call a <see cref="FakeApiTransport"/> received.
    /// </summary>
    /// <param name="Method">The method name.</param>
    /// <param name="Params">The parameters as the server reads them, before the payload is restored.</param>
    /// <param name="Id">The request id.</param>
    internal sealed record FakeApiCall(string Method, JsonRpcParams Params, JsonRpcId Id)
    {
        /// <summary>
        /// Restores the sent value into <typeparamref name="T"/> as the server does, and returns it.
        /// </summary>
        public T SentValue<T>()
        {
            ApiPayloadConverter.RestoreRequest(Params, Params.Format, null, typeof(T));
            // A Plain body stays a `JsonElement` until the parameter binder reads it.
            var value = Params.Value is JsonElement element
                ? element.Deserialize<T>(ApiInputConverter.PlainReadOptions)
                : Params.Value;
            return Assert.IsType<T>(value);
        }
    }
}
