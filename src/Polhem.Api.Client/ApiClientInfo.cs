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

    }
}
