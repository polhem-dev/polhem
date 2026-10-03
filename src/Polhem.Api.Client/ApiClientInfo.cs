using Polhem.Api.Core.Transformers;

namespace Polhem.Api.Client
{
    /// <summary>
    /// API client runtime information; shared connection parameters and settings for the API client,
    /// across WinForms, Web, and App targets. Provides the client-side counterpart to the backend's
    /// DI-registered services (see <c>Polhem.Hosting.PolhemFrameworkServiceCollectionExtensions.AddPolhemFramework</c>).
    /// Contains only application-level and connection settings; does not hold user session state.
    /// </summary>
    public static class ApiClientInfo
    {

        /// <summary>
        /// Gets or sets the connection types supported by the application.
        /// </summary>
        public static SupportedConnectTypes SupportedConnectTypes { get; set; } = SupportedConnectTypes.Both;

        /// <summary>
        /// Gets or sets the active service connection type.
        /// </summary>
        public static ConnectType ConnectType { get; set; } = ConnectType.Local;

        /// <summary>
        /// Gets or sets the API service endpoint, typically loaded from configuration.
        /// </summary>
        public static string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the API key, typically loaded from configuration.
        /// </summary>
        public static string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the deployment's default language, the last hop of the language fall-back
        /// chain on this client.
        /// </summary>
        /// <remarks>
        /// <see cref="Connectors.SystemApiConnector.InitializeAsync"/> sets it from the server's
        /// <see cref="Polhem.Definition.Settings.CommonConfiguration.DefaultLanguage"/>. It is a
        /// deployment setting rather than a user's, which is why it lives with the connection
        /// settings; the signed-in user's own culture arrives with the login response.
        /// </remarks>
        public static string DefaultLanguage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the payload options every connector of this client reads and writes the payload envelope with.
        /// </summary>
        /// <remarks>
        /// <see cref="Connectors.SystemApiConnector.InitializeAsync"/> applies the server's compressor and encryptor to this
        /// instance and leaves the rest as it is, so a setting made before it, such as
        /// <see cref="Polhem.JsonRpc.Payload.PayloadOptions.RequireFrame"/>, survives. The server must use the same frame
        /// setting.
        /// </remarks>
        public static Polhem.JsonRpc.Payload.PayloadOptions PayloadOptions { get; set; } = PolhemPayload.CreateOptions();
    }
}
