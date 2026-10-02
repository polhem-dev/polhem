using System.Text.Json;
using Polhem.Api.Core.Conversion;
using Polhem.Api.Core.JsonRpc;
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
            object? value = PolhemCallState.Get(context).Payload?.Value;

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

            return value == null ? null : ApiInputConverter.Convert(value, context.Method.ParameterType);
        }
    }
}
