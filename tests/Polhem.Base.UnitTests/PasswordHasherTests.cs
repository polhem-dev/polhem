using System.ComponentModel;
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
        /// Verifies that a hash string in the legacy SHA1 format still verifies (backward compatibility).
        /// </summary>
        [Fact]
        [DisplayName("A hash in the legacy SHA1 format passes verification")]
        public void VerifyPassword_LegacySha1Format_ReturnsTrue()
        {
            // Arrange: the legacy format is `{iterations}.{saltBase64}.{hashBase64}`, without the `v2.` prefix.
            const string password = "LegacyPassword!";
            const int iterations = 10000;
            byte[] salt = new byte[16];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
                rng.GetBytes(salt);
            byte[] hash = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
                System.Text.Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                System.Security.Cryptography.HashAlgorithmName.SHA1,
                32);
            string legacyHash = $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";

            // Act
            bool correct = PasswordHasher.VerifyPassword(password, legacyHash);
            bool wrong = PasswordHasher.VerifyPassword("WrongPassword", legacyHash);

            // Assert
            Assert.True(correct);
            Assert.False(wrong);
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
        /// <c>v2.100000..</c> returned true for <b>any</b> password. The v2 and legacy branches parse and compare
        /// separately, so both are tested. The same applies to an empty salt.
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
