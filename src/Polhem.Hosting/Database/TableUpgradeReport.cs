namespace Polhem.Hosting.Database
{
    /// <summary>
    /// The result of <see cref="IDatabaseInitializer.UpgradeTables"/>.
    /// </summary>
    public sealed class TableUpgradeReport
    {
        /// <summary>
        /// Gets one entry per table and database, in the order they were processed.
        /// </summary>
        public IReadOnlyList<TableUpgradeEntry> Tables { get; init; } = [];

        /// <summary>
        /// Gets the problems that kept whole categories from being upgraded, such as a category with
        /// tables that no database item belongs to.
        /// </summary>
        public IReadOnlyList<string> Problems { get; init; } = [];

        /// <summary>
        /// Gets whether every table succeeded and no category was left out.
        /// </summary>
        public bool Succeeded => Problems.Count == 0 && Tables.All(t => t.Outcome != TableUpgradeOutcome.Failed);
    }
}
