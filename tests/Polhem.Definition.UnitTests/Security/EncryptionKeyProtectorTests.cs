using System.ComponentModel;
using Polhem.Core.Security;
using Polhem.Definition.Security;

namespace Polhem.Definition.UnitTests.Security
{
    /// <summary>
    /// Tests for the normal and error paths of EncryptionKeyProtector.
    /// </summary>
    public class EncryptionKeyProtectorTests
    {
        [Fact]
        [DisplayName("GenerateEncryptedKey with a valid master key can be decrypted back")]
        public void GenerateEncryptedKey_ValidMasterKey_CanBeDecrypted()
        {
            // Arrange
            byte[] masterKey = AesCbcHmacKeyGenerator.GenerateCombinedKey();

            // Act
            string base64 = EncryptionKeyProtector.GenerateEncryptedKey(masterKey);
            byte[] decrypted = EncryptionKeyProtector.DecryptEncryptedKey(masterKey, base64);

            // Assert
            Assert.NotEmpty(base64);
            Assert.Equal(64, decrypted.Length); // 256-bit AES + 256-bit HMAC = 64 bytes
        }

        [Theory]
        [InlineData(null)]
        [InlineData(new byte[0])]
        [DisplayName("GenerateEncryptedKey throws ArgumentException for an empty master key")]
        public void GenerateEncryptedKey_EmptyMasterKey_ThrowsArgumentException(byte[]? masterKey)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => EncryptionKeyProtector.GenerateEncryptedKey(masterKey!));
        }

        [Theory]
        [InlineData(null)]
        [InlineData(new byte[0])]
        [DisplayName("DecryptEncryptedKey throws ArgumentException for an empty master key")]
        public void DecryptEncryptedKey_EmptyMasterKey_ThrowsArgumentException(byte[]? masterKey)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                EncryptionKeyProtector.DecryptEncryptedKey(masterKey!, "SGVsbG8="));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("DecryptEncryptedKey throws ArgumentException for an empty ciphertext")]
        public void DecryptEncryptedKey_EmptyCipherText_ThrowsArgumentException(string? cipherText)
        {
            // Arrange
            byte[] masterKey = AesCbcHmacKeyGenerator.GenerateCombinedKey();

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                EncryptionKeyProtector.DecryptEncryptedKey(masterKey, cipherText!));
        }

        [Fact]
        [DisplayName("DecryptEncryptedKey throws with the wrong master key")]
        public void DecryptEncryptedKey_WrongMasterKey_Throws()
        {
            // Arrange
            byte[] originalKey = AesCbcHmacKeyGenerator.GenerateCombinedKey();
            byte[] wrongKey = AesCbcHmacKeyGenerator.GenerateCombinedKey();
            string base64 = EncryptionKeyProtector.GenerateEncryptedKey(originalKey);

            // Act & Assert
            Assert.ThrowsAny<Exception>(() =>
                EncryptionKeyProtector.DecryptEncryptedKey(wrongKey, base64));
        }

        [Fact]
        [DisplayName("DecryptEncryptedKey throws FormatException for a ciphertext that is not Base64")]
        public void DecryptEncryptedKey_NonBase64CipherText_ThrowsFormatException()
        {
            // Arrange
            byte[] masterKey = AesCbcHmacKeyGenerator.GenerateCombinedKey();

            // Act & Assert
            Assert.Throws<FormatException>(() =>
                EncryptionKeyProtector.DecryptEncryptedKey(masterKey, "@@not-base64@@"));
        }
    }
}
