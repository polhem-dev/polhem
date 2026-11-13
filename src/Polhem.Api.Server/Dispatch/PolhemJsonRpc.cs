using Polhem.JsonRpc.Server;

namespace Polhem.Api.Server.Dispatch
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
        /// <remarks>
        /// <para>
        /// Batch requests are refused (<c>MaxBatchSize</c> is 0). A host that serves batches sets a size through
        /// <c>AddJsonRpcServer</c>, and its rate limit then has to count calls rather than HTTP requests.
        /// </para>
        /// <para>
        /// IMPORTANT: a host that assembles its own options keeps <see cref="PolhemAccessFilter"/> ahead of
        /// <see cref="PolhemPayloadFilter"/>. The access check is what keeps a request the caller may not make from
        /// reaching decryption and deserialization.
        /// </para>
        /// </remarks>
        public static JsonRpcServerOptions CreateServerOptions()
        {
            var options = new JsonRpcServerOptions
            {
                ObjectFactory = new PolhemObjectFactory(),
                MethodPolicy = new PolhemMethodPolicy(),
                ParameterBinder = new PolhemParameterBinder(),
                ExceptionMapper = PolhemExceptionMapper.Map,
                // IMPORTANT: batches are refused. A rate limit counts HTTP requests, and one batch carried many
                // anonymous sign-ins, each a full password key derivation. The framework's clients send no batch;
                // a host that needs them sets `MaxBatchSize` through `AddJsonRpcServer`.
                MaxBatchSize = 0,
            };
            options.Filters.Add(new PolhemAccessFilter());
            options.Filters.Add(new PolhemPayloadFilter());
            return options;
        }
    }
}
