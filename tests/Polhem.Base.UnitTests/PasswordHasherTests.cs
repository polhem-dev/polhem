using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using Polhem.Base.Security;

namespace Polhem.Base.UnitTests
{
    /// <summary>
    /// Tests password hashing and verification in PasswordHasher.
    /// </summary>
    public class PasswordHasherTests
    {
        /// <summary>
        /// Verifies that the same password passes hash verification.
        /// </summary>
        [Fact]
        [DisplayName("The correct password passes hash verification")]
        public void VerifyPassword_CorrectPassword_ReturnsTrue()
        {
            // Arrange
            string originalPassword = "MySecurePassword!123";

            // Act
            string hashedPassword = PasswordHasher.HashPassword(originalPassword);
            bool result = PasswordHasher.VerifyPassword(originalPassword, hashedPassword);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Verifies that a wrong password fails hash verification.
        /// </summary>
        [Fact]
        [DisplayName("A wrong password fails hash verification")]
        public void VerifyPassword_IncorrectPassword_ReturnsFalse()
        {
            // Arrange
            string originalPassword = "MySecurePassword!123";
            string wrongPassword = "WrongPassword";
            string hashedPassword = PasswordHasher.HashPassword(originalPassword);

            // Act
            bool result = PasswordHasher.VerifyPassword(wrongPassword, hashedPassword);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Verifies that a malformed hash string fails verification.
        /// </summary>
        [Fact]
        [DisplayName("A hash string with an invalid format fails verification")]
        public void VerifyPassword_InvalidHashFormat_ReturnsFalse()
        {
            // Arrange
            string password = "any";
            string invalidHash = "not.a.valid.hash";

            // Act
            bool result = PasswordHasher.VerifyPassword(password, invalidHash);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// The unprefixed PBKDF2-SHA1 format inherited from Bee.NET no longer verifies, even with the right password.
        /// </summary>
        [Fact]
        [DisplayName("A hash in the retired PBKDF2-SHA1 format fails verification even with the right password")]
        public void VerifyPassword_LegacySha1Format_ReturnsFalse()
        {
            // The legacy format is `{iterations}.{saltBase64}.{hashBase64}`, without the `v2.` prefix.
            const string password = "LegacyPassword!";
            const int iterations = 10000;
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA1, 32);
            string legacyHash = $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";

            Assert.False(PasswordHasher.VerifyPassword(password, legacyHash));
        }

        [Fact]
        [DisplayName("HashPassword stores the current iteration count, which is at least 600,000")]
        public void HashPassword_UsesCurrentIterationCount()
        {
            string stored = PasswordHasher.HashPassword("pw");

            Assert.InRange(PasswordHasher.Iterations, 600_000, int.MaxValue);
            Assert.StartsWith($"v2.{PasswordHasher.Iterations}.", stored, StringComparison.Ordinal);
            Assert.False(PasswordHasher.NeedsRehash(stored));
        }

        [Fact]
        [DisplayName("A v2 hash made with fewer iterations still verifies and is reported as needing a rehash")]
        public void VerifyPassword_WeakerV2Hash_VerifiesAndNeedsRehash()
        {
            string weak = WeakV2Hash("pw", 1000);

            Assert.True(PasswordHasher.VerifyPassword("pw", weak));
            Assert.False(PasswordHasher.VerifyPassword("other", weak));
            Assert.True(PasswordHasher.NeedsRehash(weak));
        }

        [Theory]
        [InlineData("")]
        [InlineData("not.a.valid.hash")]
        [InlineData("1000.AAAAAAAAAAAAAAAAAAAAAA==.AAAAAAAAAAAAAAAAAAAAAA==")]
        [DisplayName("NeedsRehash reports a value it cannot parse as needing a new hash")]
        public void NeedsRehash_Unparsable_ReturnsTrue(string stored)
        {
            Assert.True(PasswordHasher.NeedsRehash(stored));
        }

        private static string WeakV2Hash(string password, int iterations)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, 32);
            return $"v2.{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        /// <summary>
        /// A v2 prefix with a malformed body fails verification.
        /// </summary>
        [Fact]
        [DisplayName("A v2 prefix with a malformed body fails verification")]
        public void VerifyPassword_V2PrefixWithInvalidInner_ReturnsFalse()
        {
            // A part count other than three returns false directly.
            Assert.False(PasswordHasher.VerifyPassword("any", "v2.only.two"));
            // A Base64 parse failure is caught and returns false.
            Assert.False(PasswordHasher.VerifyPassword("any", "v2.100000.!!!bad-base64!!!.xx"));
        }

        /// <summary>
        /// A stored value with an empty hash segment must not verify any password.
        /// </summary>
        /// <remarks>
        /// This guards against a bypass that was reproduced: asking PBKDF2 for 0 output bytes returns an empty array,
        /// and <c>CryptographicOperations.FixedTimeEquals</c> treats two empty spans as equal, so a stored value such as
        /// <c>v2.100000..</c> returned true for <b>any</b> password. The unprefixed legacy shape is kept among the
        /// cases because it once had a verifier of its own. The same applies to an empty salt.
        /// </remarks>
        [Theory]
        [InlineData("v2.100000..")]
        [InlineData("v2.1..")]
        [InlineData("1..")]
        [InlineData("v2.100000.AAAAAAAAAAAAAAAAAAAAAA==.")]
        [InlineData("v2.100000..AAAAAAAAAAAAAAAAAAAAAA==")]
        [DisplayName("A stored value with an empty hash or salt segment fails verification (never true for an arbitrary password)")]
        public void VerifyPassword_EmptyHashOrSaltSegment_ReturnsFalse(string storedHash)
        {
            Assert.False(PasswordHasher.VerifyPassword("any password at all", storedHash));
            Assert.False(PasswordHasher.VerifyPassword(string.Empty, storedHash));
        }

        /// <summary>
        /// Control case: with non-empty segments, verification still tells the correct and wrong passwords apart.
        /// </summary>
        /// <remarks>
        /// Without this test, the theory above could be satisfied by always returning false, which would lock everyone out.
        /// </remarks>
        [Fact]
        [DisplayName("Control case: verification is unchanged when the segments are non-empty")]
        public void VerifyPassword_NonEmptySegments_StillDiscriminates()
        {
            string stored = PasswordHasher.HashPassword("correct horse");

            Assert.True(PasswordHasher.VerifyPassword("correct horse", stored));
            Assert.False(PasswordHasher.VerifyPassword("wrong horse", stored));
        }
    }
}
