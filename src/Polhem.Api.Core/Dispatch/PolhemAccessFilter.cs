using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Core.Validator;
using Polhem.Definition.Security;
using Polhem.JsonRpc.Payload;
using Polhem.JsonRpc.Server;

namespace Polhem.Api.Core.Dispatch
{
    /// <summary>
    /// Checks that the caller may use the method with the payload format it chose, as
    /// <see cref="Polhem.Definition.Attributes.ApiAccessControlAttribute"/> declares, and writes the parts of the Polhem
    /// wire format that are not the payload: the echoed method name, and the slow-call anomaly record.
    /// </summary>
    /// <remarks>
    /// It runs before <see cref="PolhemPayloadFilter"/>, so an unauthorized request costs no decryption work. It reads
    /// only the <c>format</c> member of the payload envelope; the envelope itself is the payload filter's.
    /// </remarks>
    public sealed class PolhemAccessFilter : IJsonRpcFilter
    {
        /// <inheritdoc/>
        public async ValueTask InvokeAsync(JsonRpcRequestContext context, JsonRpcFilterDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(next);
            var services = context.Services
                ?? throw new InvalidOperationException("A Polhem call needs the services of its scope.");
            var state = PolhemCallState.Get(context);

            ApiAccessValidator.ValidateAccess(
                context.Method!.MethodInfo,
                new ApiCallContext(state.AccessToken, state.IsLocalCall, ReadFormat(context)),
                services.GetRequiredService<IAccessTokenValidator>());

            await next(context).ConfigureAwait(false);

            ApiAnomalyRecorder.RecordSlow(services, context.Request.Method, state);
        }

        private static PayloadFormat ReadFormat(JsonRpcRequestContext context)
            => PayloadEnvelope.ReadFormat(context.Request.Params);
    }
}
