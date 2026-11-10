using System.Data.Common;
using Polhem.Db.Manager;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;
using Polhem.Repository.Abstractions.System;

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
        /// <returns>
        /// The problems, and whether every common database's <c>st_company</c> was read, which is what lets
        /// <c>sys_id</c> narrow to the company id's width.
        /// </returns>
        /// <remarks>
        /// Both checks run before anything changes. A company id that is not lowercase would no longer be
        /// found on PostgreSQL and Oracle, and one longer than <see cref="CompanyInfo.CompanyIdMaxLength"/>
        /// does not fit the column it is copied into; renaming a company touches every place its id is
        /// stored, which is the administrator's call, so the upgrade stops instead of rewriting it. A
        /// database that cannot be read here is not a problem of its own: its tables report it when they
        /// are upgraded.
        /// </remarks>
        private (List<string> Problems, bool CompanyIdsChecked) CheckCompanyReadiness(
            DatabaseSettings settings, IReadOnlyCollection<string>? tableNames)
        {
            var problems = new List<string>();

            var missing = CompanyScopeDefinitions.FindMissing(_defineAccess, tableNames);
            if (missing.Count > 0)
            {
                problems.Add(
                    $"These company table schemas have no {SysFields.CompanyId} column: {string.Join(", ", missing)}. " +
                    "Run 'db add-company-column' to add it to them, then upgrade again.");
            }

            var (companies, complete) = ReadCompanies(settings);
            var invalid = companies
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
            return (problems, complete);
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
        /// Database ids are matched without regard to case, as the connection lookup matches them.
        /// </remarks>
        private List<CompanyRowAssignment> AssignCompanyRows(
            DatabaseSettings settings, IReadOnlyCollection<string>? tableNames, bool dryRun, List<string> problems)
        {
            var assignments = new List<CompanyRowAssignment>();
            var tables = CompanyScopeDefinitions.CompanyTables(_defineAccess, tableNames).ToList();
            if (tables.Count == 0) return assignments;

            var companiesByDatabase = ReadCompanies(settings).Companies
                .GroupBy(row => row.DatabaseId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(row => row.CompanyId).ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var databaseId in GetCategoryDatabases(settings, DbCategoryIds.Company))
            {
                var companies = companiesByDatabase.GetValueOrDefault(databaseId) ?? [];
                if (companies.Count == 0) continue;
                try
                {
                    AssignDatabase(databaseId, tables, companies, dryRun, assignments, problems);
                }
                catch (DbException ex)
                {
                    problems.Add($"Could not give the rows of {databaseId} their company: {ex.Message}");
                }
                catch (KeyNotFoundException ex)
                {
                    problems.Add($"Could not give the rows of {databaseId} their company: {ex.Message}");
                }
            }
            return assignments;
        }

        private void AssignDatabase(
            string databaseId, List<string> tables, List<string> companies, bool dryRun,
            List<CompanyRowAssignment> assignments, List<string> problems)
        {
            var databaseType = _connectionManager.GetConnectionInfo(databaseId).DatabaseType;
            var repository = _repositoryFactory.Create<ICompanyRowAssignmentRepository>();
            var schemaProvider = DbDialectRegistry.Get(databaseType).CreateTableSchemaProvider(databaseId, _connectionManager);
            foreach (var table in tables)
            {
                // A dry run of a new deployment leaves the table absent; nothing to give.
                var actual = schemaProvider.GetTableSchema(table);
                if (actual is null) continue;
                // Without the column, the table either failed to upgrade (reported on its own) or this is a
                // dry run, after which every row would carry the column's empty default.
                bool hasColumn = actual.Fields!.Contains(SysFields.CompanyId);
                if (!hasColumn && !dryRun) continue;
                try
                {
                    var (assignment, problem) = AssignTable(repository, databaseId, table, hasColumn, companies, dryRun);
                    if (assignment is not null) assignments.Add(assignment);
                    if (problem is not null) problems.Add(problem);
                }
                catch (DbException ex)
                {
                    problems.Add($"Could not give {databaseId}.{table} its company: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Gives one table's rows with no company their company, or says why it cannot.
        /// </summary>
        /// <returns>The rows given a company, or the problem that stopped it; both empty when there was nothing to do.</returns>
        private static (CompanyRowAssignment? Assignment, string? Problem) AssignTable(
            ICompanyRowAssignmentRepository repository, string databaseId, string table, bool hasColumn,
            List<string> companies, bool dryRun)
        {
            int rows = repository.CountUnassignedRows(databaseId, table, hasColumn);
            if (rows == 0) return (null, null);

            if (companies.Count > 1)
            {
                return (null,
                    $"{databaseId}.{table} has {rows} rows with no company, and several companies in {CompanyTable} " +
                    $"name {databaseId} as their database ({string.Join(", ", companies)}), so which one the rows " +
                    $"belong to cannot be told. Set {SysFields.CompanyId} on them by hand.");
            }

            if (!dryRun)
                repository.AssignUnassignedRows(databaseId, table, companies[0]);
            return (new CompanyRowAssignment
            {
                DatabaseId = databaseId,
                TableName = table,
                CompanyId = companies[0],
                Rows = rows,
            }, null);
        }

        /// <summary>
        /// Reads every company's id and database from <c>st_company</c> in each common database.
        /// </summary>
        /// <returns>
        /// The companies, and whether every common database was read. A database that does not exist yet, or
        /// has no <c>st_company</c> yet, holds no company and counts as read; one that cannot be reached does not.
        /// </returns>
        private (List<(string CompanyId, string DatabaseId)> Companies, bool Complete) ReadCompanies(DatabaseSettings settings)
        {
            var companies = new List<(string CompanyId, string DatabaseId)>();
            bool complete = true;
            foreach (var commonId in GetCategoryDatabases(settings, DbCategoryIds.Common))
            {
                try
                {
                    var databaseType = _connectionManager.GetConnectionInfo(commonId).DatabaseType;
                    var schemaProvider = DbDialectRegistry.Get(databaseType).CreateTableSchemaProvider(commonId, _connectionManager);
                    if (schemaProvider.GetTableSchema(CompanyTable) is null) continue;

                    companies.AddRange(_repositoryFactory.Create<ICompanyRepository>()
                        .GetCompanyDatabases(commonId)
                        .Select(company => (company.CompanyId, company.DatabaseId)));
                }
                catch (DbException)
                {
                    complete = false;
                }
                catch (KeyNotFoundException)
                {
                    complete = false;
                }
            }
            return (companies, complete);
        }
    }
}
