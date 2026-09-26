using System.Security.Cryptography;
using Polhem.Api.Core.Transformers;
using Polhem.Base.Security;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Security tests for AesPayloadEncryptor.
    /// </summary>
    public class AesPayloadEncryptorTests
    {
        private readonly AesPayloadEncryptor _encryptor = new AesPayloadEncryptor();

        [Fact(DisplayName = "Encrypt throws CryptographicException for a null key")]
        public void Encrypt_NullKey_ThrowsCryptographicException()
        {
            var data = new byte[] { 1, 2, 3 };

            Assert.Throws<CryptographicException>(() =>
                _encryptor.Encrypt(data, null!));
        }

        [Fact(DisplayName = "Encrypt throws CryptographicException for an empty key")]
        public void Encrypt_EmptyKey_ThrowsCryptographicException()
        {
            var data = new byte[] { 1, 2, 3 };

            Assert.Throws<CryptographicException>(() =>
                _encryptor.Encrypt(data, Array.Empty<byte>()));
        }

        [Fact(DisplayName = "Decrypt throws CryptographicException for a null key")]
        public void Decrypt_NullKey_ThrowsCryptographicException()
        {
            var data = new byte[] { 1, 2, 3 };

            Assert.Throws<CryptographicException>(() =>
                _encryptor.Decrypt(data, null!));
        }

        [Fact(DisplayName = "Decrypt throws CryptographicException for an empty key")]
        public void Decrypt_EmptyKey_ThrowsCryptographicException()
        {
            var data = new byte[] { 1, 2, 3 };

            Assert.Throws<CryptographicException>(() =>
                _encryptor.Decrypt(data, Array.Empty<byte>()));
        }

        [Fact(DisplayName = "Encrypt and Decrypt round-trip the data with a valid key")]
        public void Encrypt_Decrypt_ValidKey_RoundTrip()
        {
            var originalData = new byte[] { 10, 20, 30, 40, 50 };
            var key = AesCbcHmacKeyGenerator.GenerateCombinedKey();

            var encrypted = _encryptor.Encrypt(originalData, key);
            var decrypted = _encryptor.Decrypt(encrypted, key);

            Assert.Equal(originalData, decrypted);
        }
    }
}
