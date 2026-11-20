using Polhem.Db.Schema;

namespace Polhem.Hosting.Database
{
    /// <summary>
    /// What <see cref="IDatabaseInitializer.UpgradeTables"/> builds or upgrades, and how.
    /// </summary>
    /// <remarks>
    /// A new instance processes every registered table with <see cref="UpgradeOptions.Default"/> and runs the
    /// statements. A later input becomes a property here, so the interface member keeps its signature.
    /// </remarks>
    public sealed class TableUpgradeRequest
    {
        /// <summary>
        /// Gets or initializes the upgrade options; <c>null</c> uses <see cref="UpgradeOptions.Default"/>.
        /// </summary>
        public UpgradeOptions? Options { get; init; }

        /// <summary>
        /// Gets or initializes whether to plan the statements without running them. The default is <c>false</c>.
        /// </summary>
        public bool DryRun { get; init; }

        /// <summary>
        /// Gets or initializes a callback that receives each table's entry as soon as the table is done, so a caller
        /// can report progress while the remaining tables are still being processed.
        /// </summary>
        public Action<TableUpgradeEntry>? TableCompleted { get; init; }

        /// <summary>
        /// Gets or initializes the tables to process, matched by name without regard to case; <c>null</c> or empty
        /// processes every registered table.
        /// </summary>
        /// <remarks>
        /// A deployment can hold thousands of tables, so a caller that changed a few names them. A name that no
        /// category registers is reported in <see cref="TableUpgradeReport.Problems"/>.
        /// </remarks>
        public IReadOnlyCollection<string>? TableNames { get; init; }
    }
}
