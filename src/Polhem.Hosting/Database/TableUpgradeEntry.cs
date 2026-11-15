using Polhem.Db.Schema;

namespace Polhem.Hosting.Database
{
    /// <summary>
    /// What <see cref="IDatabaseInitializer.UpgradeTables"/> did for one table in one database.
    /// </summary>
    public sealed class TableUpgradeEntry
    {
        /// <summary>
        /// Gets the id of the database item the table lives in.
        /// </summary>
        public string DatabaseId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the database category the table is registered under.
        /// </summary>
        public string CategoryId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the table name.
        /// </summary>
        public string TableName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the outcome.
        /// </summary>
        public TableUpgradeOutcome Outcome { get; init; }

        /// <summary>
        /// Gets the planned statements of a dry run, or an empty string.
        /// </summary>
        public string CommandText { get; init; } = string.Empty;

        /// <summary>
        /// Gets the reason the table failed, or <c>null</c> when it did not.
        /// </summary>
        public string? Error { get; init; }

        /// <summary>
        /// Gets whether the table was taken as unchanged from its upgrade record, without reading the database, by a
        /// <see cref="UpgradeCompareMode.Fast"/> compare.
        /// </summary>
        public bool UnchangedByRecord { get; init; }

        /// <summary>
        /// Gets the state of the table's upgrade record before this run, or <c>null</c> when it had none or the records
        /// could not be read.
        /// </summary>
        public TableUpgradeStatus? PreviousStatus { get; init; }
    }
}
