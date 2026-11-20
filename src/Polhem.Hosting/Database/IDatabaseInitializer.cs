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
        /// The initializer creates and changes nothing; a server database that does not exist yet fails here, so run
        /// <see cref="CreateDatabases"/> first. Opening a SQLite connection creates an empty database file when the
        /// connection string allows it (the default mode), and the check then succeeds.
        /// </remarks>
        IReadOnlyList<DatabaseCheckEntry> CheckConnections();

        /// <summary>
        /// Builds or upgrades the tables registered in the database category settings, in every
        /// database item whose category they belong to: every table, or only those
        /// <see cref="TableUpgradeRequest.TableNames"/> names.
        /// </summary>
        /// <param name="request">
        /// The tables, the options, whether to run the statements, and the progress callback; a new
        /// <see cref="TableUpgradeRequest"/> runs every table with the default options.
        /// </param>
        /// <returns>The outcome of each table. A failed table does not stop the others.</returns>
        TableUpgradeReport UpgradeTables(TableUpgradeRequest request);

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
