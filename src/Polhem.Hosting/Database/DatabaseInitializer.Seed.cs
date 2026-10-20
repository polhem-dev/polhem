using Polhem.Core;
using Polhem.Core.Security;
using Polhem.Db;
using Polhem.Definition.Database;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;

namespace Polhem.Hosting.Database
{
    /// <content>
    /// Seeding the first administrator, company and access grant.
    /// </content>
    internal sealed partial class DatabaseInitializer
    {
        // Column widths of the framework's st_user and st_company definitions. A longer value would
        // be truncated or refused by the engine, which is a worse message than this one.
        private const int MaxIdLength = 20;
        private const int MaxUserNameLength = 20;
        private const int MaxCompanyNameLength = 50;
        private const int MinPasswordLength = 8;

        /// <inheritdoc/>
        /// <remarks>
        /// The deployment administrator flag is written here, in the insert that creates the user.
        /// <c>SystemBusinessObject.SetDeploymentAdmin</c> is how an existing deployment appoints one;
        /// a deployment with no user at all has nobody to make that call, which is what this seed is
        /// for. An existing user's flag is never changed here.
        /// </remarks>
        public AdministratorSeedResult SeedAdministrator(AdministratorSeed seed)
        {
            ArgumentNullException.ThrowIfNull(seed);

            var userName = string.IsNullOrWhiteSpace(seed.UserName) ? seed.UserId : seed.UserName;
            var companyName = string.IsNullOrWhiteSpace(seed.CompanyName) ? seed.CompanyId : seed.CompanyName;
            ValidateSeed(seed, userName, companyName);
            var companyDatabaseId = ResolveCompanyDatabase(seed.CompanyDatabaseId);

            var databaseId = DbCategoryIds.Common;
            var databaseType = _connectionManager.GetConnectionInfo(databaseId).DatabaseType;
            var dbAccess = _dbAccessFactory.Create(databaseId);

            var userRowId = FindRowId(dbAccess, databaseType, "st_user", seed.UserId);
            var companyRowId = FindRowId(dbAccess, databaseType, "st_company", seed.CompanyId);
            bool userCreated = userRowId == Guid.Empty;
            bool companyCreated = companyRowId == Guid.Empty;
            if (userCreated) userRowId = Guid.NewGuid();
            if (companyCreated) companyRowId = Guid.NewGuid();
            bool accessGranted = userCreated || companyCreated
                || !HasAccess(dbAccess, databaseType, userRowId, companyRowId);

            var batch = new DbBatchSpec { UseTransaction = true };
            if (userCreated)
                batch.Commands.Add(InsertUser(databaseType, userRowId, seed, userName));
            if (companyCreated)
                batch.Commands.Add(InsertCompany(databaseType, companyRowId, seed, companyName, companyDatabaseId));
            if (accessGranted)
                batch.Commands.Add(InsertAccess(databaseType, userRowId, companyRowId));

            if (batch.Commands.Count > 0)
                dbAccess.ExecuteBatch(batch);

            return new AdministratorSeedResult
            {
                UserCreated = userCreated,
                CompanyCreated = companyCreated,
                AccessGranted = accessGranted,
                CompanyDatabaseId = companyDatabaseId,
            };
        }

        private void ValidateSeed(AdministratorSeed seed, string userName, string companyName)
        {
            RequireText(seed.UserId, MaxIdLength, "user id", nameof(seed.UserId));
            RequireText(userName, MaxUserNameLength, "user name", nameof(seed.UserName));
            RequireText(seed.CompanyId, MaxIdLength, "company id", nameof(seed.CompanyId));
            RequireText(companyName, MaxCompanyNameLength, "company name", nameof(seed.CompanyName));

            // IMPORTANT: no message here may include the password, not even a prefix of it.
            if (string.IsNullOrEmpty(seed.Password) || seed.Password.Length < MinPasswordLength)
            {
                throw new ArgumentException(
                    $"The administrator password must be at least {MinPasswordLength} characters long.",
                    nameof(seed));
            }

            if (string.IsNullOrWhiteSpace(seed.DefaultCurrency))
                throw new ArgumentException("A default currency is required.", nameof(seed));

            CurrencySettings currencies;
            try
            {
                currencies = _defineAccess.GetCurrencySettings();
            }
            catch (DefinitionNotFoundException)
            {
                // A deployment without its own currency file has nothing to check the code against.
                return;
            }
            if (currencies.Count > 0 && currencies.Find(seed.DefaultCurrency) is null)
            {
                throw new ArgumentException(
                    $"The currency '{seed.DefaultCurrency}' is not in the currency settings.", nameof(seed));
            }
        }

