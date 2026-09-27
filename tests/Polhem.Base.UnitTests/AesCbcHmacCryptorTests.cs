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

        [Fact]
        [DisplayName("Decrypt reads a ciphertext produced by the stream-based implementation that preceded the span rewrite")]
        public void Decrypt_CiphertextFromPreviousImplementation_ReturnsPlaintext()
        {
            // Produced by the earlier `BinaryWriter` implementation with these keys. Any change to the
            // layout [ivLength][iv][cipherLength][ciphertext][HMAC] makes this fail, which is the point:
            // payloads encrypted by an older client or server must still decrypt.
            byte[] aesKey = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
            byte[] hmacKey = Enumerable.Range(100, 32).Select(i => (byte)i).ToArray();
            byte[] previous = Convert.FromHexString(
                "100000009F578525F692144CF84293BA6A8BA5F520000000DC37D86CABD9F6C953E02B9D7F84C8DD" +
                "C84FB3D32B24ACD67B341F2E607382AE42D92EE0C732E9031AD077C7774BDEC9430439832B064A09" +
                "A54ACF9CAB0A5CC1");

            byte[] decrypted = AesCbcHmacCryptor.Decrypt(previous, aesKey, hmacKey);

            Assert.Equal("Polhem wire format pin", Encoding.UTF8.GetString(decrypted));
        }

        [Fact]
        [DisplayName("Encrypt writes a 16-byte IV, the ciphertext length and a 32-byte HMAC around a block-aligned ciphertext")]
        public void Encrypt_AnyInput_WritesDocumentedLayout()
        {
            byte[] plain = Encoding.UTF8.GetBytes("layout");

            byte[] encrypted = AesCbcHmacCryptor.Encrypt(plain, _aesKey, _hmacKey);

            Assert.Equal(16, BitConverter.ToInt32(encrypted, 0));
            int cipherLength = BitConverter.ToInt32(encrypted, 20);
            Assert.Equal(16, cipherLength);
            Assert.Equal(4 + 16 + 4 + cipherLength + 32, encrypted.Length);
        }
    }
}
