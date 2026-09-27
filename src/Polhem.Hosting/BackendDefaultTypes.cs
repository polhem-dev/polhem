namespace Polhem.Hosting
{
    /// <summary>
    /// The type names the composition root falls back to when a
    /// <see cref="Polhem.Definition.Settings.BackendComponents"/> entry is left blank.
    /// </summary>
    /// <remarks>
    /// Strings rather than <c>typeof</c>: each default is loaded through the same path as a
    /// configured name, so it exercises the code a deployment's override would.
    /// <c>BackendDefaultTypesGateTests</c> checks that every entry resolves.
    /// <see cref="Polhem.Definition.Settings.BackendComponents.CacheProvider"/> has no entry here:
    /// <see cref="Polhem.ObjectCaching.CacheInfo.Initialize"/> owns its default.
    /// </remarks>
    internal static class BackendDefaultTypes
    {
        // ---------------- Providers ----------------
        /// <summary>
        /// Default API encryption key provider type. The deriving provider is the default because
        /// its key survives cache eviction and is identical on every node, which session rebuild
        /// depends on; it requires <see cref="Polhem.Definition.Settings.SecurityKeySettings.ApiEncryptionKey"/> to be configured.
        /// </summary>
        public const string ApiEncryptionKeyProvider = "Polhem.Business.Providers.DerivedApiEncryptionKeyProvider, Polhem.Business";
        /// <summary>
        /// Default access token validator, used to verify the validity of access tokens.
        /// </summary>
        public const string AccessTokenValidator = "Polhem.Business.Validator.AccessTokenValidator, Polhem.Business";
        // ---------------- Cache ----------------
        /// <summary>
        /// Default cache data source provider type.
        /// </summary>
        public const string CacheDataSourceProvider = "Polhem.Business.Providers.CacheDataSourceProvider, Polhem.Business";

        // ---------------- Define ----------------
        /// <summary>
        /// Default define storage type.
        /// </summary>
        public const string DefineStorage = "Polhem.Definition.Storage.FileDefineStorage, Polhem.Definition";
        /// <summary>
        /// Default define access type.
        /// </summary>
        public const string DefineAccess = "Polhem.ObjectCaching.CacheDefineAccess, Polhem.ObjectCaching";

        // ---------------- Services ----------------
        /// <summary>
        /// Default session info service type.
        /// </summary>
        public const string SessionInfoService = "Polhem.ObjectCaching.Services.SessionInfoService, Polhem.ObjectCaching";
        /// <summary>
        /// Default company info service type.
        /// </summary>
        public const string CompanyInfoService = "Polhem.ObjectCaching.Services.CompanyInfoService, Polhem.ObjectCaching";

        // ---------------- Repository ----------------
        /// <summary>
        /// Default repository factory type, used for creating every repository on both axes.
        /// </summary>
        public const string RepositoryFactory = "Polhem.Repository.Factories.RepositoryFactory, Polhem.Repository";
    }
}
