using System.Reflection;
using Polhem.Api.Core.Validator;
using Polhem.JsonRpc.Server;

namespace Polhem.Api.Core.Dispatch
{
    /// <summary>
    /// Admits a business-object method when it, its base definition or its class carries
    /// <see cref="Polhem.Definition.Attributes.ApiAccessControlAttribute"/>.
    /// </summary>
    /// <remarks>
    /// Whether the caller may use the method with the payload format it chose is checked later, by
    /// <see cref="PolhemPayloadFilter"/>, once the format is known.
    /// </remarks>
    public sealed class PolhemMethodPolicy : IJsonRpcMethodPolicy
    {
        /// <inheritdoc/>
        public bool IsCallable(MethodInfo method)
        {
            ArgumentNullException.ThrowIfNull(method);
            return ApiAccessValidator.FindAccessControl(method) != null;
        }
    }
}
