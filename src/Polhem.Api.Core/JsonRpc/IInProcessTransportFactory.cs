using Polhem.JsonRpc;

namespace Polhem.Api.Core.JsonRpc
{
    /// <summary>
    /// Creates the transport an in-process client sends its calls through, straight to the backend's dispatcher in
    /// the same process.
    /// </summary>
    /// <remarks>
    /// The client resolves this from the backend's service provider, so it never references the server side
    /// itself: <c>AddPolhemFramework</c> (Polhem.Hosting) registers the implementation from Polhem.Api.Server.
    /// </remarks>
    public interface IInProcessTransportFactory
    {
        /// <summary>
        /// Creates the transport of one call.
        /// </summary>
        /// <param name="services">The service provider the call is dispatched with.</param>
        /// <param name="accessToken">The caller's access token; <see cref="Guid.Empty"/> before sign-in.</param>
        /// <returns>The transport.</returns>
        IJsonRpcTransport Create(IServiceProvider services, Guid accessToken);
    }
}
