namespace Polhem.Hosting.Database
{
    /// <summary>
    /// The folder a <c>|DataDirectory|</c> prefix in a connection string stands for, such as
    /// <c>Data Source=|DataDirectory|app.db</c> for SQLite.
    /// </summary>
    /// <remarks>
    /// The provider substitutes the prefix with the current application domain value named <see cref="AppDomainKey"/>, and
    /// falls back to <see cref="AppContext.BaseDirectory"/> when nothing has set it. That fallback is the build output
    /// of whichever program opens the database, so each host and each tool would open its own file. Polhem points the
    /// value at the folder that holds the definition folder instead, which every process that reads the same
    /// definitions agrees on: <see cref="Polhem.Hosting.DependencyInjection.PolhemHostApplicationBuilderExtensions.AddPolhemBackend"/>
    /// and the <c>polhem-db</c> tool call <see cref="EnsureDefault"/>.
    /// </remarks>
    public static class DataDirectory
    {
        /// <summary>The name of the application domain value the providers read.</summary>
        public const string AppDomainKey = "DataDirectory";

        /// <summary>The prefix a connection string uses for the folder.</summary>
        public const string Prefix = "|DataDirectory|";

        /// <summary>
        /// The folder set in the current application domain, or <see langword="null"/> when nothing has set it.
        /// </summary>
        public static string? Current
            => AppDomain.CurrentDomain.GetData(AppDomainKey) is string { Length: > 0 } value ? value : null;

        /// <summary>
        /// The default folder for <paramref name="definePath"/>: the folder that holds it, or the definition folder
        /// itself when it is a root.
        /// </summary>
        /// <param name="definePath">The definition folder.</param>
        public static string DefaultFor(string definePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(definePath);
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(definePath));
            return Path.GetDirectoryName(full) ?? full;
        }

        /// <summary>
        /// Sets the folder to <see cref="DefaultFor"/> of <paramref name="definePath"/> unless the host has already
        /// set one, and returns the folder in effect.
        /// </summary>
        /// <param name="definePath">The definition folder.</param>
        public static string EnsureDefault(string definePath)
        {
            if (Current is { } current) return current;
            var folder = DefaultFor(definePath);
            AppDomain.CurrentDomain.SetData(AppDomainKey, folder);
            return folder;
        }
    }
}
