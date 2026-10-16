using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Contracts.Form;
using Polhem.UI.ApiBridge.Connectors;
using Polhem.UI.ApiBridge.Definitions;
using Polhem.UI.Core;
using Polhem.UI.Core.Definitions;

namespace Polhem.UI.ApiBridge.DependencyInjection
{
    /// <summary>
    /// Registers the API-backed implementations of the <c>Polhem.UI.Core</c> services.
    /// </summary>
    public static class PolhemUIApiBridgeServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="IFormApiConnectorFactory"/>, <see cref="IFormDefinitionSource"/> and
        /// <see cref="IUserContext"/> for a single-user head, reading the connection and the identity from
        /// <see cref="ClientInfo"/>.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The same service collection.</returns>
        /// <remarks>
        /// A head assigns the built provider to <see cref="UIServices.Provider"/> so controls created from markup
        /// resolve these services. Not for a host that serves several users from one process.
        /// </remarks>
        public static IServiceCollection AddPolhemUIApiBridge(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);
            services.AddSingleton<IFormApiConnectorFactory>(_ => new FormApiConnectorFactory(static () => ClientInfo.ApiClient));
            services.AddSingleton<IFormDefinitionSource>(_ => new ApiFormDefinitionSource(
                static () => ClientInfo.DefineAccess, static () => ClientInfo.DefinitionLoader));
            services.AddSingleton<IUserContext, ClientUserContext>();
            return services;
        }
    }
}
