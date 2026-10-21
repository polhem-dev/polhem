using Polhem.UI.Avalonia.Shell.Preferences;

namespace Polhem.UI.Avalonia.Shell
{
    /// <summary>
    /// What an application tells the shell about itself: its title, the values the connection and sign-in screens
    /// start with, and where the shell keeps the viewer's preferences.
    /// </summary>
    /// <remarks>
    /// Every value is optional; the shell names no application of its own. An application supplies its values by
    /// overriding <see cref="ShellApplication.CreateOptions"/>, and a platform head adjusts them through
    /// <see cref="ShellApplication.ConfigureOptions"/>. The shell reads the options once, when the application
    /// initializes; changing them afterwards has no effect.
    /// </remarks>
    public sealed class ShellOptions
    {
        /// <summary>
        /// Gets or sets the application title, shown in the window title, on the connection screen and in the top
        /// bar of the forms screen.
        /// </summary>
        public string AppTitle { get; set; } = "Polhem";

        /// <summary>
        /// Gets or sets the text shown under the title on the connection screen. Empty shows the shell's own
        /// connection hint in the interface language.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the JSON-RPC endpoint the connection screen starts with when none is stored.
        /// </summary>
        public string DefaultEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the API key the application ships with. It only seeds empty storage on the first run
        /// (<see cref="Polhem.UI.ApiBridge.ClientInfo.ApplyApiKey"/>); after that the stored key wins.
        /// </summary>
        public string DefaultApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the company id the sign-in screen starts with when no company has been entered on this
        /// device before.
        /// </summary>
        public string DefaultCompanyId { get; set; } = string.Empty;

        /// <summary>Gets or sets the user id the sign-in screen starts with.</summary>
        public string DefaultUserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the password the sign-in screen starts with. Meant for demonstration accounts only.
        /// </summary>
        public string DefaultPassword { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a line shown under the sign-in title, such as the demonstration accounts. Empty shows none.
        /// </summary>
        public string SignInHint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the folder name the default preference storage uses under the per-user application data
        /// directory. Empty uses <see cref="AppTitle"/>.
        /// </summary>
        public string StorageName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the storage for the viewer's preferences (the interface language and the last company).
        /// <see langword="null"/> uses a <see cref="FileShellPreferenceStorage"/> named after
        /// <see cref="StorageName"/>; a head without a persistent file system, such as the browser, supplies its
        /// own.
        /// </summary>
        public IShellPreferenceStorage? PreferenceStorage { get; set; }

        /// <summary>
        /// Gets or sets whether the connection and sign-in screens offer a choice of interface language.
        /// </summary>
        /// <remarks>
        /// A head whose fonts cannot draw every offered language turns it off. The browser head is one: its bundled
        /// font has no CJK glyphs and the sandbox has no system font to borrow them from.
        /// </remarks>
        public bool AllowLanguageSwitch { get; set; } = true;

        internal string ResolveStorageName()
            => string.IsNullOrWhiteSpace(StorageName) ? AppTitle : StorageName;
    }
}
