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
        /// Creates server options with the Polhem object factory, method policy, access and payload filters, parameter
        /// binder and error contract.
        /// </summary>
        /// <returns>The options.</returns>
        public static JsonRpcServerOptions CreateServerOptions()
        {
            var options = new JsonRpcServerOptions
            {
                ObjectFactory = new PolhemObjectFactory(),
                MethodPolicy = new PolhemMethodPolicy(),
                ParameterBinder = new PolhemParameterBinder(),
                ExceptionMapper = PolhemExceptionMapper.Map,
            };
            options.Filters.Add(new PolhemAccessFilter());
            options.Filters.Add(new PolhemPayloadFilter());
            return options;
        }
    }
}
