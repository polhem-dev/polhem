namespace Polhem.UI.Avalonia.Shell.Localization
{
    /// <summary>One entry of the language switch.</summary>
    /// <param name="Code">The culture name.</param>
    /// <param name="DisplayName">The language's name in that language.</param>
    internal sealed record ShellLanguageOption(string Code, string DisplayName)
    {
        /// <inheritdoc/>
        public override string ToString() => DisplayName;
    }
}
