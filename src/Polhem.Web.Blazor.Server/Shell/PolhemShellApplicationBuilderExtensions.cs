using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;

namespace Polhem.Web.Blazor.Server.Shell
{
    /// <summary>
    /// Request-pipeline extensions for the Blazor Server shell.
    /// </summary>
    public static class PolhemShellApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds request localization over <see cref="PolhemShellOptions.Languages"/>, falling back to
        /// <see cref="PolhemShellOptions.DefaultCulture"/>, so each circuit starts in the language the viewer picked
        /// on the sign-in screen.
        /// </summary>
        /// <remarks>
        /// Call it before the static files and the Razor components, as any request localization. The culture comes
        /// from the query string, the request-localization cookie that
        /// <see cref="PolhemShellEndpointRouteBuilderExtensions.MapPolhemShellCulture"/> writes, and the browser's
        /// <c>Accept-Language</c>, in that order. A host that configures request localization itself does not call
        /// this; the language switch only needs that cookie to be read.
        /// </remarks>
        /// <param name="app">The application builder.</param>
        /// <returns><paramref name="app"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// <see cref="PolhemShellServiceCollectionExtensions.AddPolhemShell"/> was not called, or the languages and the
        /// default culture do not agree.
        /// </exception>
        public static IApplicationBuilder UsePolhemShellLocalization(this IApplicationBuilder app)
        {
            ArgumentNullException.ThrowIfNull(app);
            var options = PolhemShellServiceCollectionExtensions.GetRequiredOptions(app.ApplicationServices);

            string defaultCulture = options.ResolveDefaultCulture();
            var cultures = options.Languages.Select(l => CultureInfo.GetCultureInfo(l.Culture)).ToList();
            return app.UseRequestLocalization(new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture(defaultCulture),
                SupportedCultures = cultures,
                SupportedUICultures = cultures,
            });
        }
    }
}
