using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Core.Conversion;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages;
using Polhem.Api.Core.Validator;
using Polhem.Core.Serialization;
using Polhem.Definition.Security;
using Polhem.JsonRpc.Server;

namespace Polhem.Api.Core.Dispatch
{
    /// <summary>
    /// Reads and writes the Polhem payload envelope (<see cref="ApiPayload"/>) around every call: checks that the caller
    /// may use the method with the payload format it chose, restores the request payload (decryption, decompression,
    /// replay protection), and wraps the method's result in the same format and codec.
    /// </summary>
    /// <remarks>
    /// Access is checked before anything is decrypted, so an unauthorized request costs no decryption work.
    /// </remarks>
    public sealed class PolhemPayloadFilter : IJsonRpcFilter
    {
        /// <inheritdoc/>
        public async ValueTask InvokeAsync(JsonRpcRequestContext context, JsonRpcFilterDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(next);
            var services = context.Services
                ?? throw new InvalidOperationException("A Polhem call needs the services of its scope.");
            var state = PolhemCallState.Get(context);
            var method = context.Method!.MethodInfo;
            var cancellationToken = context.CancellationToken;

            // The Polhem wire format echoes the method name in every response.
            context.ResponseMembers["method"] = JsonSerializer.SerializeToElement(context.Request.Method);

            var payload = ReadPayload(context);
            var format = payload.Format;
            ApiAccessValidator.ValidateAccess(method, new ApiCallContext(state.AccessToken, state.IsLocalCall, format),
                services.GetRequiredService<IAccessTokenValidator>());

            // Access confirmed: retrieve the encryption key and restore the payload. The frame rides inside the
            // envelope, so the replay gate can only run once the payload is decrypted.
            state.EncryptionKey = format == PayloadFormat.Encrypted
                ? services.GetRequiredService<IApiEncryptionKeyProvider>().GetKey(state.AccessToken)
                : null;
            ApiPayloadConverter.RestoreRequest(payload, format, state.EncryptionKey, ActionPayloadType.Resolve(method));
            ApiFrameGate.ValidateTimestamp(payload.Frame);
            await ApiFrameGate.ValidateSequenceAsync(method, payload.Frame, state.AccessToken, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            state.Payload = payload;

            await next(context).ConfigureAwait(false);

            // The answer is written with the codec the caller asked for, the same way it keeps the caller's format.
            var value = ApiOutputConverter.Convert(context.ReturnValue!);
            var result = new JsonRpcResult { Value = value, Codec = payload.Codec };
            ApiPayloadConverter.TransformTo(result, format, state.EncryptionKey);
            using var document = JsonDocument.Parse(JsonCodec.Serialize(result));
            context.Result = document.RootElement.Clone();

            ApiAnomalyRecorder.RecordSlow(services, context.Request.Method, state);
        }

        private static JsonRpcParams ReadPayload(JsonRpcRequestContext context)
        {
            if (context.Request.Params is not { } element) { return new JsonRpcParams(); }
            try
            {
                return JsonCodec.Deserialize<JsonRpcParams>(element.GetRawText()) ?? new JsonRpcParams();
            }
            catch (JsonException ex)
            {
                throw new InvalidParamsException($"The params of '{context.Request.Method}' are not a Polhem payload: {ex.Message}", ex);
            }
        }
    }
}
