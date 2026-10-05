using System.Text.Json;
using Polhem.Api.Core.Conversion;
using Polhem.Api.Core.JsonRpc;
using Polhem.JsonRpc.Payload.Server;
using Polhem.JsonRpc.Server;

namespace Polhem.Api.Core.Dispatch
{
    /// <summary>
    /// Binds the restored value of the Polhem payload to the business-object method's parameter.
    /// </summary>
    /// <remarks>
    /// A plain body arrives as JSON and is read into the method's payload type; a decoded body is already an object.
    /// Either is then converted to the parameter type, which may be a business-layer argument type rather than the API
    /// message (adr-007).
    /// </remarks>
    public sealed class PolhemParameterBinder : IJsonRpcParameterBinder
    {
        /// <inheritdoc/>
        public object? Bind(JsonRpcRequestContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            var method = context.Method!.MethodInfo;
            object? value = PayloadRequest.Find(context)?.Value;

            // Every action takes one argument object, so a call without a value has nothing to bind. A request
            // without `params` reads as an empty plain envelope and lands here too; it is answered as the
            // dispatcher's own binder answers it, rather than failing inside the business object.
            if (value is null or JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined })
                throw new InvalidParamsException($"'{method.Name}' was called without a value to bind.");

            if (value is JsonElement element)
            {
                try
                {
                    value = element.Deserialize(ActionPayloadType.Resolve(method), ApiInputConverter.PlainReadOptions);
                }
                catch (JsonException ex)
                {
                    throw new InvalidParamsException($"The body of '{method.Name}' could not be read: {ex.Message}", ex);
                }
            }

            return ApiInputConverter.Convert(value!, context.Method.ParameterType);
        }
    }
}
