using System.Data.Common;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Db.Schema;
using Polhem.Definition.Database;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;

namespace Polhem.Hosting.Database
{
    /// <summary>
    /// Default <see cref="IDatabaseInitializer"/>, driven by the database settings and the database
    /// category settings. The seeding half is in <c>DatabaseInitializer.Seed.cs</c>.
    /// </summary>
    internal sealed partial class DatabaseInitializer : IDatabaseInitializer
    {
        private readonly IDefineAccess _defineAccess;
        private readonly IDbConnectionManager _connectionManager;
        private readonly IDbAccessFactory _dbAccessFactory;

        /// <summary>
        /// Initializes a new <see cref="DatabaseInitializer"/>.
        /// </summary>
        /// <param name="defineAccess">Definition access, for the settings and table schemas.</param>
        /// <param name="connectionManager">The connection manager.</param>
        /// <param name="dbAccessFactory">Creates the data access used to seed the administrator.</param>
        public DatabaseInitializer(
            IDefineAccess defineAccess, IDbConnectionManager connectionManager, IDbAccessFactory dbAccessFactory)
        {
            _defineAccess = defineAccess ?? throw new ArgumentNullException(nameof(defineAccess));
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            _dbAccessFactory = dbAccessFactory ?? throw new ArgumentNullException(nameof(dbAccessFactory));
        }

        /// <inheritdoc/>
        public IReadOnlyList<DatabaseCreateEntry> CreateDatabases()
        {
            var settings = _defineAccess.GetDatabaseSettings();
            var entries = new List<DatabaseCreateEntry>();
            if (settings.Items is null) return entries;

            foreach (var item in settings.Items)
                entries.Add(CreateDatabase(settings, item));
            return entries;
        }

        private DatabaseCreateEntry CreateDatabase(DatabaseSettings settings, DatabaseItem item)
        {
            var databaseName = string.Empty;
            try
            {
                var info = _connectionManager.GetConnectionInfo(item.Id);
                databaseName = DatabaseCreator.GetDatabaseName(info.DatabaseType, info.Provider, info.ConnectionString);
                var result = DatabaseCreator.EnsureExists(info.DatabaseType, info.Provider, info.ConnectionString);
                return new DatabaseCreateEntry { DatabaseId = item.Id, DatabaseName = databaseName, Result = result };
            }
            catch (KeyNotFoundException)
            {
                return Failed(item.Id, databaseName, ProviderNotRegistered(GetDatabaseType(settings, item)));
            }
            catch (InvalidOperationException ex)
            {
                return Failed(item.Id, databaseName, ex.Message);
            }
            catch (DbException ex)
            {
                return Failed(item.Id, databaseName, ex.Message);
            }
        }

        private static DatabaseCreateEntry Failed(string databaseId, string databaseName, string error)
            => new() { DatabaseId = databaseId, DatabaseName = databaseName, Error = error };

        /// <inheritdoc/>
        public TableUpgradeReport UpgradeTables(UpgradeOptions? options = null, bool dryRun = false)
        {
            var databaseSettings = _defineAccess.GetDatabaseSettings();
            var categories = _defineAccess.GetDbCategorySettings().Categories;
            var tables = new List<TableUpgradeEntry>();
            var problems = new List<string>();
            if (categories is null) return new TableUpgradeReport();

            foreach (var category in categories)
            {
                if (category.Tables is null || category.Tables.Count == 0) continue;

                var databases = GetCategoryDatabases(databaseSettings, category.Id);
                if (databases.Count == 0)
                {
                    problems.Add(
                        $"No database item belongs to the '{category.Id}' category, so its tables were not built. " +
                        $"Add a database item with CategoryId=\"{category.Id}\" to the database settings.");
                    continue;
                }

                foreach (var databaseId in databases)
                {
                    foreach (var table in category.Tables)
                        tables.Add(UpgradeTable(databaseId, category.Id, table.TableName, options, dryRun));
                }
            }
            return new TableUpgradeReport { Tables = tables, Problems = problems };
        }

