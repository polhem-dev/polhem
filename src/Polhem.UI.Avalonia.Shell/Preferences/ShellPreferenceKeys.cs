namespace Polhem.UI.Avalonia.Shell.Preferences
{
    /// <summary>
    /// The keys the shell stores in an <see cref="IShellPreferenceStorage"/>.
    /// </summary>
    public static class ShellPreferenceKeys
    {
        /// <summary>The interface language chosen before sign-in, as a culture name (<c>en</c>, <c>zh-TW</c>).</summary>
        public const string Language = "language";

        /// <summary>The id of the last company the viewer entered.</summary>
        public const string CompanyId = "company";
    }
}
