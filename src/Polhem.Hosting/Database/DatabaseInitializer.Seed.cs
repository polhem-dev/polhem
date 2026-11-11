using Polhem.Core.Security;
using Polhem.Definition.Database;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;
using Polhem.Repository.Abstractions.System;
using Polhem.Definition.Storage;

namespace Polhem.Hosting.Database
{
    /// <content>
    /// Seeding the first administrator, company and access grant.
    /// </content>
    internal sealed partial class DatabaseInitializer
    {
        // Column widths of the framework's st_user and st_company definitions; the user id's is
        // `UserInfo.UserIdMaxLength`. A longer value would be truncated or refused by the engine, which is a worse
        // message than this one.
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
            var companyId = CompanyInfo.NormalizeId(seed.CompanyId);
            var companyDatabaseId = ResolveCompanyDatabase(seed.CompanyDatabaseId);

            var outcome = _repositoryFactory.Value.Create<IAdministratorSeedRepository>().SeedAdministrator(
                new AdministratorSeedRecord
                {
                    UserId = seed.UserId,
                    UserName = userName,
                    PasswordHash = PasswordHasher.HashPassword(seed.Password),
                    DeploymentAdmin = seed.DeploymentAdmin,
                    CompanyId = companyId,
                    CompanyName = companyName,
                    CompanyDatabaseId = companyDatabaseId,
                    DefaultCurrency = seed.DefaultCurrency,
                });

            return new AdministratorSeedResult
            {
                UserCreated = outcome.UserCreated,
                CompanyCreated = outcome.CompanyCreated,
                AccessGranted = outcome.AccessGranted,
                CompanyDatabaseId = companyDatabaseId,
            };
        }

        private void ValidateSeed(AdministratorSeed seed, string userName, string companyName)
        {
            RequireText(seed.UserId, UserInfo.UserIdMaxLength, "user id", nameof(seed.UserId));
            RequireText(userName, MaxUserNameLength, "user name", nameof(seed.UserName));
            RequireText(seed.CompanyId, CompanyInfo.CompanyIdMaxLength, "company id", nameof(seed.CompanyId));
            if (!CompanyInfo.IsValidId(CompanyInfo.NormalizeId(seed.CompanyId)))
            {
                throw new ArgumentException(
                    $"The company id '{seed.CompanyId}' may contain only ASCII letters and digits.", nameof(seed));
            }
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
    }
}
