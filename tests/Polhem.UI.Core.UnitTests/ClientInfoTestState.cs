using System.Reflection;
using Polhem.Api.Client;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// Reaches the process-wide state of <see cref="ClientInfo"/> that has no public setter, so a test can replace
    /// it and put it back.
    /// </summary>
    internal static class ClientInfoTestState
    {
        private static readonly FieldInfo s_client = Field("s_client");
        private static readonly FieldInfo s_apiKey = Field("s_apiKey");
        private static readonly FieldInfo s_defineAccess = Field("s_defineAccess");
        private static readonly FieldInfo s_definitionLoader = Field("s_definitionLoader");

        private static FieldInfo Field(string name)
            => typeof(ClientInfo).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException($"ClientInfo has no field '{name}'.");

        /// <summary>
        /// Gets or sets the API key <see cref="ClientInfo"/> gives the clients it creates.
        /// </summary>
        public static string ApiKey
        {
            get => (string)s_apiKey.GetValue(null)!;
            set => s_apiKey.SetValue(null, value);
        }

        /// <summary>
        /// Makes <paramref name="client"/> the client of <see cref="ClientInfo"/>; <c>null</c> lets it create its default.
        /// </summary>
        public static void UseClient(PolhemApiClient? client) => s_client.SetValue(null, client);

        /// <summary>
        /// Captures the client, the API key, the allowed connection types and the definition caches, and restores them
        /// when disposed.
        /// </summary>
        public static IDisposable Preserve() => new Snapshot();

        private sealed class Snapshot : IDisposable
        {
            private readonly object? _client = s_client.GetValue(null);
            private readonly object? _apiKey = s_apiKey.GetValue(null);
            private readonly object? _defineAccess = s_defineAccess.GetValue(null);
            private readonly object? _definitionLoader = s_definitionLoader.GetValue(null);
            private readonly SupportedConnectTypes _supported = ClientInfo.SupportedConnectTypes;

            public void Dispose()
            {
                s_client.SetValue(null, _client);
                s_apiKey.SetValue(null, _apiKey);
                s_defineAccess.SetValue(null, _defineAccess);
                s_definitionLoader.SetValue(null, _definitionLoader);
                ClientInfo.SupportedConnectTypes = _supported;
            }
        }
    }
}
