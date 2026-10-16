using Polhem.Api.Client;
using Polhem.Api.Contracts.Form;

namespace Polhem.UI.ApiBridge.Connectors
{
    /// <summary>
    /// Creates the form connectors of an API client.
    /// </summary>
    /// <remarks>
    /// Takes the client as a delegate because the client a head calls through is replaced when the endpoint
    /// changes; a captured instance would keep calling the previous endpoint.
    /// </remarks>
    public sealed class FormApiConnectorFactory : IFormApiConnectorFactory
    {
        private readonly Func<PolhemApiClient> _client;

        /// <summary>
        /// Initializes a new instance of the <see cref="FormApiConnectorFactory"/> class.
        /// </summary>
        /// <param name="client">Returns the client to create connectors from, read on every call.</param>
        public FormApiConnectorFactory(Func<PolhemApiClient> client)
        {
            ArgumentNullException.ThrowIfNull(client);
            _client = client;
        }

        /// <inheritdoc/>
        public IFormApiConnector Create(string progId) => _client().Form(progId);
    }
}
