using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Polhem.Api.Client;
using Polhem.Definition.Language;
using Polhem.UI.Core;
using Polhem.Web.Blazor.Components;
using Polhem.Web.Blazor.Identity;

namespace Polhem.Web.Blazor.DependencyInjection
{
    /// <summary>
    /// The registrations every Blazor host package shares.
    /// </summary>
    public static class PolhemBlazorClientServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <paramref name="options"/>, a scoped <see cref="PolhemApiClient"/>, a scoped
        /// <see cref="BlazorUserContext"/>, also as its <see cref="IUserContext"/>, and the
        /// <see cref="IStringLocalizer{T}"/> of <see cref="PolhemUIText"/> the components read their own text from.
        /// </summary>
        /// <remarks>
        /// A host package calls this from its own <c>AddPolhemBlazor</c>, which decides how the client reaches the
        /// backend. An application calls the host package's method.
        /// </remarks>
        /// <param name="services">The service collection.</param>
        /// <param name="options">The host's settings for how components assemble definitions.</param>
        /// <param name="clientFactory">Creates the client of one scope.</param>
        /// <returns><paramref name="services"/>.</returns>
        public static IServiceCollection AddPolhemBlazorClient(this IServiceCollection services,
            IBlazorDefinitionOptions options, Func<IServiceProvider, PolhemApiClient> clientFactory)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(clientFactory);

            services.AddSingleton(options);
            // WARNING: scoped, not singleton. A client holds one signed-in identity, so one per circuit is what keeps
            // one user's token and transmission key out of another's requests. A singleton would make the last
            // sign-in everyone's on Blazor Server.
            services.AddScoped(clientFactory);
            // Scoped for the same reason as the client. TryAdd for the interface, so a host can supply its own
            // context; the pages read the interface.
            services.AddScoped(sp => new BlazorUserContext(sp.GetRequiredService<PolhemApiClient>(), options.DefaultLanguage));
            services.TryAddScoped<IUserContext>(sp => sp.GetRequiredService<BlazorUserContext>());
            // The components' own text. TryAdd, so a host that registered its own localizer for
            // PolhemUIText — before or instead of this call — keeps it. The default answers from the
            // host's language resources when an in-process backend registered them, then from the
            // translations shipped with the framework, in the current UI culture.
            services.TryAddSingleton<IStringLocalizer<PolhemUIText>>(sp =>
            {
                var hostService = sp.GetService<ILanguageService>();
                var service = hostService is null
                    ? new FrameworkLanguageService(null, () => options.DefaultLanguage)
                    : new FrameworkLanguageService(hostService);
                return new LanguageResourceStringLocalizer<PolhemUIText>(service);
            });
            return services;
        }
    }
}
