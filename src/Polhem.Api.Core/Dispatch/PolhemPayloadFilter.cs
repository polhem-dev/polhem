using Polhem.Api.Core.Conversion;
using Polhem.JsonRpc.Payload.Server;
using Polhem.JsonRpc.Server;

namespace Polhem.Api.Core.Dispatch
{
    /// <summary>
    /// Reads and writes the Polhem payload envelope around every call through the payload filter of
    /// Polhem.JsonRpc.Payload.Server, with Polhem's policy: the session key, the replay rules of
    /// <see cref="Polhem.Definition.Attributes.ApiAccessControlAttribute"/>, and the wire message types.
    /// </summary>
    /// <remarks>
    /// It runs after <see cref="PolhemAccessFilter"/>, which has already checked that the caller may use the method with
    /// this payload format. Inside it, the method's return value is converted to its wire message type before the
    /// package seals it.
    /// </remarks>
    public sealed class PolhemPayloadFilter : IJsonRpcFilter
    {
        /// <inheritdoc/>
        public ValueTask InvokeAsync(JsonRpcRequestContext context, JsonRpcFilterDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(next);

            var filter = new PayloadFilter(LegacyPayloadBridge.CreateOptions(), PolhemPayloadPolicy.Instance,
                LegacyPayloadBridge.ReplayStore);
            return filter.InvokeAsync(context, async inner =>
            {
                await next(inner).ConfigureAwait(false);
                inner.ReturnValue = ApiOutputConverter.Convert(inner.ReturnValue!);
            });
        }
    }
}
