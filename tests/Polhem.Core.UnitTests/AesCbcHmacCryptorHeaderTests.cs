using System.Buffers.Binary;
using System.ComponentModel;
using System.Security.Cryptography;
using Polhem.Core.Security;

namespace Polhem.Core.UnitTests
{
    /// <summary>
    /// Tests for how <see cref="AesCbcHmacCryptor.Decrypt"/> rejects a header whose IV length AES-CBC cannot use,
    /// both before the HMAC check (out of range) and after it (in range but not a block size).
    /// </summary>
    public class AesCbcHmacCryptorHeaderTests
    {
        [Fact]
        [DisplayName("Decrypt rejects an IV length above the allowed range before checking the HMAC")]
        public void Decrypt_IvLengthAboveRange_Throws()
        {
            AesCbcHmacKeyGenerator.FromCombinedKey(AesCbcHmacKeyGenerator.GenerateCombinedKey(), out var aesKey, out var hmacKey);
            var data = new byte[80];
            BinaryPrimitives.WriteInt32LittleEndian(data, 40);

            var ex = Assert.Throws<CryptographicException>(() => AesCbcHmacCryptor.Decrypt(data, aesKey, hmacKey));
            Assert.Equal("Invalid IV length.", ex.Message);
        }

        [Fact]
        [DisplayName("Decrypt rejects an authenticated IV length that is not the AES block size")]
        public void Decrypt_AuthenticatedIvLengthNotBlockSize_Throws()
        {
            AesCbcHmacKeyGenerator.FromCombinedKey(AesCbcHmacKeyGenerator.GenerateCombinedKey(), out var aesKey, out var hmacKey);
            const int ivLength = 20;
            const int cipherLength = 16;
            int headerLength = 4 + ivLength + 4;
            var data = new byte[headerLength + cipherLength + 32];
            BinaryPrimitives.WriteInt32LittleEndian(data, ivLength);
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4 + ivLength), cipherLength);
            HMACSHA256.HashData(hmacKey, data.AsSpan(0, headerLength + cipherLength), data.AsSpan(headerLength + cipherLength));

            var ex = Assert.Throws<CryptographicException>(() => AesCbcHmacCryptor.Decrypt(data, aesKey, hmacKey));
            Assert.Equal("Invalid IV length.", ex.Message);
        }
    }
}
