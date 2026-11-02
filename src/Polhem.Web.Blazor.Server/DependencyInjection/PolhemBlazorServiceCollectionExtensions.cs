using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Client;
using Polhem.JsonRpc.Payload;
using Polhem.Web.Blazor.DependencyInjection;

namespace Polhem.Web.Blazor.Server.DependencyInjection
{
    /// <summary>
    /// Service-collection extensions for the Blazor Server component library.
    /// </summary>
    public static class PolhemBlazorServiceCollectionExtensions
    {
        /// <summary>
        /// Registers Polhem Blazor Server services: the resolved
        /// <see cref="PolhemBlazorOptions"/>, a <see cref="PolhemApiClient"/> per circuit, created with the
        /// configured provider, a <see cref="Polhem.Web.Blazor.Identity.BlazorUserContext"/> per circuit, also as its
        /// <see cref="Polhem.UI.Core.IUserContext"/>, and the
        /// <see cref="Microsoft.Extensions.Localization.IStringLocalizer{T}"/> of <see cref="Polhem.Definition.Language.PolhemUIText"/> the components read their
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
            services.AddPolhemBlazorClient(options, sp => options.Mode == PolhemBlazorProviderMode.Local
                // In process the client speaks the payload options the backend registered, so a debug host that
                // encodes its calls uses the same compressor and encryptor on both ends.
                ? PolhemApiClient.CreateLocal(sp, sp.GetService<PayloadOptions>())
                : PolhemApiClient.CreateRemote(options.Endpoint, options.ApiKey));
            return services;
        }
    }
}
