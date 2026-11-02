using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Client;
using Polhem.Web.Blazor.DependencyInjection;

namespace Polhem.Web.Blazor.Wasm.DependencyInjection
{
    /// <summary>
    /// Service-collection extensions for the Blazor WebAssembly host.
    /// </summary>
    public static class PolhemBlazorServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the services the components read, over a <see cref="PolhemApiClient"/> that calls the endpoint in
        /// <see cref="PolhemBlazorOptions"/>.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Sets the endpoint and the other options.</param>
        /// <returns><paramref name="services"/>.</returns>
        /// <exception cref="ArgumentException"><see cref="PolhemBlazorOptions.Endpoint"/> was left empty.</exception>
        public static IServiceCollection AddPolhemBlazor(this IServiceCollection services,
            Action<PolhemBlazorOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            var options = new PolhemBlazorOptions();
            configure(options);
            ArgumentException.ThrowIfNullOrWhiteSpace(options.Endpoint, nameof(PolhemBlazorOptions.Endpoint));
            ArgumentNullException.ThrowIfNull(options.ApiKey, nameof(PolhemBlazorOptions.ApiKey));

            services.AddSingleton(options);
            return services.AddPolhemBlazorClient(options,
                _ => PolhemApiClient.CreateRemote(options.Endpoint, options.ApiKey));
        }
    }
}
