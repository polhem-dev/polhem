using System.Text.Json;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Conversion;
using Polhem.Api.Core.Messages;
using Polhem.JsonRpc;
using PayloadEnvelope = Polhem.JsonRpc.Payload.PayloadEnvelope;
using PayloadProcessor = Polhem.JsonRpc.Payload.PayloadProcessor;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// A transport that answers in place of a server, for tests that replace <see cref="ApiConnector.Provider"/>.
    /// </summary>
    /// <remarks>
    /// It reads the parameters as the server's payload filter does, with the client's own payload options, so a test
    /// sees the envelope that went onto the wire. The answer is a payload envelope; throwing
    /// <see cref="JsonRpcErrorException"/> answers with an error instead.
    /// </remarks>
    internal sealed class FakeApiTransport : IJsonRpcTransport
    {
        private readonly Func<FakeApiCall, PayloadEnvelope> _respond;

        /// <summary>
        /// Initializes a new instance that answers with <paramref name="respond"/>, or echoes <c>"ok"</c>.
        /// </summary>
        public FakeApiTransport(Func<FakeApiCall, PayloadEnvelope>? respond = null)
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
        public static PayloadEnvelope Answer(FakeApiCall call, object? value)
            => new PayloadProcessor(ApiClientInfo.PayloadOptions)
                .Seal(value, call.Params.Envelope.Format, call.Params.Codec);

        public Task<JsonRpcResponse?> SendAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
        {
            LastToken = cancellationToken;
            var call = new FakeApiCall(request.Method, new FakeParams(PayloadEnvelope.Read(request.Params)), request.Id);
            Calls.Add(call);

            JsonRpcResponse response;
            try
            {
                response = JsonRpcResponse.Success(request.Id, _respond(call).ToElement());
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
    /// The parameters a <see cref="FakeApiTransport"/> received, as the server reads them before the payload is opened.
    /// </summary>
    /// <param name="Envelope">The envelope as it arrived.</param>
    internal sealed record FakeParams(PayloadEnvelope Envelope)
    {
        /// <summary>Gets the format the call was sent in.</summary>
        public PayloadFormat Format => (PayloadFormat)Envelope.Format;

        /// <summary>Gets the codec the call named.</summary>
        public string Codec => Envelope.Codec;

        /// <summary>Gets the type name the call wrote.</summary>
        public string TypeName => Envelope.TypeName;

        /// <summary>Gets the body bytes of an encoded call, or the JSON value of a plain one.</summary>
        public object? Value => Envelope.Body is { } body ? body : Envelope.Value;
    }

    /// <summary>
    /// One call a <see cref="FakeApiTransport"/> received.
    /// </summary>
    /// <param name="Method">The method name.</param>
    /// <param name="Params">The parameters as the server reads them, before the payload is opened.</param>
    /// <param name="Id">The request id.</param>
    internal sealed record FakeApiCall(string Method, FakeParams Params, JsonRpcId Id)
    {
        /// <summary>
        /// Opens the sent value into <typeparamref name="T"/> as the server does, and returns it.
        /// </summary>
        public T SentValue<T>()
        {
            var value = new PayloadProcessor(ApiClientInfo.PayloadOptions).OpenRequest(Params.Envelope, typeof(T), null, out _);
            // A Plain body stays a `JsonElement` until the parameter binder reads it.
            if (value is JsonElement element)
                value = element.Deserialize<T>(ApiInputConverter.PlainReadOptions);
            return Assert.IsType<T>(value);
        }
    }
}
