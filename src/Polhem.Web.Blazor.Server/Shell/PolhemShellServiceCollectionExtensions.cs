using Microsoft.Extensions.DependencyInjection;

namespace Polhem.Web.Blazor.Server.Shell
{
    /// <summary>
    /// Service-collection extensions for the Blazor Server shell.
    /// </summary>
    public static class PolhemShellServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the <see cref="PolhemShellOptions"/> the shell components and its request pipeline read.
        /// </summary>
        /// <remarks>
        /// The shell runs on the services of
        /// <see cref="Polhem.Web.Blazor.Server.DependencyInjection.PolhemBlazorServiceCollectionExtensions.AddPolhemBlazor"/>
        /// (the circuit's client and user context), which the host registers too.
        /// </remarks>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Sets the application's values; omitted, every option keeps its default.</param>
        /// <returns><paramref name="services"/>.</returns>
        public static IServiceCollection AddPolhemShell(this IServiceCollection services,
            Action<PolhemShellOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            var options = new PolhemShellOptions();
            configure?.Invoke(options);
            // Fails at startup rather than on the first request.
            options.ResolveDefaultCulture();

            services.AddSingleton(options);
            return services;
        }

        internal static PolhemShellOptions GetRequiredOptions(IServiceProvider services)
            => services.GetService<PolhemShellOptions>()
                ?? throw new InvalidOperationException(
                    "The Polhem shell is not registered. Call services.AddPolhemShell() first.");
    }
}
