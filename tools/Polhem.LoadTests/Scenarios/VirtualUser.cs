using Polhem.Api.Client;
using Polhem.Api.Client.Connectors;

namespace Polhem.LoadTests.Scenarios
{
    /// <summary>
    /// One signed-in virtual user: a client of its own, holding its identity.
    /// </summary>
    public sealed class VirtualUser
    {
        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="client">This user's own client, already signed in.</param>
        public VirtualUser(PolhemApiClient client)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <summary>Gets this user's client.</summary>
        public PolhemApiClient Client { get; }

        /// <summary>Gets the access token.</summary>
        public Guid AccessToken => Client.Session.Credentials.AccessToken;

        /// <summary>
        /// Creates a form connector for a program, calling as this user.
        /// </summary>
        /// <param name="progId">The program id.</param>
        /// <returns>A connector.</returns>
        /// <remarks>
        /// A connector is created per call rather than cached: it is a thin object over the client,
        /// and caching one per user per program would add bookkeeping that measures nothing.
        /// </remarks>
        public FormApiConnector CreateFormConnector(string progId) => Client.Form(progId);
    }
}
