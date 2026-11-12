using System.Data;

namespace Polhem.Db
{
    /// <summary>
    /// Describes a batch of database commands to execute.
    /// </summary>
    public sealed class DbBatchSpec
    {
        /// <summary>
        /// The key under which <see cref="DbAccess.ExecuteBatch(DbBatchSpec)"/> and
        /// <see cref="DbAccess.ExecuteBatchAsync(DbBatchSpec, CancellationToken)"/> put the index of the failing
        /// command (an <see cref="int"/>) in the rethrown exception's <see cref="Exception.Data"/>.
        /// </summary>
        public const string FailedIndexDataKey = "Polhem.BatchIndex";

        /// <summary>
        /// Gets or sets the collection of commands to execute (executed in order).
        /// </summary>
        public DbCommandSpecCollection Commands { get; set; } = [];

        /// <summary>
        /// Gets or sets whether to wrap the entire batch in a transaction (rolls back on any failure; commits on success).
        /// </summary>
        public bool UseTransaction { get; set; } = false;

        /// <summary>
        /// Gets or sets the transaction isolation level (applicable when <see cref="UseTransaction"/> is <c>true</c>).
        /// </summary>
        public IsolationLevel? IsolationLevel { get; set; }
    }
}
