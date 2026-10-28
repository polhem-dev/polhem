namespace Polhem.Web.Blazor.Server.Shell
{
    /// <summary>
    /// The address the shell's language switch goes through, shared by the switch and the endpoint behind it.
    /// </summary>
    internal static class PolhemShellCulture
    {
        /// <summary>The path of the endpoint, relative to the base address.</summary>
        public const string Path = "culture";

        /// <summary>
        /// Returns the address that switches to <paramref name="culture"/> and then returns to
        /// <paramref name="returnPath"/>.
        /// </summary>
        public static string BuildSwitchUri(string culture, string returnPath)
            => $"{Path}?culture={Uri.EscapeDataString(culture)}&redirectUri={Uri.EscapeDataString(returnPath)}";

        /// <summary>
        /// Returns whether <paramref name="redirectUri"/> is a path on this site, the only kind the endpoint
        /// redirects to.
        /// </summary>
        /// <remarks>
        /// IMPORTANT: an absolute or protocol-relative address (<c>//host</c>, or <c>/\host</c>, which browsers read
        /// the same way) would make the endpoint an open redirect. The rule is the one of ASP.NET Core's
        /// <c>IUrlHelper.IsLocalUrl</c> without the <c>~/</c> form, which the switch never produces.
        /// </remarks>
        public static bool IsLocalPath(string? redirectUri)
        {
            if (string.IsNullOrEmpty(redirectUri) || redirectUri[0] != '/')
                return false;
            if (redirectUri.Length == 1)
                return true;
            return redirectUri[1] != '/' && redirectUri[1] != '\\' && !redirectUri.Any(char.IsControl);
        }
    }
}
