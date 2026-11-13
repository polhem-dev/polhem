namespace Polhem.Web.Blazor.BrowserTests
{
    /// <summary>
    /// A test that drives the installed Chrome. It skips on a machine without Chrome, and in CI, where the runner image
    /// ships Chrome, a missing browser fails at launch instead of skipping.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class BrowserFactAttribute : FactAttribute
    {
        private static readonly string[] s_chromePaths =
        [
            "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
            "/usr/bin/google-chrome",
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
        ];

        /// <summary>
        /// Initializes a new instance of <see cref="BrowserFactAttribute"/>.
        /// </summary>
        public BrowserFactAttribute() => Skip = SkipReason();

        /// <summary>
        /// Gets the reason to skip a browser test on this machine, or <c>null</c> to run it.
        /// </summary>
        /// <returns>The reason, or <c>null</c>.</returns>
        internal static string? SkipReason()
            => Environment.GetEnvironmentVariable("CI") is null && !s_chromePaths.Any(File.Exists) ? "Chrome is not installed." : null;
    }
}
