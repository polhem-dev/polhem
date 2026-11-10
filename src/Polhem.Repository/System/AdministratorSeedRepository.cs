using Polhem.Core;
using Polhem.Db;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Repository.Abstractions.System;

namespace Polhem.Repository.System
{
    /// <summary>
    /// Default <see cref="IAdministratorSeedRepository"/>.
    /// </summary>
    internal sealed class AdministratorSeedRepository : RepositoryBase, IAdministratorSeedRepository
    {
        private const string UserTable = "st_user";
        private const string CompanyTable = "st_company";
        private const string AccessTable = "st_user_company";

        public AdministratorSeedRepository(IRepositoryContext ctx, Guid accessToken, string progId)
            : base(ctx, accessToken, progId, DbScope.Common)
        {
        }

        /// <inheritdoc/>
        public AdministratorSeedOutcome SeedAdministrator(AdministratorSeedRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            var databaseType = Context.ConnectionManager.GetConnectionInfo(DatabaseId).DatabaseType;
            var dbAccess = CreateDbAccess();

            var userRowId = FindRowId(dbAccess, databaseType, UserTable, record.UserId);
            var companyRowId = FindRowId(dbAccess, databaseType, CompanyTable, record.CompanyId);
            bool userCreated = userRowId == Guid.Empty;
            bool companyCreated = companyRowId == Guid.Empty;
            if (userCreated) userRowId = Guid.NewGuid();
            if (companyCreated) companyRowId = Guid.NewGuid();
            bool accessGranted = userCreated || companyCreated
                || !HasAccess(dbAccess, databaseType, userRowId, companyRowId);

            var batch = new DbBatchSpec { UseTransaction = true };
            if (userCreated)
                batch.Commands.Add(InsertUser(databaseType, userRowId, record));
            if (companyCreated)
                batch.Commands.Add(InsertCompany(databaseType, companyRowId, record));
            if (accessGranted)
                batch.Commands.Add(InsertAccess(databaseType, userRowId, companyRowId));

            if (batch.Commands.Count > 0)
                dbAccess.ExecuteBatch(batch);

            return new AdministratorSeedOutcome(userCreated, companyCreated, accessGranted);
        }

        private static Guid FindRowId(DbAccess dbAccess, DatabaseType databaseType, string table, string sysId)
        {
            var sql = $"SELECT {databaseType.QuoteIdentifier(SysFields.RowId)} FROM {databaseType.QuoteIdentifier(table)} " +
                      $"WHERE {databaseType.QuoteIdentifier(SysFields.Id)} = {{0}}";
            var scalar = dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar, sql, sysId)).Scalar;
            return scalar is null || scalar is DBNull ? Guid.Empty : ValueUtilities.CGuid(scalar);
        }

        private static bool HasAccess(DbAccess dbAccess, DatabaseType databaseType, Guid userRowId, Guid companyRowId)
        {
            var sql = $"SELECT COUNT(*) FROM {databaseType.QuoteIdentifier(AccessTable)} " +
                      $"WHERE {databaseType.QuoteIdentifier("user_rowid")} = {{0}} " +
                      $"AND {databaseType.QuoteIdentifier("company_rowid")} = {{1}}";
            var scalar = dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar, sql, userRowId, companyRowId)).Scalar;
            return scalar is not null && ValueUtilities.CInt(scalar) > 0;
        }

        private static DbCommandSpec InsertUser(DatabaseType databaseType, Guid rowId, AdministratorSeedRecord record)
        {
            // time_zone and culture are left empty so the user follows the deployment defaults.
            var sql = BuildInsert(databaseType, UserTable,
                SysFields.RowId, SysFields.Id, SysFields.Name, "password", "email", "note", "time_zone", "culture", "deployment_admin");
            return new DbCommandSpec(DbCommandKind.NonQuery, sql,
                rowId, record.UserId, record.UserName, record.PasswordHash,
                string.Empty, string.Empty, string.Empty, string.Empty, record.DeploymentAdmin);
        }

        private static DbCommandSpec InsertCompany(DatabaseType databaseType, Guid rowId, AdministratorSeedRecord record)
        {
            // The three XML columns are Text, which MySQL gives no default, so they are written
            // explicitly as empty strings.
            var sql = BuildInsert(databaseType, CompanyTable,
                SysFields.RowId, SysFields.Id, SysFields.Name, "company_database_id", "customize_id",
                "number_formats_xml", "default_currency", "cash_rounding_xml", "allowed_currencies_xml", "enabled");
            return new DbCommandSpec(DbCommandKind.NonQuery, sql,
                rowId, record.CompanyId, record.CompanyName, record.CompanyDatabaseId, string.Empty,
                string.Empty, record.DefaultCurrency, string.Empty, string.Empty, true);
        }

        private static DbCommandSpec InsertAccess(DatabaseType databaseType, Guid userRowId, Guid companyRowId)
        {
            var sql = BuildInsert(databaseType, AccessTable, SysFields.RowId, "user_rowid", "company_rowid");
            return new DbCommandSpec(DbCommandKind.NonQuery, sql, Guid.NewGuid(), userRowId, companyRowId);
        }

        private static string BuildInsert(DatabaseType databaseType, string table, params string[] columns)
        {
            var columnList = string.Join(", ", columns.Select(column => databaseType.QuoteIdentifier(column)));
            var valueList = string.Join(", ", columns.Select((_, index) => $"{{{index}}}"));
            return $"INSERT INTO {databaseType.QuoteIdentifier(table)} ({columnList}) VALUES ({valueList})";
        }
    }
}
