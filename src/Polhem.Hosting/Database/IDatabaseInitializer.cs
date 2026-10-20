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
        /// Builds or upgrades every table registered in the database category settings, in every
        /// database item whose category it belongs to.
        /// </summary>
        /// <param name="options">Upgrade options; <c>null</c> uses <see cref="UpgradeOptions.Default"/>.</param>
        /// <param name="dryRun">When <c>true</c>, plan the statements and do not run them.</param>
        /// <returns>The outcome of each table. A failed table does not stop the others.</returns>
        TableUpgradeReport UpgradeTables(UpgradeOptions? options = null, bool dryRun = false);

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
