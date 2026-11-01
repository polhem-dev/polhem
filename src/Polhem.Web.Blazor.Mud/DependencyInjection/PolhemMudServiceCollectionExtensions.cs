using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Polhem.Web.Blazor.Mud.DependencyInjection
{
    /// <summary>
    /// Registers what the MudBlazor UI package needs.
    /// </summary>
    public static class PolhemMudServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the MudBlazor services.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns><paramref name="services"/>, for chaining.</returns>
        public static IServiceCollection AddPolhemMud(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);
            services.AddMudServices();
            return services;
        }
    }
}
