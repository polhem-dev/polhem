using Polhem.Definition;

namespace Polhem.Hosting
{
    /// <summary>
    /// Options for <see cref="PolhemHostApplicationBuilderExtensions.AddPolhemBackend"/>.
    /// </summary>
    public sealed class PolhemBackendOptions
    {
        /// <summary>
        /// Gets or sets the definition folder. When <c>null</c>, the first <c>Define</c> folder that holds a
        /// <c>SystemSettings.xml</c>, walking up from <see cref="AppContext.BaseDirectory"/>, is used, so the host finds
        /// the same folder whether it runs from its build output or from the solution.
        /// </summary>
        public string? DefinePath { get; set; }

        /// <summary>
        /// Gets or sets the root of the tenant customization layer (<see cref="PathOptions.CustomizePath"/>).
        /// Empty, the default, turns the layer off.
        /// </summary>
        public string CustomizePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the framework's default definitions that the definition folder does not have yet are
        /// written into it before the settings are read (<see cref="Defaults.MaterializeTo"/>). Files already there
        /// are left as they are. The default is <c>true</c>.
        /// </summary>
        public bool MaterializeDefaults { get; set; } = true;

        /// <summary>
        /// Gets or sets the options passed to <see cref="Defaults.MaterializeTo"/>, for example a filter that takes
        /// only some of the defaults. <c>null</c> takes them all.
        /// </summary>
        public MaterializeOptions? MaterializeOptions { get; set; }

        /// <summary>
        /// Gets or sets whether a master key is created when the configured source has none. See the
        /// <c>autoCreateMasterKey</c> parameter of
        /// <see cref="PolhemFrameworkServiceCollectionExtensions.AddPolhemFramework(Microsoft.Extensions.DependencyInjection.IServiceCollection, Polhem.Definition.Settings.BackendConfiguration, PathOptions, bool)"/>
        /// for what that means for each source. The default is <c>false</c>.
        /// </summary>
        public bool AutoCreateMasterKey { get; set; }
    }
}
