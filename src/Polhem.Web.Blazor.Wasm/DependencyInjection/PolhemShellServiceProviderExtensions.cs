using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Polhem.Web.Blazor.Shell;
using Polhem.Web.Blazor.Wasm.Shell;

namespace Polhem.Web.Blazor.Wasm.DependencyInjection
{
    /// <summary>
    /// Startup extensions for the Blazor WebAssembly shell.
    /// </summary>
    public static class PolhemShellServiceProviderExtensions
    {
        /// <summary>
        /// Starts the page in the language the viewer picked on the sign-in screen, then in the browser's language
        /// when <see cref="PolhemShellOptions.Languages"/> has it, then in
        /// <see cref="PolhemShellOptions.DefaultCulture"/>.
        /// </summary>
        /// <remarks>
        /// Call it on the built host's <c>Services</c>, before <c>RunAsync</c>: the host loads the satellite assemblies
        /// of the culture that is current when it runs. The package README lists the project settings the languages need.
        /// </remarks>
        /// <param name="services">The host's services.</param>
        /// <exception cref="InvalidOperationException">
        /// <see cref="PolhemShellServiceCollectionExtensions.AddPolhemShell"/> was not called.
        /// </exception>
        public static void ApplyPolhemShellCulture(this IServiceProvider services)
        {
            ArgumentNullException.ThrowIfNull(services);
            var options = services.GetService<PolhemShellOptions>()
                ?? throw new InvalidOperationException(
                    "The Polhem shell is not registered. Call services.AddPolhemShell() first.");
            var js = (IJSInProcessRuntime)services.GetRequiredService<IJSRuntime>();

            string? stored = js.Invoke<string?>("localStorage.getItem", ShellLanguageSwitcher.StorageKey);
            var culture = CultureInfo.GetCultureInfo(
                SelectCulture(options, stored, CultureInfo.CurrentUICulture));

            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            js.InvokeVoid("document.documentElement.setAttribute", "lang", culture.Name);
        }

        /// <summary>
        /// Picks the culture to start in. A stored value that is no longer one of the languages is ignored, and a
        /// browser language falls back through its parents, as request localization does on Blazor Server.
        /// </summary>
        internal static string SelectCulture(PolhemShellOptions options, string? stored, CultureInfo browser)
        {
            if (FindLanguage(options, stored) is { } picked)
                return picked;
            for (var culture = browser; culture.Name.Length > 0; culture = culture.Parent)
            {
                if (FindLanguage(options, culture.Name) is { } offered)
                    return offered;
            }
            return options.ResolveDefaultCulture();
        }

        private static string? FindLanguage(PolhemShellOptions options, string? culture)
            => options.Languages.FirstOrDefault(
                l => string.Equals(l.Culture, culture, StringComparison.OrdinalIgnoreCase))?.Culture;
    }
}
