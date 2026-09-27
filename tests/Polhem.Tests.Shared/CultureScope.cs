using System.Globalization;

namespace Polhem.Tests.Shared
{
    /// <summary>
    /// Sets the current culture and UI culture of a test's flow and restores both on dispose.
    /// </summary>
    /// <remarks>
    /// Display text and formatting follow the user's culture, so a test that asserts rendered text
    /// pins one instead of inheriting the culture of the machine it runs on. Both properties flow with
    /// the execution context, so a scope changes nothing for tests running in parallel; it does not
    /// touch the process-wide <see cref="CultureInfo.DefaultThreadCurrentCulture"/>.
    /// </remarks>
    public sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

        /// <summary>
        /// Switches the current flow to <paramref name="culture"/>.
        /// </summary>
        /// <param name="culture">The culture name, e.g. <c>de-DE</c>.</param>
        public CultureScope(string culture)
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _uiCulture;
        }
    }
}
