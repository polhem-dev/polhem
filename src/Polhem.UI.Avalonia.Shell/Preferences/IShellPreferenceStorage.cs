namespace Polhem.UI.Avalonia.Shell.Preferences
{
    /// <summary>
    /// Keeps the small per-viewer preferences the shell remembers between runs: the interface language chosen before
    /// sign-in and the last company entered.
    /// </summary>
    /// <remarks>
    /// The keys are the constants of <see cref="ShellPreferenceKeys"/>. An implementation stores plain strings and
    /// must not throw for a key it has never seen.
    /// </remarks>
    public interface IShellPreferenceStorage
    {
        /// <summary>
        /// Returns the stored value of <paramref name="key"/>, or an empty string when none is stored.
        /// </summary>
        /// <param name="key">The preference key.</param>
        string Load(string key);

        /// <summary>
        /// Stores <paramref name="value"/> under <paramref name="key"/>; an empty value removes the preference.
        /// </summary>
        /// <param name="key">The preference key.</param>
        /// <param name="value">The value to store.</param>
        void Save(string key, string value);
    }
}