        private TableUpgradeEntry UpgradeTable(
            string databaseId, string categoryId, string tableName, UpgradeOptions? options, bool dryRun)
        {
            var entry = new TableUpgradeEntry { DatabaseId = databaseId, CategoryId = categoryId, TableName = tableName };
            try
            {
                var builder = new TableSchemaBuilder(databaseId, _defineAccess, _connectionManager);
                var diff = builder.CompareToDiff(categoryId, tableName);
                var databaseType = _connectionManager.GetConnectionInfo(databaseId).DatabaseType;
                var orchestrator = new TableUpgradeOrchestrator(DbDialectRegistry.Get(databaseType), _connectionManager);
                var plan = orchestrator.Plan(diff, options);

                if (plan.IsEmpty)
                    return WithOutcome(entry, TableUpgradeOutcome.Unchanged);

                if (dryRun)
                {
                    var commandText = string.Join(Environment.NewLine,
                        plan.AllStatements.Where(sql => !string.IsNullOrWhiteSpace(sql)));
                    return new TableUpgradeEntry
                    {
                        DatabaseId = databaseId,
                        CategoryId = categoryId,
                        TableName = tableName,
                        Outcome = TableUpgradeOutcome.Planned,
                        CommandText = commandText,
                    };
                }

                orchestrator.Execute(plan, databaseId);
                return WithOutcome(entry, diff.IsNewTable ? TableUpgradeOutcome.Created : TableUpgradeOutcome.Upgraded);
            }
            catch (KeyNotFoundException)
            {
                var databaseType = GetDatabaseType(_defineAccess.GetDatabaseSettings(), databaseId);
                return WithError(entry, ProviderNotRegistered(databaseType));
            }
            catch (DefinitionNotFoundException ex)
            {
                return WithError(entry, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return WithError(entry, ex.Message);
            }
            catch (DbException ex)
            {
                return WithError(entry, ex.Message);
            }
        }

        private static TableUpgradeEntry WithOutcome(TableUpgradeEntry entry, TableUpgradeOutcome outcome)
            => new()
            {
                DatabaseId = entry.DatabaseId,
                CategoryId = entry.CategoryId,
                TableName = entry.TableName,
                Outcome = outcome,
            };

        private static TableUpgradeEntry WithError(TableUpgradeEntry entry, string error)
            => new()
            {
                DatabaseId = entry.DatabaseId,
                CategoryId = entry.CategoryId,
                TableName = entry.TableName,
                Outcome = TableUpgradeOutcome.Failed,
                Error = error,
            };

        /// <summary>
        /// Returns the ids of the database items that belong to a category.
        /// </summary>
        /// <remarks>
        /// An item with no <see cref="DatabaseItem.CategoryId"/> counts as belonging to the category
        /// its id names, so settings written before the field existed (an item <c>common</c> with no
        /// category) keep working.
        /// </remarks>
        private static List<string> GetCategoryDatabases(DatabaseSettings settings, string categoryId)
        {
            if (settings.Items is null) return [];
            return settings.Items
                .Where(item => string.IsNullOrEmpty(item.CategoryId)
                    ? string.Equals(item.Id, categoryId, StringComparison.Ordinal)
                    : string.Equals(item.CategoryId, categoryId, StringComparison.Ordinal))
                .Select(item => item.Id)
                .ToList();
        }

        private static DatabaseType GetDatabaseType(DatabaseSettings settings, string databaseId)
        {
            var item = settings.Items?.FirstOrDefault(i => string.Equals(i.Id, databaseId, StringComparison.Ordinal));
            return item is null ? DatabaseType.SQLServer : GetDatabaseType(settings, item);
        }

        private static DatabaseType GetDatabaseType(DatabaseSettings settings, DatabaseItem item)
        {
            if (!string.IsNullOrEmpty(item.ServerId)
                && settings.Servers is not null && settings.Servers.Contains(item.ServerId))
            {
                return settings.Servers[item.ServerId].DatabaseType;
            }
            return item.DatabaseType;
        }

        private static string ProviderNotRegistered(DatabaseType databaseType)
            => $"No ADO.NET provider is registered for {databaseType}. The host registers the provider of each " +
               "database it uses with DbProviderRegistry.Register at startup.";
    }
}
