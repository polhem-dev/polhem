using Polhem.Db.Manager;
using Polhem.Db.Schema;
using Polhem.Definition.Database;
using Polhem.Definition.Storage;
using Polhem.LoadTests.Configuration;

namespace Polhem.LoadTests.Bootstrap
{
    /// <summary>
    /// Creates the physical databases and tables a run measures against.
    /// </summary>
    /// <remarks>
    /// Tables are built from <c>DbCategorySettings</c>, so which tables exist is decided by the
    /// definitions rather than by a list here — the same loop the demo seeders use.
    /// </remarks>
    public static class SchemaPreparer
    {
        /// <summary>
        /// Creates each category's physical database when it does not already exist.
        /// </summary>
        /// <param name="options">The run configuration.</param>
        /// <param name="defineAccess">Definition access, for the category list.</param>
        /// <param name="connectionStringTemplate">
        /// The connection string as configured, still containing the <c>{@DbName}</c> placeholder.
        /// </param>
        public static void EnsureDatabases(
            LoadTestOptions options, IDefineAccess defineAccess, string connectionStringTemplate)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(defineAccess);
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringTemplate);

            var categories = defineAccess.GetDbCategorySettings().Categories;
            if (categories is null) { return; }

            var factory = DbProviderRegistry.Get(options.Database.Provider);

            foreach (var category in categories)
            {
                if (category.Tables is null || category.Tables.Count == 0) { continue; }

                // The name is validated by `DatabaseCreator` right before it reaches the DDL, over the
                // whole resolved name: the category half comes from a definition file rather than from
                // the configuration that was validated.
                var databaseName = options.Database.ResolveDatabaseName(category.Id);
                var connectionString = connectionStringTemplate.Replace(
                    "{@DbName}", databaseName, StringComparison.Ordinal);
                DatabaseCreator.EnsureExists(options.Database.Provider, factory, connectionString);
            }
        }

        /// <summary>
        /// Builds every table registered in <c>DbCategorySettings</c>. Create-if-not-exists, so
        /// running it again is harmless.
        /// </summary>
        /// <param name="defineAccess">Definition access.</param>
        /// <param name="connectionManager">The connection manager.</param>
        /// <returns>The number of tables built or confirmed.</returns>
        public static int EnsureTables(
            IDefineAccess defineAccess, IDbConnectionManager connectionManager)
        {
            ArgumentNullException.ThrowIfNull(defineAccess);
            ArgumentNullException.ThrowIfNull(connectionManager);

            var categories = defineAccess.GetDbCategorySettings().Categories;
            if (categories is null) { return 0; }

            var count = 0;
            foreach (var category in categories)
            {
                if (category.Tables is null) { continue; }

                var builder = new TableSchemaBuilder(category.Id, defineAccess, connectionManager);
                foreach (var table in category.Tables)
                {
                    builder.Execute(category.Id, table.TableName);
                    count++;
                }
            }
            return count;
        }
    }
}
