using Polhem.Db.Manager;

namespace Polhem.Hosting.Database
{
    /// <summary>
    /// What <see cref="IDatabaseInitializer.CreateDatabases"/> did for one database item.
    /// </summary>
    public sealed class DatabaseCreateEntry
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
        /// Gets the outcome, or <c>null</c> when the item failed; <see cref="Error"/> then says why.
        /// </summary>
        public DatabaseCreateResult? Result { get; init; }

        /// <summary>
        /// Gets the reason the item failed, or <c>null</c> when it succeeded: the validator's or the
        /// engine's message. The initializer does not add the connection string to it.
        /// </summary>
        public string? Error { get; init; }
    }
}
