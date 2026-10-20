namespace Polhem.Hosting.Database
{
    /// <summary>
    /// What <see cref="IDatabaseInitializer.UpgradeTables"/> did for one table.
    /// </summary>
    public enum TableUpgradeOutcome
    {
        /// <summary>The table already matched its definition.</summary>
        Unchanged,

        /// <summary>The table did not exist and was created.</summary>
        Created,

        /// <summary>The table existed and was changed to match its definition.</summary>
        Upgraded,

        /// <summary>A dry run planned changes and did not apply them; the statements are in <see cref="TableUpgradeEntry.CommandText"/>.</summary>
        Planned,

        /// <summary>The table could not be compared or upgraded; <see cref="TableUpgradeEntry.Error"/> says why.</summary>
        Failed,
    }
}
