using System.ComponentModel;
using Polhem.Base.Security;
using System.Security.Cryptography;
using System.Text;

namespace Polhem.Base.UnitTests
{
    /// <summary>
    /// Tests for the AesCbcHmacCryptor encryption and decryption logic.
    /// </summary>
    public class AesCbcHmacCryptorTests
    {
        private readonly byte[] _aesKey = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"); // 32 bytes
        private readonly byte[] _hmacKey = Encoding.UTF8.GetBytes("abcdef0123456789abcdef0123456789"); // 32 bytes

        [Fact]
        [DisplayName("Decrypting the ciphertext restores the original plaintext")]
        public void EncryptAndDecrypt_ValidPlaintext_ReturnsOriginalText()
        {
            // Arrange
            string originalText = "Polhem 測試資料內容";
            byte[] plainBytes = Encoding.UTF8.GetBytes(originalText);

            // Act
            byte[] encrypted = AesCbcHmacCryptor.Encrypt(plainBytes, _aesKey, _hmacKey);
            byte[] decrypted = AesCbcHmacCryptor.Decrypt(encrypted, _aesKey, _hmacKey);
            string decryptedText = Encoding.UTF8.GetString(decrypted);

            // Assert
            Assert.Equal(originalText, decryptedText);
        }

        [Fact]
        [DisplayName("Encrypting the same plaintext twice produces different ciphertexts")]
        public void Encrypt_SamePlaintext_ProducesDifferentCiphertext()
        {
            // Arrange
            byte[] plainBytes = Encoding.UTF8.GetBytes("相同內容測試");

            // Act
            byte[] encrypted1 = AesCbcHmacCryptor.Encrypt(plainBytes, _aesKey, _hmacKey);
            byte[] encrypted2 = AesCbcHmacCryptor.Encrypt(plainBytes, _aesKey, _hmacKey);

            // Assert
            Assert.NotEqual(Convert.ToBase64String(encrypted1), Convert.ToBase64String(encrypted2));
        }

        [Fact]
        [DisplayName("Decrypting tampered ciphertext throws CryptographicException")]
        public void Decrypt_TamperedCiphertext_ThrowsCryptographicException()
        {
            // Arrange
            byte[] plainBytes = Encoding.UTF8.GetBytes("敏感資料");
            byte[] encrypted = AesCbcHmacCryptor.Encrypt(plainBytes, _aesKey, _hmacKey);

            encrypted[encrypted.Length - 10] ^= 0xFF;

            // Act & Assert
            Assert.Throws<CryptographicException>(() =>
            {
                AesCbcHmacCryptor.Decrypt(encrypted, _aesKey, _hmacKey);
            });
        }

        [Fact]
        [DisplayName("Decrypting null data throws CryptographicException")]
        public void Decrypt_NullData_ThrowsCryptographicException()
        {
            Assert.Throws<CryptographicException>(() =>
            {
                AesCbcHmacCryptor.Decrypt(null!, _aesKey, _hmacKey);
            });
        }

        [Fact]
        [DisplayName("Decrypting data that is too short throws CryptographicException")]
        public void Decrypt_TooShortData_ThrowsCryptographicException()
        {
            // The minimum valid length is 72 bytes.
            byte[] tooShort = new byte[50];

            Assert.Throws<CryptographicException>(() =>
            {
                AesCbcHmacCryptor.Decrypt(tooShort, _aesKey, _hmacKey);
            });
        }

        [Fact]
        [DisplayName("Decrypting data with an invalid IV length throws CryptographicException")]
        public void Decrypt_InvalidIvLength_ThrowsCryptographicException()
        {
            // Build data whose `ivLength` is negative.
            byte[] malicious = new byte[80];
            BitConverter.GetBytes(-1).CopyTo(malicious, 0); // ivLength = -1

            Assert.Throws<CryptographicException>(() =>
            {
                AesCbcHmacCryptor.Decrypt(malicious, _aesKey, _hmacKey);
            });
        }

        [Fact]
        [DisplayName("Decrypting data with an oversized cipher length throws CryptographicException")]
        public void Decrypt_OversizedCipherLength_ThrowsCryptographicException()
        {
            // Build data whose `ivLength` is valid but whose `cipherLength` far exceeds the actual data.
            byte[] malicious = new byte[80];
            BitConverter.GetBytes(16).CopyTo(malicious, 0);            // ivLength = 16 (valid)
            BitConverter.GetBytes(int.MaxValue).CopyTo(malicious, 20); // cipherLength = MaxValue at offset 4 + 16

            Assert.Throws<CryptographicException>(() =>
            {
                AesCbcHmacCryptor.Decrypt(malicious, _aesKey, _hmacKey);
            });
        }
    }
}
