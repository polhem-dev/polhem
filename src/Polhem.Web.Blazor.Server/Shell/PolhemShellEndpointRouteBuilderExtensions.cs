using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Routing;

namespace Polhem.Web.Blazor.Server.Shell
{
    /// <summary>
    /// Endpoint-routing extensions for the Blazor Server shell.
    /// </summary>
    public static class PolhemShellEndpointRouteBuilderExtensions
    {
        /// <summary>
        /// Maps <c>GET /culture</c>, the endpoint behind the sign-in screen's language switch: it stores the chosen
        /// culture in the request-localization cookie and redirects back to the page the switch was on.
        /// </summary>
        /// <remarks>
        /// A circuit's culture is fixed when it connects, so the switch reloads the page through this endpoint. The
        /// endpoint accepts only a culture of <see cref="PolhemShellOptions.Languages"/>, and redirects only to a path
        /// on this site; anything else returns to the site's root. The cookie is read by the request localization
        /// that <see cref="PolhemShellApplicationBuilderExtensions.UsePolhemShellLocalization"/> sets up.
        /// </remarks>
        /// <param name="endpoints">The application's endpoint route builder.</param>
        /// <returns>The endpoint's convention builder.</returns>
        /// <exception cref="InvalidOperationException">
        /// <see cref="PolhemShellServiceCollectionExtensions.AddPolhemShell"/> was not called.
        /// </exception>
        public static IEndpointConventionBuilder MapPolhemShellCulture(this IEndpointRouteBuilder endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);
            var options = PolhemShellServiceCollectionExtensions.GetRequiredOptions(endpoints.ServiceProvider);

            return endpoints.MapGet("/" + PolhemShellCulture.Path, (string? culture, string? redirectUri, HttpContext context) =>
            {
                if (options.FindLanguage(culture) is { } language)
                {
                    context.Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName,
                        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(language.Culture)),
                        new CookieOptions
                        {
                            Expires = DateTimeOffset.UtcNow.AddYears(1),
                            IsEssential = true,
                            HttpOnly = true,
                            Secure = context.Request.IsHttps,
                            SameSite = SameSiteMode.Lax,
                        });
                }
                return Results.LocalRedirect(PolhemShellCulture.IsLocalPath(redirectUri) ? redirectUri! : "/");
            });
        }
    }
}
