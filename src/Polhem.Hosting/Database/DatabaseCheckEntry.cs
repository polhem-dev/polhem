namespace Polhem.Hosting.Database
{
    /// <summary>
    /// Whether <see cref="IDatabaseInitializer.CheckConnections"/> could connect to one database item.
    /// </summary>
    public sealed class DatabaseCheckEntry
    {
        /// <summary>
        /// Gets the id of the database item in the database settings.
        /// </summary>
        public string DatabaseId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the database the item's connection string names: a database name, or a file path
        /// for SQLite. Empty when the connection string names none or could not be resolved.
        /// </summary>
        public string DatabaseName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the reason the connection failed, or <c>null</c> when it opened: the validator's or the
        /// engine's message. The initializer does not add the connection string to it.
        /// </summary>
        public string? Error { get; init; }

        /// <summary>
        /// Gets whether the connection opened.
        /// </summary>
        public bool Succeeded => Error is null;
    }
}
