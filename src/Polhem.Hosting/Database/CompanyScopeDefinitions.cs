using Polhem.Definition.Database;
using Polhem.Definition.Storage;

namespace Polhem.Hosting.Database
{
    /// <summary>
    /// Finds, and adds, the company column and company-led unique indexes on the table schemas registered
    /// under the <c>company</c> category (<see cref="TableSchema.ApplyCompanyScope"/>).
    /// </summary>
    /// <remarks>
    /// A table schema written before the column existed lacks it, and its form repository would then
    /// query a column the table does not have. The upgrade refuses to run while such a table schema is
    /// registered; <c>db add-company-column</c> adds what is missing.
    /// </remarks>
    internal static class CompanyScopeDefinitions
    {
        /// <summary>
        /// Returns the names of the company tables, among <paramref name="tableNames"/> or all of them,
        /// whose table schema does not carry the company column and indexes yet.
        /// </summary>
        public static IReadOnlyList<string> FindMissing(IDefineAccess defineAccess, IReadOnlyCollection<string>? tableNames = null)
            => CompanyTables(defineAccess, tableNames)
                .Where(name => !defineAccess.GetTableSchema(DbCategoryIds.Company, name).HasCompanyScope())
                .ToList();

        /// <summary>
        /// Adds the company column and company-led unique indexes to every company table schema that lacks
        /// them, and saves each one unless <paramref name="dryRun"/> is set.
        /// </summary>
        /// <returns>The names of the table schemas that changed, or would change.</returns>
        public static IReadOnlyList<string> AddMissing(IDefineAccess defineAccess, bool dryRun)
        {
            var changed = new List<string>();
            foreach (var name in CompanyTables(defineAccess, tableNames: null))
            {
                // The cached instance is shared by every reader; change a copy and save that.
                var schema = defineAccess.GetTableSchema(DbCategoryIds.Company, name).Clone();
                if (!schema.ApplyCompanyScope()) continue;
                if (!dryRun) defineAccess.SaveTableSchema(DbCategoryIds.Company, schema);
                changed.Add(name);
            }
            return changed;
        }

        /// <summary>
        /// Returns the tables registered under the <c>company</c> category, optionally only those named.
        /// </summary>
        public static IEnumerable<string> CompanyTables(IDefineAccess defineAccess, IReadOnlyCollection<string>? tableNames)
        {
            var wanted = tableNames is { Count: > 0 }
                ? new HashSet<string>(tableNames, StringComparer.OrdinalIgnoreCase)
                : null;
            return (defineAccess.GetDbCategorySettings().Categories ?? [])
                .Where(c => string.Equals(c.Id, DbCategoryIds.Company, StringComparison.Ordinal))
                .SelectMany(c => c.Tables ?? [])
                .Select(t => t.TableName)
                .Where(name => wanted is null || wanted.Contains(name))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }
    }
}
