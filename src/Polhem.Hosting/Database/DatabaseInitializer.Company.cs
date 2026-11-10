using System.Data.Common;
using System.Globalization;
using Polhem.Core;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;

namespace Polhem.Hosting.Database
{
    /// <content>
    /// Readying company data for row-level tenancy: the checks that run before the tables are upgraded,
    /// and the company given afterwards to rows that carry none.
    /// </content>
    internal sealed partial class DatabaseInitializer
    {
        private const string CompanyTable = "st_company";

        /// <summary>
        /// Lists what must be fixed before the tables are upgraded: company ids that are not in their
        /// stored form, and company table schemas without the company column.
        /// </summary>
        /// <remarks>
        /// Both checks run before anything changes. A company id that is not lowercase would no longer be
        /// found on PostgreSQL and Oracle, and one longer than <see cref="CompanyInfo.CompanyIdMaxLength"/>
        /// does not fit the column it is copied into; renaming a company touches every place its id is
        /// stored, which is the administrator's call, so the upgrade stops instead of rewriting it.
        /// </remarks>
        private List<string> CheckCompanyReadiness(DatabaseSettings settings, IReadOnlyCollection<string>? tableNames)
        {
            var problems = new List<string>();

            var missing = CompanyScopeDefinitions.FindMissing(_defineAccess, tableNames);
            if (missing.Count > 0)
            {
                problems.Add(
                    $"These company table schemas have no {SysFields.CompanyId} column: {string.Join(", ", missing)}. " +
                    "Run 'db add-company-column' to add it to them, then upgrade again.");
            }

            var invalid = ReadCompanyIds(settings)
                .Select(row => row.CompanyId)
                .Where(id => !CompanyInfo.IsValidId(id))
                .Order(StringComparer.Ordinal)
                .ToList();
            if (invalid.Count > 0)
            {
                problems.Add(
                    $"These company ids in {CompanyTable} are not up to {CompanyInfo.CompanyIdMaxLength} lowercase " +
                    $"ASCII letters and digits: {string.Join(", ", invalid)}. Rename them, and every place the id is " +
                    "stored, before upgrading.");
            }
            return problems;
        }

        /// <summary>
        /// Gives every row of a company table that carries no company the one company its database
        /// belongs to, or reports the database when that company cannot be told.
        /// </summary>
        /// <remarks>
        /// A row with no company is invisible to the form repository and refused by every company-bound
        /// statement. Before row-level tenancy each company had a database of its own, so such a row
        /// belongs to the company <c>company_database_id</c> names. A database that several companies
        /// point at has no such answer, and its rows are reported and left for the administrator. A
        /// database no company points at is skipped: no company could read its rows before either.
        /// </remarks>
        private List<CompanyRowAssignment> AssignCompanyRows(
            DatabaseSettings settings, IReadOnlyCollection<string>? tableNames, bool dryRun, List<string> problems)
        {
            var assignments = new List<CompanyRowAssignment>();
            var tables = CompanyScopeDefinitions.CompanyTables(_defineAccess, tableNames).ToList();
            if (tables.Count == 0) return assignments;

            var companiesByDatabase = ReadCompanyIds(settings)
                .GroupBy(row => row.DatabaseId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Select(row => row.CompanyId).ToList(), StringComparer.Ordinal);

            foreach (var databaseId in GetCategoryDatabases(settings, DbCategoryIds.Company))
            {
                var companies = companiesByDatabase.GetValueOrDefault(databaseId) ?? [];
                if (companies.Count == 0) continue;

                var databaseType = _connectionManager.GetConnectionInfo(databaseId).DatabaseType;
                var dbAccess = _dbAccessFactory.Create(databaseId);
                var schemaProvider = DbDialectRegistry.Get(databaseType).CreateTableSchemaProvider(databaseId, _connectionManager);
                foreach (var table in tables)
                {
                    // A dry run, or a table that failed to build, leaves the table absent: nothing to give.
                    if (schemaProvider.GetTableSchema(table) is null) continue;
                    try
                    {
                        AssignTable(databaseId, databaseType, dbAccess, table, companies, dryRun, assignments, problems);
                    }
                    catch (DbException ex)
                    {
                        problems.Add($"Could not give {databaseId}.{table} its company: {ex.Message}");
                    }
                }
            }
            return assignments;
        }

        private static void AssignTable(
            string databaseId, DatabaseType databaseType, DbAccess dbAccess, string table, List<string> companies,
            bool dryRun, List<CompanyRowAssignment> assignments, List<string> problems)
        {
            string quotedTable = databaseType.QuoteIdentifier(table);
            string column = databaseType.QuoteIdentifier(SysFields.CompanyId);
            // Oracle stores an empty string as NULL, so a row with no company is either.
            string unassigned = $"{column} IS NULL OR {column} = ''";

            int rows = Convert.ToInt32(
                dbAccess.ExecuteScalar($"SELECT COUNT(*) FROM {quotedTable} WHERE {unassigned}"), CultureInfo.InvariantCulture);
            if (rows == 0) return;

            if (companies.Count > 1)
            {
                problems.Add(
                    $"{databaseId}.{table} has {rows} rows with no company, and several companies in {CompanyTable} " +
                    $"name {databaseId} as their database ({string.Join(", ", companies)}), so which one the rows " +
                    $"belong to cannot be told. Set {SysFields.CompanyId} on them by hand.");
                return;
            }

            if (!dryRun)
                dbAccess.ExecuteNonQuery($"UPDATE {quotedTable} SET {column} = {{0}} WHERE {unassigned}", companies[0]);
            assignments.Add(new CompanyRowAssignment
            {
                DatabaseId = databaseId,
                TableName = table,
                CompanyId = companies[0],
                Rows = rows,
            });
        }

        /// <summary>
        /// Reads every company's id and database from <c>st_company</c>, or nothing when the common
        /// database has no such table yet.
        /// </summary>
        private List<(string CompanyId, string DatabaseId)> ReadCompanyIds(DatabaseSettings settings)
        {
            var commonId = GetCategoryDatabases(settings, DbCategoryIds.Common).FirstOrDefault();
            if (commonId is null) return [];

            var databaseType = _connectionManager.GetConnectionInfo(commonId).DatabaseType;
            var schemaProvider = DbDialectRegistry.Get(databaseType).CreateTableSchemaProvider(commonId, _connectionManager);
            if (schemaProvider.GetTableSchema(CompanyTable) is null) return [];

            var table = _dbAccessFactory.Create(commonId).ExecuteDataTable(
                $"SELECT {databaseType.QuoteIdentifier(SysFields.Id)}, {databaseType.QuoteIdentifier("company_database_id")} " +
                $"FROM {databaseType.QuoteIdentifier(CompanyTable)}");
            return table is null
                ? []
                : table.Rows.Cast<System.Data.DataRow>()
                    .Select(row => (ValueUtilities.CStr(row[0]), ValueUtilities.CStr(row[1])))
                    .ToList();
        }
    }
}
