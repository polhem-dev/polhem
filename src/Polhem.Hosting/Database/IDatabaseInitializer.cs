using Polhem.Db.Schema;

namespace Polhem.Hosting.Database
{
    /// <summary>
    /// Brings a new deployment's databases up: creates the databases, builds or upgrades the tables,
    /// and seeds the first administrator.
    /// </summary>
    /// <remarks>
    /// Registered by <c>AddPolhemFramework</c>. Each step can be run again: an existing database or
    /// seeded row is left as it is, and an existing table only changes where it differs from its
    /// definition. <see cref="DatabaseCommandLine"/> exposes the steps as <c>db</c> commands of
    /// the host.
    /// </remarks>
    public interface IDatabaseInitializer
    {
        /// <summary>
        /// Creates each database named in the database settings that does not exist yet.
        /// </summary>
        /// <returns>One entry per database item. A failed item does not stop the others.</returns>
        IReadOnlyList<DatabaseCreateEntry> CreateDatabases();

        /// <summary>
        /// Opens a connection to each database item in the database settings and closes it again.
        /// </summary>
        /// <returns>One entry per database item. A failed item does not stop the others.</returns>
        /// <remarks>
        /// Nothing is created or changed. A database that does not exist yet fails here; run
        /// <see cref="CreateDatabases"/> first.
        /// </remarks>
        IReadOnlyList<DatabaseCheckEntry> CheckConnections();

        /// <summary>
        /// Builds or upgrades the tables registered in the database category settings, in every
        /// database item whose category they belong to: every table, or only those named in
        /// <paramref name="tableNames"/>.
        /// </summary>
        /// <param name="options">Upgrade options; <c>null</c> uses <see cref="UpgradeOptions.Default"/>.</param>
        /// <param name="dryRun">When <c>true</c>, plan the statements and do not run them.</param>
        /// <param name="tableCompleted">
        /// Called with each table's entry as soon as the table is done, so a caller can report progress
        /// while the remaining tables are still being processed.
        /// </param>
        /// <param name="tableNames">
        /// The tables to process, matched by name without regard to case; <c>null</c> or empty processes every
        /// registered table. A deployment can hold thousands of tables, so a caller that changed a few names them.
        /// A name that no category registers is reported in <see cref="TableUpgradeReport.Problems"/>.
        /// </param>
        /// <returns>The outcome of each table. A failed table does not stop the others.</returns>
        TableUpgradeReport UpgradeTables(
            UpgradeOptions? options = null, bool dryRun = false, Action<TableUpgradeEntry>? tableCompleted = null,
            IReadOnlyCollection<string>? tableNames = null);

        /// <summary>
        /// Creates the first administrator, a company, and the administrator's access to it, in the
        /// common database.
        /// </summary>
        /// <param name="seed">What to create.</param>
        /// <returns>Which of the three rows were created.</returns>
        /// <remarks>
        /// A row that already exists is left unchanged; in particular an existing user's password is
        /// not replaced (checked by <c>DatabaseInitializerDbTests</c>). The three inserts run in one transaction.
        /// </remarks>
        /// <exception cref="ArgumentException">A value in <paramref name="seed"/> is missing or invalid.</exception>
        AdministratorSeedResult SeedAdministrator(AdministratorSeed seed);
    }
}
