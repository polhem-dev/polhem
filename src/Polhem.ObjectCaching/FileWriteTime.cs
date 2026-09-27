namespace Polhem.ObjectCaching
{
    /// <summary>
    /// Reads the last write time a file-backed cache entry is compared against.
    /// </summary>
    internal static class FileWriteTime
    {
        /// <summary>
        /// Returns the file's last write time in UTC, or <see cref="DateTime.MinValue"/> when it cannot be read.
        /// </summary>
        /// <param name="path">The watched file path.</param>
        /// <remarks>
        /// A watched file that cannot be stat'ed is treated as "no known write time", which makes the
        /// comparison equal and the entry stay cached. Only the failures that mean exactly that are
        /// swallowed; anything else is a real fault and propagates.
        /// </remarks>
        public static DateTime Get(string path)
        {
            try { return File.GetLastWriteTimeUtc(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                return DateTime.MinValue;
            }
        }
    }
}