        private static void RequireText(string value, int maxLength, string description, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"A {description} is required.", parameterName);
            if (value.Length > maxLength)
            {
                throw new ArgumentException(
                    $"The {description} '{value}' is longer than {maxLength} characters.", parameterName);
            }
        }

        /// <summary>
        /// Returns the database item the company's data lives in.
        /// </summary>
        /// <remarks>
        /// Checked here rather than left to sign-in: a company pointing at a missing database signs in
        /// fine and then fails on the first company-scoped request, a long way from the cause.
        /// </remarks>
        private string ResolveCompanyDatabase(string requested)
        {
            var companyDatabases = GetCategoryDatabases(_defineAccess.GetDatabaseSettings(), DbCategoryIds.Company);
            if (!string.IsNullOrWhiteSpace(requested))
            {
                if (companyDatabases.Contains(requested, StringComparer.Ordinal)) return requested;
                throw new ArgumentException(
                    $"The database '{requested}' is not a database item of the '{DbCategoryIds.Company}' category.",
                    nameof(requested));
            }

            return companyDatabases.Count switch
            {
                1 => companyDatabases[0],
                0 => throw new ArgumentException(
                    $"No database item belongs to the '{DbCategoryIds.Company}' category. Add one to the database " +
                    "settings before creating a company.", nameof(requested)),
                _ => throw new ArgumentException(
                    $"Several database items belong to the '{DbCategoryIds.Company}' category " +
                    $"({string.Join(", ", companyDatabases)}); name the one this company uses.", nameof(requested)),
            };
        }

        private static Guid FindRowId(DbAccess dbAccess, DatabaseType databaseType, string table, string sysId)
        {
            var sql = $"SELECT {databaseType.QuoteIdentifier("sys_rowid")} FROM {databaseType.QuoteIdentifier(table)} " +
                      $"WHERE {databaseType.QuoteIdentifier("sys_id")} = {{0}}";
            var scalar = dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar, sql, sysId)).Scalar;
            return scalar is null || scalar is DBNull ? Guid.Empty : ValueUtilities.CGuid(scalar);
        }

        private static bool HasAccess(DbAccess dbAccess, DatabaseType databaseType, Guid userRowId, Guid companyRowId)
        {
            var sql = $"SELECT COUNT(*) FROM {databaseType.QuoteIdentifier("st_user_company")} " +
                      $"WHERE {databaseType.QuoteIdentifier("user_rowid")} = {{0}} " +
                      $"AND {databaseType.QuoteIdentifier("company_rowid")} = {{1}}";
            var scalar = dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar, sql, userRowId, companyRowId)).Scalar;
            return scalar is not null && ValueUtilities.CInt(scalar) > 0;
        }

        private static DbCommandSpec InsertUser(DatabaseType databaseType, Guid rowId, AdministratorSeed seed, string userName)
        {
            // time_zone and culture are left empty so the user follows the deployment defaults.
            var sql = BuildInsert(databaseType, "st_user",
                "sys_rowid", "sys_id", "sys_name", "password", "email", "note", "time_zone", "culture", "deployment_admin");
            return new DbCommandSpec(DbCommandKind.NonQuery, sql,
                rowId, seed.UserId, userName, PasswordHasher.HashPassword(seed.Password),
                string.Empty, string.Empty, string.Empty, string.Empty, seed.DeploymentAdmin);
        }

        private static DbCommandSpec InsertCompany(
            DatabaseType databaseType, Guid rowId, AdministratorSeed seed, string companyName, string companyDatabaseId)
        {
            // The three XML columns are Text, which MySQL gives no default, so they are written
            // explicitly as empty strings.
            var sql = BuildInsert(databaseType, "st_company",
                "sys_rowid", "sys_id", "sys_name", "company_database_id", "customize_id",
                "number_formats_xml", "default_currency", "cash_rounding_xml", "allowed_currencies_xml", "enabled");
            return new DbCommandSpec(DbCommandKind.NonQuery, sql,
                rowId, seed.CompanyId, companyName, companyDatabaseId, string.Empty,
                string.Empty, seed.DefaultCurrency, string.Empty, string.Empty, true);
        }

        private static DbCommandSpec InsertAccess(DatabaseType databaseType, Guid userRowId, Guid companyRowId)
        {
            var sql = BuildInsert(databaseType, "st_user_company", "sys_rowid", "user_rowid", "company_rowid");
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
