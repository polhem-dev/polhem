using Microsoft.Extensions.AI;

namespace Polhem.Definition.Drafting.Documents
{
    /// <summary>
    /// Turns a document the user picked into the content of a chat message: a PDF or an image goes to the model as is,
    /// and a Word document is read into Markdown first.
    /// </summary>
    /// <remarks>
    /// A file over its size limit is refused, never cut short: a truncated form would come back as a draft that silently
    /// lacks the fields of the missing part.
    /// </remarks>
    public static class DocumentSource
    {
        /// <summary>The largest PDF or Word document accepted, in bytes.</summary>
        public const long MaxDocumentBytes = 32L * 1024 * 1024;

        /// <summary>The largest image accepted, in bytes.</summary>
        public const long MaxImageBytes = 5L * 1024 * 1024;

        /// <summary>
        /// Gets the file extensions <see cref="FromFile(string)"/> accepts, with the leading dot.
        /// </summary>
        public static IReadOnlyList<string> SupportedExtensions { get; } = [".pdf", ".png", ".jpg", ".jpeg", ".docx"];

        /// <summary>
        /// Reads a document from disk into chat message content.
        /// </summary>
        /// <param name="path">The path of a <c>.pdf</c>, <c>.png</c>, <c>.jpg</c>, <c>.jpeg</c> or <c>.docx</c> file.</param>
        /// <returns>
        /// A <see cref="DataContent"/> for a PDF or an image, or a <see cref="TextContent"/> holding the Markdown of a Word
        /// document.
        /// </returns>
        /// <exception cref="NotSupportedException">The file type is not one of <see cref="SupportedExtensions"/>.</exception>
        /// <exception cref="DocumentTooLargeException">The file is larger than the limit for its type.</exception>
        public static AIContent FromFile(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            var fileName = Path.GetFileName(path);
            var kind = Classify(fileName);
            var length = new FileInfo(path).Length;
            EnsureWithinLimit(fileName, kind, length);
            using var stream = File.OpenRead(path);
            return Read(stream, fileName, kind);
        }

        /// <summary>
        /// Reads several documents, such as the front and back of a form, into the content of one chat message.
        /// </summary>
        /// <param name="paths">The paths, in the order the model should see them.</param>
        /// <returns>The content of each file, in the same order.</returns>
        /// <remarks>
        /// Every file's type and size are checked before any is read, so one refused file refuses the whole set.
        /// </remarks>
        /// <exception cref="NotSupportedException">A file type is not one of <see cref="SupportedExtensions"/>.</exception>
        /// <exception cref="DocumentTooLargeException">A file is larger than the limit for its type.</exception>
        public static IReadOnlyList<AIContent> FromFiles(IEnumerable<string> paths)
        {
            ArgumentNullException.ThrowIfNull(paths);
            var list = paths.ToList();
            foreach (var path in list)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(paths));
                var fileName = Path.GetFileName(path);
                EnsureWithinLimit(fileName, Classify(fileName), new FileInfo(path).Length);
            }
            return list.Select(FromFile).ToList();
        }

        /// <summary>
        /// Reads a document from a stream into chat message content.
        /// </summary>
        /// <param name="stream">The document's bytes. Read to the end; not disposed.</param>
        /// <param name="fileName">The file name, whose extension decides how the bytes are read.</param>
        /// <returns>The same content as <see cref="FromFile(string)"/> returns for a file of that name.</returns>
        /// <exception cref="NotSupportedException">The file type is not one of <see cref="SupportedExtensions"/>.</exception>
        /// <exception cref="DocumentTooLargeException">The document is larger than the limit for its type.</exception>
        public static AIContent FromStream(Stream stream, string fileName)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
            var kind = Classify(fileName);
            if (stream.CanSeek)
                EnsureWithinLimit(fileName, kind, stream.Length - stream.Position);
            return Read(stream, fileName, kind);
        }

        private static AIContent Read(Stream stream, string fileName, DocumentKind kind)
        {
            if (kind.MediaType is null)
            {
                var buffer = ReadAll(stream, fileName, kind);
                using var document = new MemoryStream(buffer, writable: false);
                return new TextContent(DocxMarkdownReader.Read(document));
            }
            return new DataContent(ReadAll(stream, fileName, kind), kind.MediaType) { Name = fileName };
        }

        private static byte[] ReadAll(Stream stream, string fileName, DocumentKind kind)
        {
            // A non-seekable stream reveals its length only once read, so the limit is checked again here.
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            EnsureWithinLimit(fileName, kind, buffer.Length);
            return buffer.ToArray();
        }

        private static DocumentKind Classify(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => new DocumentKind("application/pdf", MaxDocumentBytes),
                ".png" => new DocumentKind("image/png", MaxImageBytes),
                ".jpg" or ".jpeg" => new DocumentKind("image/jpeg", MaxImageBytes),
                ".docx" => new DocumentKind(null, MaxDocumentBytes),
                _ => throw new NotSupportedException(
                    $"'{fileName}' is not a supported document. Supported types: {string.Join(", ", SupportedExtensions)}."),
            };
        }

        private static void EnsureWithinLimit(string fileName, DocumentKind kind, long length)
        {
            if (length > kind.MaxBytes)
                throw new DocumentTooLargeException(fileName, length, kind.MaxBytes);
        }

        private readonly record struct DocumentKind(string? MediaType, long MaxBytes);
    }
}
