using System.ComponentModel;
using Polhem.Base.Attributes;

namespace Polhem.Definition.Settings
{
    /// <summary>
    /// Settings for replaceable backend components, defining the type names for various backend services.
    /// </summary>
    /// <remarks>
    /// Each entry is an assembly-qualified type name. A blank entry selects the framework default,
    /// which the composition root (<c>Polhem.Hosting</c>) owns: the defaults name types in assemblies
    /// above this one.
    /// </remarks>
    [Description("Settings for replaceable backend components, defining the type names for various backend services.")]
    [TreeNode("Components")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class BackendComponents
    {
        /// <summary>
        /// API encryption key provider type.
        /// </summary>
        [Category("Providers")]
        [Description("API encryption key provider type, defines how to obtain the API data encryption key.")]
        [DefaultValue("")]
        public string ApiEncryptionKeyProvider { get; set; } = string.Empty;

        /// <summary>
        /// Access token validator type.
        /// </summary>
        [Category("Validators")]
        [Description("Access token validator type, used to verify the validity of access tokens.")]
        [DefaultValue("")]
        public string AccessTokenValidator { get; set; } = string.Empty;

        /// <summary>
        /// Cache provider type.
        /// </summary>
        [Category("Cache")]
        [Description("Cache provider type, defines the cache mechanism implementation (e.g., MemoryCache, Redis).")]
        [DefaultValue("")]
        public string CacheProvider { get; set; } = string.Empty;

        /// <summary>
        /// Cache data source provider type.
        /// </summary>
        [Category("Cache")]
        [Description("Cache data source provider type, defines the source of cached data (such as preloaded definition data).")]
        [DefaultValue("")]
        public string CacheDataSourceProvider { get; set; } = string.Empty;

        /// <summary>
        /// Define storage type.
        /// </summary>
        [Category("Define")]
        [Description("Define storage type, specifies how to load system definition files (e.g., file, database, etc.).")]
        [DefaultValue("")]
        public string DefineStorage { get; set; } = string.Empty;

        /// <summary>
        /// Define access type.
        /// </summary>
        [Category("Define")]
        [Description("Define access type.")]
        [DefaultValue("")]
        public string DefineAccess { get; set; } = string.Empty;

        /// <summary>
        /// Session info service type.
        /// </summary>
        [Category("Service")]
        [Description("Session info service type.")]
        [DefaultValue("")]
        public string SessionInfoService { get; set; } = string.Empty;

        /// <summary>
        /// Company info service type.
        /// </summary>
        [Category("Service")]
        [Description("Company info service type.")]
        [DefaultValue("")]
        public string CompanyInfoService { get; set; } = string.Empty;

        /// <summary>
        /// Repository factory type.
        /// </summary>
        /// <remarks>
        /// One entry for both axes: the same factory serves progId-bound repositories and framework
        /// ones, so replacing it replaces all repository creation at once. It used to take two
        /// entries, which meant a host overriding one silently kept the framework's own for the other.
        /// </remarks>
        [Category("Repository")]
        [Description("Repository factory type, defines how every repository is created.")]
        [DefaultValue("")]
        public string RepositoryFactory { get; set; } = string.Empty;
    }
}
