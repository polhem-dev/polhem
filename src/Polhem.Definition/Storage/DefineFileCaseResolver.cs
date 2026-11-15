namespace Polhem.Definition.Storage
{
    /// <summary>
    /// Finds a definition file whose name differs from the requested one only in case.
    /// </summary>
    /// <remarks>
    /// Callers name a language namespace in whatever case they write it, and the language cache keys it in lowercase,
    /// so whichever spelling reaches the storage first decides what every spelling gets. On a case-insensitive file
    /// system the requested path finds the file whatever its case; on a case-sensitive one it would not, and the miss
    /// would read as a missing translation. This makes both behave alike.
    /// </remarks>
    internal static class DefineFileCaseResolver
    {
        /// <summary>
        /// Returns the path of the existing file that matches <paramref name="path"/> ignoring the case of its file
        /// name and of its parent folder's name, or <paramref name="path"/> itself when it exists or nothing matches.
        /// </summary>
        /// <param name="path">The requested file path.</param>
        /// <exception cref="InvalidOperationException">
        /// The requested file does not exist and more than one file matches it ignoring case.
        /// </exception>
        public static string Resolve(string path)
        {
            if (File.Exists(path))
                return path;

            string? folder = ResolveFolder(Path.GetDirectoryName(path));
            if (folder is null)
                return path;

            string fileName = Path.GetFileName(path);
            var matches = Directory.EnumerateFiles(folder)
                .Where(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToList();
            return matches.Count switch
            {
                0 => path,
                1 => matches[0],
                _ => throw new InvalidOperationException(
                    $"More than one definition file matches '{fileName}' ignoring case; keep only one of them."),
            };
        }

        /// <summary>
        /// Returns <paramref name="folder"/> when it exists, otherwise the one sibling folder whose name matches it
        /// ignoring case, or <c>null</c> when there is none or more than one.
        /// </summary>
        private static string? ResolveFolder(string? folder)
        {
            if (string.IsNullOrEmpty(folder) || Directory.Exists(folder))
                return folder;

            string? parent = Path.GetDirectoryName(folder);
            if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
                return null;

            string name = Path.GetFileName(folder);
            var matches = Directory.EnumerateDirectories(parent)
                .Where(d => string.Equals(Path.GetFileName(d), name, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToList();
            return matches.Count == 1 ? matches[0] : null;
        }
    }
}
