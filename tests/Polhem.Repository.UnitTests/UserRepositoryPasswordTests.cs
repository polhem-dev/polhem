using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using Polhem.Base.Security;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Repository.System;
using Polhem.Tests.Shared;

namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// Per-provider tests of <see cref="UserRepository.VerifyPassword"/>: the framework's built-in credential check
    /// against the hash stored in <c>st_user.password</c>.
    /// </summary>
    /// <remarks>
    /// Each test owns its user row, created in that provider's common database and removed afterwards, and the
    /// repository is routed to the same database through <see cref="ProviderScopedRouter"/>.
    /// The response-time equality between an unknown account and a wrong password is not asserted: a timing test
    /// would be flaky. What is pinned is that both paths return <c>false</c>.
    /// </remarks>
    public class UserRepositoryPasswordTests : IClassFixture<SharedDbFixture>
    {
        private const string Password = "correct horse battery staple";
        private readonly SharedDbFixture _fx;

        public UserRepositoryPasswordTests(SharedDbFixture fx) { _fx = fx; }

        private IDbConnectionManager ConnectionManager => _fx.GetRequiredService<IDbConnectionManager>();

        private UserRepository CreateRepo(DatabaseType databaseType)
            => new UserRepository(
                TestRepositoryContext.Create(ConnectionManager, router: new ProviderScopedRouter(databaseType)),
                Guid.Empty,
                string.Empty);

        /// <summary>
        /// Builds a stored value in the current <c>v2.</c> format with the given iteration count, cheaper to make than
        /// <see cref="PasswordHasher.HashPassword"/> and weaker than its parameters.
        /// </summary>
        private static string WeakV2Hash(string password, int iterations)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, 32);
            return $"v2.{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        /// <summary>
        /// Runs <paramref name="test"/> against a fresh user row whose stored hash is <paramref name="storedHash"/>.
        /// </summary>
        private void WithUser(DatabaseType databaseType, string storedHash, Action<UserRepository, string, string> test)
        {
            string databaseId = TestDbConventions.GetDatabaseId(databaseType, DbCategoryIds.Common);
            string userId = TestUsers.Create(ConnectionManager, "pwd", databaseId);
            try
            {
                TestUsers.SetPasswordHash(ConnectionManager, userId, storedHash, databaseId);
                test(CreateRepo(databaseType), userId, databaseId);
            }
            finally
            {
                TestUsers.Delete(ConnectionManager, userId, databaseId);
            }
        }

        private void RunCorrectPassword(DatabaseType databaseType)
            => WithUser(databaseType, PasswordHasher.HashPassword(Password),
                (repo, userId, _) => Assert.True(repo.VerifyPassword(userId, Password)));

        private void RunWrongPassword(DatabaseType databaseType)
            => WithUser(databaseType, WeakV2Hash(Password, 1000),
                (repo, userId, _) => Assert.False(repo.VerifyPassword(userId, "wrong " + Password)));

        private void RunUnknownUser(DatabaseType databaseType)
            => Assert.False(CreateRepo(databaseType).VerifyPassword($"no-such-{Guid.NewGuid():N}"[..20], Password));

        private void RunBlankHash(DatabaseType databaseType)
            // A single space is how a blank value is stored where '' would be NULL (Oracle); the repository treats it as blank.
            => WithUser(databaseType, " ", (repo, userId, _) =>
            {
                Assert.False(repo.VerifyPassword(userId, string.Empty));
                Assert.False(repo.VerifyPassword(userId, " "));
            });

        private void RunLegacyHash(DatabaseType databaseType)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(Password), salt, 1000, HashAlgorithmName.SHA1, 32);
            string legacy = $"1000.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";

            WithUser(databaseType, legacy, (repo, userId, _) => Assert.False(repo.VerifyPassword(userId, Password)));
        }

        private void RunRehash(DatabaseType databaseType)
            => WithUser(databaseType, WeakV2Hash(Password, 1000), (repo, userId, databaseId) =>
            {
                Assert.True(repo.VerifyPassword(userId, Password));

                string stored = TestUsers.GetPasswordHash(ConnectionManager, userId, databaseId);
                Assert.StartsWith($"v2.{PasswordHasher.Iterations}.", stored, StringComparison.Ordinal);
                Assert.False(PasswordHasher.NeedsRehash(stored));
                Assert.True(PasswordHasher.VerifyPassword(Password, stored));
            });

        private void RunNoRehashOnFailure(DatabaseType databaseType)
        {
            string weak = WeakV2Hash(Password, 1000);
            WithUser(databaseType, weak, (repo, userId, databaseId) =>
            {
                Assert.False(repo.VerifyPassword(userId, "wrong " + Password));
                Assert.Equal(weak, TestUsers.GetPasswordHash(ConnectionManager, userId, databaseId));
            });
        }

        #region CorrectPassword_ReturnsTrue

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("VerifyPassword accepts the password a current hash was made from (SQL Server)")]
        public void VerifyPassword_CorrectPassword_ReturnsTrue_SqlServer() => RunCorrectPassword(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("VerifyPassword accepts the password a current hash was made from (PostgreSQL)")]
        public void VerifyPassword_CorrectPassword_ReturnsTrue_PostgreSql() => RunCorrectPassword(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("VerifyPassword accepts the password a current hash was made from (SQLite)")]
        public void VerifyPassword_CorrectPassword_ReturnsTrue_Sqlite() => RunCorrectPassword(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("VerifyPassword accepts the password a current hash was made from (MySQL)")]
        public void VerifyPassword_CorrectPassword_ReturnsTrue_MySql() => RunCorrectPassword(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("VerifyPassword accepts the password a current hash was made from (Oracle)")]
        public void VerifyPassword_CorrectPassword_ReturnsTrue_Oracle() => RunCorrectPassword(DatabaseType.Oracle);

        #endregion

        #region WrongPassword_ReturnsFalse

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("VerifyPassword rejects a wrong password (SQL Server)")]
        public void VerifyPassword_WrongPassword_ReturnsFalse_SqlServer() => RunWrongPassword(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("VerifyPassword rejects a wrong password (PostgreSQL)")]
        public void VerifyPassword_WrongPassword_ReturnsFalse_PostgreSql() => RunWrongPassword(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("VerifyPassword rejects a wrong password (SQLite)")]
        public void VerifyPassword_WrongPassword_ReturnsFalse_Sqlite() => RunWrongPassword(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("VerifyPassword rejects a wrong password (MySQL)")]
        public void VerifyPassword_WrongPassword_ReturnsFalse_MySql() => RunWrongPassword(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("VerifyPassword rejects a wrong password (Oracle)")]
        public void VerifyPassword_WrongPassword_ReturnsFalse_Oracle() => RunWrongPassword(DatabaseType.Oracle);

        #endregion

        #region UnknownUser_ReturnsFalse

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("VerifyPassword rejects an account that does not exist (SQL Server)")]
        public void VerifyPassword_UnknownUser_ReturnsFalse_SqlServer() => RunUnknownUser(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("VerifyPassword rejects an account that does not exist (PostgreSQL)")]
        public void VerifyPassword_UnknownUser_ReturnsFalse_PostgreSql() => RunUnknownUser(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("VerifyPassword rejects an account that does not exist (SQLite)")]
        public void VerifyPassword_UnknownUser_ReturnsFalse_Sqlite() => RunUnknownUser(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("VerifyPassword rejects an account that does not exist (MySQL)")]
        public void VerifyPassword_UnknownUser_ReturnsFalse_MySql() => RunUnknownUser(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("VerifyPassword rejects an account that does not exist (Oracle)")]
        public void VerifyPassword_UnknownUser_ReturnsFalse_Oracle() => RunUnknownUser(DatabaseType.Oracle);

        #endregion

        #region BlankStoredHash_ReturnsFalse

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("VerifyPassword rejects every password for an account with a blank stored hash (SQL Server)")]
        public void VerifyPassword_BlankStoredHash_ReturnsFalse_SqlServer() => RunBlankHash(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("VerifyPassword rejects every password for an account with a blank stored hash (PostgreSQL)")]
        public void VerifyPassword_BlankStoredHash_ReturnsFalse_PostgreSql() => RunBlankHash(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("VerifyPassword rejects every password for an account with a blank stored hash (SQLite)")]
        public void VerifyPassword_BlankStoredHash_ReturnsFalse_Sqlite() => RunBlankHash(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("VerifyPassword rejects every password for an account with a blank stored hash (MySQL)")]
        public void VerifyPassword_BlankStoredHash_ReturnsFalse_MySql() => RunBlankHash(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("VerifyPassword rejects every password for an account with a blank stored hash (Oracle)")]
        public void VerifyPassword_BlankStoredHash_ReturnsFalse_Oracle() => RunBlankHash(DatabaseType.Oracle);

        #endregion

        #region LegacySha1Hash_ReturnsFalse

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("VerifyPassword rejects a hash in the retired PBKDF2-SHA1 format even with the right password (SQL Server)")]
        public void VerifyPassword_LegacySha1Hash_ReturnsFalse_SqlServer() => RunLegacyHash(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("VerifyPassword rejects a hash in the retired PBKDF2-SHA1 format even with the right password (PostgreSQL)")]
        public void VerifyPassword_LegacySha1Hash_ReturnsFalse_PostgreSql() => RunLegacyHash(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("VerifyPassword rejects a hash in the retired PBKDF2-SHA1 format even with the right password (SQLite)")]
        public void VerifyPassword_LegacySha1Hash_ReturnsFalse_Sqlite() => RunLegacyHash(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("VerifyPassword rejects a hash in the retired PBKDF2-SHA1 format even with the right password (MySQL)")]
        public void VerifyPassword_LegacySha1Hash_ReturnsFalse_MySql() => RunLegacyHash(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("VerifyPassword rejects a hash in the retired PBKDF2-SHA1 format even with the right password (Oracle)")]
        public void VerifyPassword_LegacySha1Hash_ReturnsFalse_Oracle() => RunLegacyHash(DatabaseType.Oracle);

        #endregion

        #region WeakerHash_IsReplacedOnSuccess

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("VerifyPassword replaces a hash with fewer iterations after a successful sign-in (SQL Server)")]
        public void VerifyPassword_WeakerHash_IsReplacedOnSuccess_SqlServer() => RunRehash(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("VerifyPassword replaces a hash with fewer iterations after a successful sign-in (PostgreSQL)")]
        public void VerifyPassword_WeakerHash_IsReplacedOnSuccess_PostgreSql() => RunRehash(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("VerifyPassword replaces a hash with fewer iterations after a successful sign-in (SQLite)")]
        public void VerifyPassword_WeakerHash_IsReplacedOnSuccess_Sqlite() => RunRehash(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("VerifyPassword replaces a hash with fewer iterations after a successful sign-in (MySQL)")]
        public void VerifyPassword_WeakerHash_IsReplacedOnSuccess_MySql() => RunRehash(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("VerifyPassword replaces a hash with fewer iterations after a successful sign-in (Oracle)")]
        public void VerifyPassword_WeakerHash_IsReplacedOnSuccess_Oracle() => RunRehash(DatabaseType.Oracle);

        #endregion

        #region WeakerHash_WrongPassword_KeepsStoredHash

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("VerifyPassword leaves a weaker hash in place when the password is wrong (SQL Server)")]
        public void VerifyPassword_WeakerHash_WrongPassword_KeepsStoredHash_SqlServer() => RunNoRehashOnFailure(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("VerifyPassword leaves a weaker hash in place when the password is wrong (PostgreSQL)")]
        public void VerifyPassword_WeakerHash_WrongPassword_KeepsStoredHash_PostgreSql() => RunNoRehashOnFailure(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("VerifyPassword leaves a weaker hash in place when the password is wrong (SQLite)")]
        public void VerifyPassword_WeakerHash_WrongPassword_KeepsStoredHash_Sqlite() => RunNoRehashOnFailure(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("VerifyPassword leaves a weaker hash in place when the password is wrong (MySQL)")]
        public void VerifyPassword_WeakerHash_WrongPassword_KeepsStoredHash_MySql() => RunNoRehashOnFailure(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("VerifyPassword leaves a weaker hash in place when the password is wrong (Oracle)")]
        public void VerifyPassword_WeakerHash_WrongPassword_KeepsStoredHash_Oracle() => RunNoRehashOnFailure(DatabaseType.Oracle);

        #endregion
    }
}
