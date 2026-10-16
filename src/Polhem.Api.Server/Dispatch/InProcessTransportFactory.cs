using Polhem.Api.Core.JsonRpc;
using Polhem.JsonRpc;
using Polhem.JsonRpc.Server;

namespace Polhem.Api.Server.Dispatch
{
    /// <summary>
    /// Sends an in-process client's calls to the <see cref="JsonRpcDispatcher"/> of the same process through an
    /// <see cref="InProcessTransport"/>, which marks each call as local and carries the access token in
    /// <see cref="PolhemJsonRpc.AccessTokenItem"/>.
    /// </summary>
    /// <remarks>
    /// The call runs on the caller's thread, and business object methods are synchronous in 1.0 (ADR-046), so a
    /// cancellation token is observed until the method is dispatched and not while it runs.
    /// </remarks>
    public sealed class InProcessTransportFactory : IInProcessTransportFactory
    {
        /// <inheritdoc />
        public IJsonRpcTransport Create(IServiceProvider services, Guid accessToken)
        {
            ArgumentNullException.ThrowIfNull(services);
            var dispatcher = services.GetService(typeof(JsonRpcDispatcher)) as JsonRpcDispatcher
                ?? throw new InvalidOperationException(
                    "JsonRpcDispatcher is not registered in the service provider of this in-process call. " +
                    "Local API calls need the provider built from services.AddPolhemFramework(...).");
            var transport = new InProcessTransport(dispatcher, services);
            transport.Items[PolhemJsonRpc.AccessTokenItem] = accessToken;
            return transport;
        }
    }
}
