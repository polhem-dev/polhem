using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Polhem.Api.Client;
using Polhem.Definition.Language;

namespace Polhem.Web.Blazor.Server.DependencyInjection
{
    /// <summary>
    /// Service-collection extensions for the Blazor Server component library.
    /// </summary>
    public static class PolhemBlazorServiceCollectionExtensions
    {
        /// <summary>
        /// Registers Polhem Blazor Server services: the resolved
        /// <see cref="PolhemBlazorOptions"/>, a <see cref="PolhemApiConnectorFactory"/>
        /// that hosts inject to build connectors with the configured provider, and the
        /// <see cref="IStringLocalizer{T}"/> of <see cref="PolhemUIText"/> the components read their
        /// own text from.
        /// </summary>
        /// <remarks>
        /// This call deliberately does <em>not</em> bundle <c>AddPolhemFramework</c>:
        /// Blazor Server hosts that want in-process backend dispatch must call
        /// <c>AddPolhemFramework</c> separately so they keep full control over the
        /// backend composition root (Polhem.Hosting stays the single composition
        /// authority).
        /// </remarks>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">
        /// Optional configuration callback. When omitted, the default mode is
        /// <see cref="PolhemBlazorProviderMode.Local"/> (in-process backend).
        /// </param>
        public static IServiceCollection AddPolhemBlazor(
            this IServiceCollection services,
            Action<PolhemBlazorOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            var options = new PolhemBlazorOptions();
            configure?.Invoke(options);

            services.AddSingleton(options);
            // Scoped, not singleton: one ApiSessionContext per circuit is what keeps one user's
            // transmission key out of another's requests. See PolhemApiConnectorFactory's remarks.
            services.AddScoped<Polhem.Api.Client.ApiSessionContext>();
            services.AddScoped<PolhemApiConnectorFactory>();
            // The components' own text. TryAdd, so a host that registered its own localizer for
            // PolhemUIText — before or instead of this call — keeps it. The default answers from the
            // host's language resources when an in-process backend registered them, then from the
            // translations shipped with the framework, in the circuit's CurrentUICulture.
            services.TryAddSingleton<IStringLocalizer<PolhemUIText>>(sp =>
            {
                var hostService = sp.GetService<ILanguageService>();
                var service = hostService is null
                    ? new FrameworkLanguageService(null, static () => ApiClientInfo.DefaultLanguage)
                    : new FrameworkLanguageService(hostService);
                return new LanguageResourceStringLocalizer<PolhemUIText>(service);
            });
            return services;
        }
    }
}
