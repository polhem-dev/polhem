using Polhem.Api.Core.Transformers;
using Polhem.JsonRpc;
using Polhem.JsonRpc.Payload;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// Creates the clients the tests call through.
    /// </summary>
    internal static class TestClients
    {
        /// <summary>
        /// Gets the payload options every fake client and <see cref="FakeApiTransport"/> share, so a fake server
        /// reads and writes what the client does.
        /// </summary>
        public static PayloadOptions PayloadOptions { get; } = PolhemPayload.CreateOptions();

        /// <summary>
        /// Creates a client whose calls all go to <paramref name="transport"/>, signed in with the given credentials.
        /// </summary>
        /// <param name="transport">The transport every call goes to.</param>
        /// <param name="accessToken">The access token; <c>null</c> picks a new one.</param>
        /// <param name="key">The transmission key; <c>null</c> for none.</param>
        /// <param name="timeZoneId">The user's time zone; blank for none.</param>
        public static PolhemApiClient Fake(IJsonRpcTransport transport, Guid? accessToken = null, byte[]? key = null,
            string timeZoneId = "")
        {
            var client = PolhemApiClient.CreateWithTransport(_ => transport, payloadOptions: PayloadOptions);
            client.Session.SignIn(new ApiSessionCredentials(accessToken ?? Guid.NewGuid(), key ?? [], timeZoneId));
            return client;
        }

        /// <summary>
        /// Creates an in-process client over <paramref name="services"/>, signed in as <paramref name="accessToken"/>.
        /// </summary>
        /// <param name="services">The in-process backend.</param>
        /// <param name="accessToken">The access token; <see cref="Guid.Empty"/> for an anonymous client.</param>
        public static PolhemApiClient Local(IServiceProvider services, Guid accessToken)
        {
            var client = PolhemApiClient.CreateLocal(services);
            client.Session.SignIn(new ApiSessionCredentials(accessToken, [], string.Empty));
            return client;
        }
    }
}
