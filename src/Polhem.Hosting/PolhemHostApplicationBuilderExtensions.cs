using Microsoft.Extensions.Hosting;
using Polhem.Core;
using Polhem.Definition;

namespace Polhem.Hosting
{
    /// <summary>
    /// Brings up the Polhem backend in a host built with an <see cref="IHostApplicationBuilder"/>.
    /// </summary>
    public static class PolhemHostApplicationBuilderExtensions
    {
        /// <summary>
        /// Locates the definition folder, adds the framework's default definitions it does not have yet, reads
        /// <c>SystemSettings.xml</c>, and registers the framework with
        /// <see cref="PolhemFrameworkServiceCollectionExtensions.AddPolhemPayload"/> and
        /// <see cref="PolhemFrameworkServiceCollectionExtensions.AddPolhemFramework(Microsoft.Extensions.DependencyInjection.IServiceCollection, Polhem.Definition.Settings.BackendConfiguration, PathOptions, bool)"/>.
        /// </summary>
        /// <param name="builder">The host builder.</param>
        /// <param name="configure">Changes to the defaults of <see cref="PolhemBackendOptions"/>.</param>
        /// <returns>The paths the framework was registered with.</returns>
        /// <remarks>
        /// <para>Register the database providers and dialects before the host runs; this method does not, so a host
        /// carries only the drivers it uses. A host that serves the API over HTTP adds the JSON-RPC endpoint itself.</para>
        /// <para>This method initializes the process-wide <see cref="SysInfo"/> from the settings it reads.</para>
        /// </remarks>
        /// <exception cref="InvalidOperationException"><see cref="PolhemBackendOptions.DefinePath"/> is not set and no
        /// <c>Define/SystemSettings.xml</c> is found above <see cref="AppContext.BaseDirectory"/>.</exception>
        public static PathOptions AddPolhemBackend(this IHostApplicationBuilder builder, Action<PolhemBackendOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);

            var options = new PolhemBackendOptions();
            configure?.Invoke(options);

            var paths = new PathOptions
            {
                DefinePath = options.DefinePath ?? FindDefinePath(AppContext.BaseDirectory),
                CustomizePath = options.CustomizePath,
            };
            if (options.MaterializeDefaults)
                Defaults.MaterializeTo(paths.DefinePath, options.MaterializeOptions);

            var settings = SystemSettingsLoader.Load(paths);
            SysInfo.Initialize(settings.CommonConfiguration);

            builder.Services.AddPolhemPayload(settings.CommonConfiguration.ApiPayloadOptions, settings.CommonConfiguration.IsDebugMode);
            builder.Services.AddPolhemFramework(settings.BackendConfiguration, paths, options.AutoCreateMasterKey);
            return paths;
        }

        /// <summary>
        /// Returns the first <c>Define</c> folder holding a <c>SystemSettings.xml</c> in
        /// <paramref name="startDirectory"/> or above it.
        /// </summary>
        internal static string FindDefinePath(string startDirectory)
        {
            for (var dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "Define");
                if (File.Exists(Path.Combine(candidate, "SystemSettings.xml"))) return candidate;
            }
            throw new InvalidOperationException(
                $"No Define/SystemSettings.xml was found in or above '{startDirectory}'. Set PolhemBackendOptions.DefinePath.");
        }
    }
}
