using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Polhem.Api.Client;
using Polhem.Definition.Language;

namespace Polhem.Web.Blazor.Server.Components
{
    /// <summary>
    /// Resolves the text the components of this package render, through the
    /// <see cref="IStringLocalizer{T}"/> of <see cref="PolhemUIText"/> registered in the container.
    /// </summary>
    /// <remarks>
    /// <c>AddPolhemBlazor</c> registers that localizer; a component rendered without it — a test,
    /// or a host that composes its own container — falls back to one over the framework's shipped
    /// translations, so the components never fail for want of a localizer.
    /// </remarks>
    internal static class PolhemBlazorText
    {
        private static readonly IStringLocalizer s_fallback =
            new LanguageResourceStringLocalizer<PolhemUIText>(
                new FrameworkLanguageService(null, static () => ApiClientInfo.DefaultLanguage));

        /// <summary>
        /// Returns the localizer registered in <paramref name="services"/>, else the fallback.
        /// </summary>
        /// <param name="services">
        /// The component's service provider; <c>null</c> when the component was created outside a
        /// renderer.
        /// </param>
        internal static IStringLocalizer GetLocalizer(IServiceProvider? services)
            => services?.GetService<IStringLocalizer<PolhemUIText>>() ?? s_fallback;
    }
}
