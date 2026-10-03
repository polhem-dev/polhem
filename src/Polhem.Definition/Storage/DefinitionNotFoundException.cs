namespace Polhem.Definition.Storage
{
    /// <summary>
    /// A definition the storage must hold is not there: a form schema, a table schema, the program registry or the
    /// database categories.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A remote caller receives <see cref="Exception.Message"/> verbatim as a user message (the
    /// <c>JsonRpcErrorContract</c> row in <c>Polhem.Api.Core</c>), so a missing definition reads as what it is rather
    /// than as a generic server error. The message names only the definition type and its key; the key is the value
    /// the caller sent (a progId) or a name of the definition itself, never a path. A file storage keeps the full
    /// path on <see cref="FileNotFoundException.FileName"/>, which only the server's log records.
    /// <c>DefinitionNotFoundExceptionTests</c> pins the message.
    /// </para>
    /// <para>
    /// It derives from <see cref="FileNotFoundException"/> because the framework's callers that treat a missing
    /// definition as a normal case catch that type, and a database storage reporting the same condition takes the
    /// same branch.
    /// </para>
    /// </remarks>
    public sealed class DefinitionNotFoundException : FileNotFoundException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DefinitionNotFoundException"/> class.
        /// </summary>
        /// <param name="defineType">The type of the missing definition.</param>
        /// <param name="key">
        /// The key of the missing definition, such as the progId of a form schema; empty for a definition the storage
        /// holds only once.
        /// </param>
        /// <param name="filePath">The full path of the missing file, or <c>null</c> for a storage that has none.</param>
        public DefinitionNotFoundException(DefineType defineType, string key, string? filePath = null)
            : base(FormatMessage(defineType, key), filePath)
        {
            DefineType = defineType;
            Key = key ?? string.Empty;
        }

        /// <summary>
        /// Gets the type of the missing definition.
        /// </summary>
        public DefineType DefineType { get; }

        /// <summary>
        /// Gets the key of the missing definition; empty for a definition the storage holds only once.
        /// </summary>
        public string Key { get; }

        private static string FormatMessage(DefineType defineType, string? key)
            => string.IsNullOrEmpty(key)
                ? $"{defineType} not found."
                : $"{defineType} '{key}' not found.";
    }
}
