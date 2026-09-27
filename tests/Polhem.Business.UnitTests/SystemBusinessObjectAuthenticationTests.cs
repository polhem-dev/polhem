using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using Polhem.Base.Security;
using Polhem.Business.System;
using Polhem.Db.Manager;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Identity;
using Polhem.Repository.Abstractions.System;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Tests the built-in credential check: <see cref="SystemBusinessObject.Login"/> through the base
    /// <c>AuthenticateUser</c>, which verifies the password against <c>st_user.password</c> through
    /// <see cref="IUserRepository.VerifyPassword"/>. No test double replaces the check here.
    /// </summary>
    /// <remarks>
    /// The business object reaches the common database through DI, so one provider is enough and the gate is
    /// <c>SQLServer</c>. Each test owns its user row; the per-provider behaviour of the repository is covered by
    /// <c>UserRepositoryPasswordTests</c>.
    /// </remarks>
    public class SystemBusinessObjectAuthenticationTests : IClassFixture<SharedDbFixture>
    {
        private const string Password = "correct horse battery staple";
        private const string RejectionMessage = "Invalid username or password.";
        private readonly SharedDbFixture _fx;

        public SystemBusinessObjectAuthenticationTests(SharedDbFixture fx) { _fx = fx; }

        private IDbConnectionManager ConnectionManager => _fx.GetRequiredService<IDbConnectionManager>();

        private SystemBusinessObject NewBo() => new(TestPolhemContext.Create(_fx), Guid.Empty, SysProgIds.System);

        /// <summary>
        /// Runs <paramref name="test"/> against a fresh user row whose stored hash is <paramref name="storedHash"/>.
        /// </summary>
        private void WithUser(string storedHash, Action<string> test)
        {
            string userId = TestUsers.Create(ConnectionManager, "auth");
            try
            {
                TestUsers.SetPasswordHash(ConnectionManager, userId, storedHash);
                test(userId);
            }
            finally
            {
                TestUsers.Delete(ConnectionManager, userId);
            }
        }

        private void RemoveSession(Guid accessToken)
        {
            _fx.GetRequiredService<ISessionInfoService>().Remove(accessToken);
            _fx.GetRequiredService<Repository.Abstractions.Factories.IRepositoryFactory>()
                .Create<ISessionRepository>().DeleteSession(accessToken);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Login through the base AuthenticateUser succeeds with the password the stored hash was made from")]
        public void Login_CorrectPassword_Succeeds()
        {
            WithUser(PasswordHasher.HashPassword(Password), userId =>
            {
                var result = NewBo().Login(new LoginArgs { UserId = userId, Password = Password });
                try
                {
                    Assert.NotEqual(Guid.Empty, result.AccessToken);
                    Assert.Equal(userId, result.UserId);
                    Assert.Equal("測試使用者", result.UserName);
                }
                finally
                {
                    RemoveSession(result.AccessToken);
                }
            });
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Login through the base AuthenticateUser falls back to the account id when the stored name is blank")]
        public void Login_BlankName_UsesAccountIdAsUserName()
        {
            WithUser(PasswordHasher.HashPassword(Password), userId =>
            {
                TestUsers.SetName(ConnectionManager, userId, " ");

                var result = NewBo().Login(new LoginArgs { UserId = userId, Password = Password });
                try
                {
                    Assert.Equal(userId, result.UserName);
                }
                finally
                {
                    RemoveSession(result.AccessToken);
                }
            });
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Login through the base AuthenticateUser rejects a wrong password")]
        public void Login_WrongPassword_Throws()
        {
            WithUser(PasswordHasher.HashPassword(Password), userId =>
            {
                var ex = Assert.Throws<UnauthorizedAccessException>(
                    () => NewBo().Login(new LoginArgs { UserId = userId, Password = "wrong " + Password }));
                Assert.Equal(RejectionMessage, ex.Message);
            });
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Login rejects an unknown account with the same message as a wrong password")]
        public void Login_UnknownUser_ThrowsSameMessageAsWrongPassword()
        {
            string unknown = $"nouser-{Guid.NewGuid():N}"[..20];

            var ex = Assert.Throws<UnauthorizedAccessException>(
                () => NewBo().Login(new LoginArgs { UserId = unknown, Password = Password }));

            Assert.Equal(RejectionMessage, ex.Message);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Login rejects an account whose stored hash is blank, whatever password is given")]
        public void Login_BlankStoredHash_Throws()
        {
            WithUser(" ", userId =>
            {
                Assert.Throws<UnauthorizedAccessException>(
                    () => NewBo().Login(new LoginArgs { UserId = userId, Password = string.Empty }));
                Assert.Throws<UnauthorizedAccessException>(
                    () => NewBo().Login(new LoginArgs { UserId = userId, Password = " " }));
            });
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Login rejects the right password when the stored hash is in the retired PBKDF2-SHA1 format")]
        public void Login_LegacySha1Hash_Throws()
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(Password), salt, 1000, HashAlgorithmName.SHA1, 32);
            string legacy = $"1000.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";

            WithUser(legacy, userId =>
                Assert.Throws<UnauthorizedAccessException>(
                    () => NewBo().Login(new LoginArgs { UserId = userId, Password = Password })));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("A successful login replaces a stored hash made with fewer iterations than the current count")]
        public void Login_WeakerStoredHash_IsRehashed()
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(Password), salt, 1000, HashAlgorithmName.SHA256, 32);
            string weak = $"v2.1000.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";

            WithUser(weak, userId =>
            {
                var result = NewBo().Login(new LoginArgs { UserId = userId, Password = Password });
                RemoveSession(result.AccessToken);

                string stored = TestUsers.GetPasswordHash(ConnectionManager, userId);
                Assert.NotEqual(weak, stored);
                Assert.False(PasswordHasher.NeedsRehash(stored));
                Assert.True(PasswordHasher.VerifyPassword(Password, stored));
            });
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Login with an RSA public key returns the session key encrypted for the client's private key")]
        public void Login_WithRsaKeyPair_ReturnsDecryptableSessionKey()
        {
            RsaCryptor.GenerateRsaKeyPair(out var publicKey, out var privateKey);

            WithUser(PasswordHasher.HashPassword(Password), userId =>
            {
                var result = NewBo().Login(new LoginArgs { UserId = userId, Password = Password, ClientPublicKey = publicKey });
                try
                {
                    Assert.NotEmpty(result.ApiEncryptionKey);
                    string sessionKey = RsaCryptor.DecryptWithPrivateKey(result.ApiEncryptionKey, privateKey);
                    var session = _fx.GetRequiredService<ISessionInfoService>().Get(result.AccessToken);
                    Assert.Equal(Convert.ToBase64String(session!.ApiEncryptionKey), sessionKey);
                }
                finally
                {
                    RemoveSession(result.AccessToken);
                }
            });
        }
    }
}
