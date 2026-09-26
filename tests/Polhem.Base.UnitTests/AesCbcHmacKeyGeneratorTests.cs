using System.ComponentModel;
using Polhem.Base.Security;

namespace Polhem.Base.UnitTests
{
    /// <summary>
    /// Tests the key generation and parsing logic of <see cref="AesCbcHmacKeyGenerator"/>.
    /// </summary>
    public class AesCbcHmacKeyGeneratorTests
    {
        /// <summary>
        /// Verifies that the AES and HMAC keys stay the same after generation, Base64 encoding and parsing.
        /// </summary>
        [Fact]
        [DisplayName("A generated combined key parsed back through Base64 yields the same AES and HMAC keys")]
        public void GenerateAndParseKey_FromCombinedAndBase64_ReturnsConsistentKeys()
        {
            // Arrange
            byte[] combinedKey = AesCbcHmacKeyGenerator.GenerateCombinedKey();

            // Act
            AesCbcHmacKeyGenerator.FromCombinedKey(combinedKey, out var aesKey1, out var hmacKey1);
            string base64 = Convert.ToBase64String(combinedKey);
            AesCbcHmacKeyGenerator.FromBase64CombinedKey(base64, out var aesKey2, out var hmacKey2);

            // Assert
            Assert.Equal(32, aesKey1.Length);
            Assert.Equal(32, hmacKey1.Length);
            Assert.Equal(aesKey1, aesKey2);
            Assert.Equal(hmacKey1, hmacKey2);
        }

        /// <summary>
        /// Verifies that two generated combined keys differ, as secure randomness requires.
        /// </summary>
        [Fact]
        [DisplayName("Each generated combined key is a different random value")]
        public void GenerateCombinedKey_CalledTwice_ReturnsDifferentKeys()
        {
            // Arrange & Act
            var key1 = AesCbcHmacKeyGenerator.GenerateCombinedKey();
            var key2 = AesCbcHmacKeyGenerator.GenerateCombinedKey();

            // Assert
            Assert.NotEqual(Convert.ToBase64String(key1), Convert.ToBase64String(key2));
        }

        /// <summary>
        /// Verifies that a combined key of the wrong length throws <see cref="ArgumentException"/>.
        /// </summary>
        [Fact]
        [DisplayName("FromCombinedKey throws ArgumentException for a combined key of invalid length")]
        public void FromCombinedKey_InvalidLength_ThrowsArgumentException()
        {
            // Arrange
            var invalid = new byte[48];

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                AesCbcHmacKeyGenerator.FromCombinedKey(invalid, out _, out _));
        }
    }
}
