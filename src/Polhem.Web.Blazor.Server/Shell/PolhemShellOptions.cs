namespace Polhem.Web.Blazor.Server.Shell
{
    /// <summary>
    /// What an application tells the Blazor Server shell about itself: its title, the values the sign-in screen starts
    /// with, the languages it offers and the route of its form page.
    /// </summary>
    /// <remarks>
    /// The counterpart of the Avalonia shell's <c>ShellOptions</c>. Every value is optional; the shell names no
    /// application of its own. Register the options with
    /// <see cref="PolhemShellServiceCollectionExtensions.AddPolhemShell"/>; they are read by every circuit and by the
    /// request pipeline, so change them only inside that call.
    /// </remarks>
    public sealed class PolhemShellOptions
    {
        /// <summary>
        /// Gets or sets the application title, shown on the sign-in screen and in the top bar.
        /// </summary>
        public string AppTitle { get; set; } = "Polhem";

        /// <summary>
        /// Gets or sets a line shown under the sign-in title, such as the demonstration accounts. Empty shows none.
        /// </summary>
        public string SignInHint { get; set; } = string.Empty;

        /// <summary>Gets or sets the company id the sign-in screen starts with.</summary>
        public string DefaultCompanyId { get; set; } = string.Empty;

        /// <summary>Gets or sets the user id the sign-in screen starts with.</summary>
        public string DefaultUserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the password the sign-in screen starts with. Meant for demonstration accounts only.
        /// </summary>
        public string DefaultPassword { get; set; } = string.Empty;

        /// <summary>
        /// Gets the languages the site serves. The sign-in screen offers a switch between them when there is more
        /// than one, and <see cref="PolhemShellApplicationBuilderExtensions.UsePolhemShellLocalization"/> accepts
        /// only these cultures from a request.
        /// </summary>
        /// <remarks>
        /// The shell's own text exists in English and Traditional Chinese; any other language shows it in English,
        /// while the components and the definitions follow their own language resources.
        /// </remarks>
        public IList<PolhemShellLanguage> Languages { get; } =
        [
            new("en-US", "English"),
            new("zh-TW", "繁體中文"),
        ];

        /// <summary>
        /// Gets or sets the culture of a request that names none of <see cref="Languages"/>, as a culture name. Empty
        /// uses the first of <see cref="Languages"/>.
        /// </summary>
        /// <remarks>
        /// This is the language of the user interface before the viewer picks one. It is not
        /// <see cref="Polhem.Web.Blazor.Server.DependencyInjection.PolhemBlazorOptions.DefaultLanguage"/>, the last
        /// hop of the definitions' language fall-back chain.
        /// </remarks>
        public string DefaultCulture { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the first segment of the route that opens a form. The menu links to
        /// <c>{FormRoute}/{ProgId}</c> relative to the base address, so the application's page that hosts a
        /// <see cref="Polhem.Web.Blazor.Server.Components.FormPage"/> declares <c>@page "/form/{ProgId}"</c> with the
        /// default.
        /// </summary>
        public string FormRoute { get; set; } = "form";

        /// <summary>
        /// Returns the culture a request falls back to: <see cref="DefaultCulture"/>, or the first language.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// <see cref="Languages"/> is empty, or <see cref="DefaultCulture"/> is not one of them.
        /// </exception>
        internal string ResolveDefaultCulture()
        {
            if (Languages.Count == 0)
                throw new InvalidOperationException("PolhemShellOptions.Languages must name at least one language.");
            if (string.IsNullOrWhiteSpace(DefaultCulture))
                return Languages[0].Culture;
            return FindLanguage(DefaultCulture)?.Culture
                ?? throw new InvalidOperationException(
                    $"PolhemShellOptions.DefaultCulture '{DefaultCulture}' is not one of PolhemShellOptions.Languages.");
        }

        /// <summary>Returns the offered language named <paramref name="culture"/>, or <see langword="null"/>.</summary>
        internal PolhemShellLanguage? FindLanguage(string? culture)
            => Languages.FirstOrDefault(l => string.Equals(l.Culture, culture, StringComparison.OrdinalIgnoreCase));

        /// <summary>Returns the address, relative to the base address, of the form page for <paramref name="progId"/>.</summary>
        internal string GetFormUri(string progId)
            => $"{FormRoute.Trim('/')}/{Uri.EscapeDataString(progId)}";
    }
}
