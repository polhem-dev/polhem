using Polhem.Api.Core.JsonRpc;
using Polhem.JsonRpc.Server;

namespace Polhem.Api.Core.Dispatch
{
    /// <summary>
    /// Sets up the JSON-RPC dispatcher the way the Polhem framework serves its API.
    /// </summary>
    public static class PolhemJsonRpc
    {
        /// <summary>
        /// The key of <see cref="JsonRpcTransportInfo.Items"/> under which an in-process caller passes its access token
        /// (a <see cref="Guid"/>).
        /// </summary>
        public const string AccessTokenItem = "Polhem.AccessToken";

        /// <summary>
        /// Creates server options with the Polhem object factory, method policy, payload filter, parameter binder and
        /// error contract.
        /// </summary>
        /// <returns>The options.</returns>
        /// <remarks>
        /// The internal error code is the Polhem framework's <see cref="JsonRpcErrorCode.InternalError"/>, which its
        /// clients expect.
        /// </remarks>
        public static JsonRpcServerOptions CreateServerOptions()
        {
            var options = new JsonRpcServerOptions
            {
                ObjectFactory = new PolhemObjectFactory(),
                MethodPolicy = new PolhemMethodPolicy(),
                ParameterBinder = new PolhemParameterBinder(),
                ExceptionMapper = PolhemExceptionMapper.Map,
                InternalErrorCode = (int)JsonRpcErrorCode.InternalError,
            };
            options.Filters.Add(new PolhemPayloadFilter());
            return options;
        }
    }
}
