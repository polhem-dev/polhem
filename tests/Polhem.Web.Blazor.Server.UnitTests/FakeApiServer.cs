using System.Text.Json;
using Polhem.Api.Client;
using Polhem.Api.Core.JsonRpc;
using Polhem.Core.Exceptions;
using Polhem.Api.Core.Transformers;
using Polhem.JsonRpc;
using Polhem.JsonRpc.Payload;

namespace Polhem.Web.Blazor.Server.UnitTests
{
    /// <summary>
    /// Answers the calls of a <see cref="PolhemApiClient"/> in place of a backend, one handler per method.
    /// </summary>
    /// <remarks>
    /// The components take the circuit's client from the container and create their connectors from it, so a test
    /// stands in for the backend at the transport. Requests are opened and answers sealed with the client's own
    /// payload options, so a handler sees the request as a server would and the client reads the answer as it would
    /// read a server's.
    /// </remarks>
    internal sealed class FakeApiServer : IJsonRpcTransport
    {
        private readonly Dictionary<string, Func<PayloadEnvelope, string, object?>> _handlers = new(StringComparer.Ordinal);

        /// <summary>Gets the payload options the client and this server share.</summary>
        public PayloadOptions PayloadOptions { get; } = PolhemPayload.CreateOptions();

        /// <summary>Gets the method of every call received, in order.</summary>
        public List<string> Calls { get; } = [];

        /// <summary>
        /// Answers <paramref name="method"/> (<c>progId.action</c>) with what <paramref name="respond"/> returns for
        /// the request. A <see cref="UserMessageException"/> thrown by the handler reaches the client as the server
        /// would send it.
        /// </summary>
        public FakeApiServer On<TRequest>(string method, Func<TRequest, object?> respond)
        {
            _handlers[method] = (envelope, name) => respond(Open<TRequest>(envelope, name));
            return this;
        }

        /// <summary>Creates a client whose calls all come to this server.</summary>
        public PolhemApiClient CreateClient()
            => PolhemApiClient.CreateWithTransport(_ => this, payloadOptions: PayloadOptions);

        private TRequest Open<TRequest>(PayloadEnvelope envelope, string method)
        {
            var value = new PayloadProcessor(PayloadOptions).OpenRequest(envelope, typeof(TRequest), null, method, out _);
            // A Plain body stays a `JsonElement` until the parameter binder reads it.
            if (value is JsonElement element)
                value = element.Deserialize<TRequest>();
            return (TRequest)value!;
        }

        public Task<JsonRpcResponse?> SendAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(request.Method);
            var envelope = PayloadEnvelope.Read(request.Params);
            if (!_handlers.TryGetValue(request.Method, out var handler))
                throw new InvalidOperationException($"The fake server has no handler for '{request.Method}'.");

            JsonRpcResponse response;
            try
            {
                var value = handler(envelope, request.Method);
                var sealedResponse = new PayloadProcessor(PayloadOptions)
                    .SealResponse(request.Method, value, envelope.Format, envelope.Codec);
                response = JsonRpcResponse.Success(request.Id, sealedResponse.ToElement());
            }
            catch (UserMessageException ex)
            {
                response = JsonRpcResponse.Failure(request.Id,
                    new JsonRpcErrorException((int)JsonRpcErrorCode.UserMessage, ex.Message).Error);
            }
            return Task.FromResult<JsonRpcResponse?>(response);
        }

        public Task<IReadOnlyList<JsonRpcResponse>> SendBatchAsync(IReadOnlyList<JsonRpcRequest> requests,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The connectors do not send batches.");
    }
}
