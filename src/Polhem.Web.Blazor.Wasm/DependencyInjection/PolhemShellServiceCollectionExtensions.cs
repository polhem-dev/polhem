using Microsoft.Extensions.DependencyInjection;
using Polhem.Web.Blazor.Shell;
using Polhem.Web.Blazor.Wasm.Shell;

namespace Polhem.Web.Blazor.Wasm.DependencyInjection
{
    /// <summary>
    /// Service-collection extensions for the Blazor WebAssembly shell.
    /// </summary>
    public static class PolhemShellServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the <see cref="PolhemShellOptions"/> the shell components read, and the language switch that
        /// stores the choice in the browser.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Sets the application's values; omitted, every option keeps its default.</param>
        /// <returns><paramref name="services"/>.</returns>
        public static IServiceCollection AddPolhemShell(this IServiceCollection services,
            Action<PolhemShellOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            var options = new PolhemShellOptions();
            configure?.Invoke(options);
            // Fails at startup rather than on the first switch.
            options.ResolveDefaultCulture();

            services.AddSingleton(options);
            services.AddScoped<IShellLanguageSwitcher, ShellLanguageSwitcher>();
            return services;
        }
    }
}
