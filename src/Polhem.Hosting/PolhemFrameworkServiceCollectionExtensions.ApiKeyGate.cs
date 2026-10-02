using Polhem.Hosting.ApiKeys;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Polhem.Hosting
{
    /// <summary>
    /// The startup check a host opts into when it serves the API over HTTP.
    /// </summary>
    public static partial class PolhemFrameworkServiceCollectionExtensions
    {
        /// <summary>
        /// Logs at startup while no API key has been issued, which leaves the <c>X-Api-Key</c> header checked for
        /// presence only: an error, or a warning when the host runs in the Development environment.
        /// </summary>
        /// <param name="services">The service collection, on which the framework is already registered.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// A host that serves the API over HTTP calls this. It is not part of
        /// <see cref="AddPolhemFramework(IServiceCollection, Polhem.Definition.Settings.BackendConfiguration, Polhem.Definition.PathOptions)"/>,
        /// because an in-process host has no HTTP endpoint for an API key to guard and would log a false alarm.
        /// The check runs when the host starts, so a host that seeds <c>st_api_key</c> before running is read
        /// after the seed. It never stops the host.
        /// </remarks>
        public static IServiceCollection AddPolhemApiKeyGateCheck(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ApiKeyGateWarningService>());
            return services;
        }
    }
}
