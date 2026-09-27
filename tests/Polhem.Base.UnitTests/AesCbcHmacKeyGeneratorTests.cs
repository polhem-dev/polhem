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
        /// Verifies that a generated combined key splits into a 32-byte AES key and a 32-byte HMAC key.
        /// </summary>
        [Fact]
        [DisplayName("A generated combined key splits into 32-byte AES and HMAC keys")]
        public void FromCombinedKey_GeneratedKey_ReturnsTwo32ByteKeys()
        {
            byte[] combinedKey = AesCbcHmacKeyGenerator.GenerateCombinedKey();

            AesCbcHmacKeyGenerator.FromCombinedKey(combinedKey, out var aesKey, out var hmacKey);

            Assert.Equal(32, aesKey.Length);
            Assert.Equal(32, hmacKey.Length);
            Assert.Equal(combinedKey[..32], aesKey);
            Assert.Equal(combinedKey[32..], hmacKey);
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
