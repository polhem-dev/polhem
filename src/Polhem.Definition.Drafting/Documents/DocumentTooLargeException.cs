namespace Polhem.Definition.Drafting.Documents
{
    /// <summary>
    /// Thrown when a document is larger than <see cref="DocumentSource"/> accepts for its type.
    /// </summary>
    public sealed class DocumentTooLargeException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentTooLargeException"/> class.
        /// </summary>
        /// <param name="fileName">The name of the refused file.</param>
        /// <param name="length">The file's size, in bytes.</param>
        /// <param name="maxLength">The limit for the file's type, in bytes.</param>
        public DocumentTooLargeException(string fileName, long length, long maxLength)
            : base($"'{fileName}' is {FormatMegabytes(length)}, larger than the {FormatMegabytes(maxLength)} accepted for this type of file. "
                + "Split it, or reduce the image resolution, and try again.")
        {
            FileName = fileName;
            Length = length;
            MaxLength = maxLength;
        }

        /// <summary>Gets the name of the refused file.</summary>
        public string FileName { get; }

        /// <summary>Gets the file's size, in bytes.</summary>
        public long Length { get; }

        /// <summary>Gets the limit for the file's type, in bytes.</summary>
        public long MaxLength { get; }

        private static string FormatMegabytes(long bytes) =>
            (bytes / (1024d * 1024d)).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " MB";
    }
}
