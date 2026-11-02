using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Polhem.Web.Blazor.Fluent.DependencyInjection
{
    /// <summary>
    /// Registers what the Fluent UI UI package needs.
    /// </summary>
    public static class PolhemFluentServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the Fluent UI services.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns><paramref name="services"/>, for chaining.</returns>
        public static IServiceCollection AddPolhemFluent(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);
            services.AddFluentUIComponents();
            return services;
        }
    }
}
