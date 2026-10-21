namespace Polhem.UI.Avalonia.Shell.Preferences
{
    /// <summary>
    /// File-backed <see cref="IShellPreferenceStorage"/>: one single-line UTF-8 text file per key, in a per-application
    /// folder under the per-user local application data directory.
    /// </summary>
    /// <remarks>
    /// The folder is the one <see cref="Polhem.UI.ApiBridge.FileEndpointStorage"/> uses for the same application
    /// name, so the endpoint, the API key and these preferences sit side by side. Each file is named
    /// <c>pref-&lt;key&gt;.txt</c>. Browser WebAssembly has no persistent file system, so a browser head supplies an
    /// implementation backed by browser storage instead.
    /// </remarks>
    public sealed class FileShellPreferenceStorage : IShellPreferenceStorage
    {
        private readonly string _directory;

        /// <summary>
        /// Initializes a new instance of <see cref="FileShellPreferenceStorage"/>.
        /// </summary>
        /// <param name="appName">
        /// Application folder name appended to the per-user local application data directory; a single path segment.
        /// </param>
        public FileShellPreferenceStorage(string appName)
            : this(appName, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
        {
        }

        /// <summary>
        /// Initializes a new instance under an explicit root directory, so tests keep the files out of the real
        /// user profile.
        /// </summary>
        internal FileShellPreferenceStorage(string appName, string rootDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(appName);
            if (appName.IndexOfAny(['/', '\\']) >= 0 || appName is "." or "..")
                throw new ArgumentException("The application name must be a single path segment.", nameof(appName));

            _directory = Path.Combine(rootDirectory, appName);
        }

        /// <summary>Gets the folder the preference files are written to.</summary>
        public string DirectoryPath => _directory;

        /// <inheritdoc/>
        public string Load(string key)
        {
            var path = GetPath(key);
            return File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;
        }

        /// <inheritdoc/>
        public void Save(string key, string value)
        {
            var path = GetPath(key);
            if (string.IsNullOrEmpty(value))
            {
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }

            Directory.CreateDirectory(_directory);
            File.WriteAllText(path, value);
        }

        private string GetPath(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            // NOTE: keys come from `ShellPreferenceKeys`, not from user input; the check still keeps a key from
            // naming a file outside the folder.
            if (key.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || key.Contains("..", StringComparison.Ordinal))
                throw new ArgumentException("The preference key must be a plain file name segment.", nameof(key));

            return Path.Combine(_directory, "pref-" + key + ".txt");
        }
    }
}
