using System.ComponentModel;
using System.Security.Cryptography;
using Polhem.Core.Security;
using Polhem.JsonRpc.Payload;

namespace Polhem.Api.Core.UnitTests.Payload
{
    /// <summary>
    /// The payload package carries its own AES-CBC-HMAC (polhem-jsonrpc ADR-002, decision 6), and Polhem keeps
    /// <see cref="AesCbcHmacCryptor"/> for its settings files. These tests are the only thing that keeps the two in
    /// agreement: each one decrypts what the other encrypts, with the 64-byte key split the same way.
    /// </summary>
    public class PayloadEncryptionCrossTests
    {
        public static TheoryData<int> Lengths => [0, 1, 15, 16, 17, 1000];

        [Theory]
        [MemberData(nameof(Lengths))]
        [DisplayName("Cross encryption: data the payload package encrypts decrypts with Polhem.Core's AES-CBC-HMAC")]
        public void PackageEncrypt_CoreDecrypt_RoundTrips(int length)
        {
            var key = RandomNumberGenerator.GetBytes(AesCbcHmacPayloadEncryptor.KeySize);
            var data = RandomNumberGenerator.GetBytes(length);

            var encrypted = new AesCbcHmacPayloadEncryptor().Encrypt(data, key);
            AesCbcHmacKeyGenerator.FromCombinedKey(key, out var aesKey, out var hmacKey);

            Assert.Equal(data, AesCbcHmacCryptor.Decrypt(encrypted, aesKey, hmacKey));
        }

        [Theory]
        [MemberData(nameof(Lengths))]
        [DisplayName("Cross encryption: data Polhem.Core's AES-CBC-HMAC encrypts decrypts with the payload package")]
        public void CoreEncrypt_PackageDecrypt_RoundTrips(int length)
        {
            var key = RandomNumberGenerator.GetBytes(AesCbcHmacPayloadEncryptor.KeySize);
            var data = RandomNumberGenerator.GetBytes(length);
            AesCbcHmacKeyGenerator.FromCombinedKey(key, out var aesKey, out var hmacKey);

            var encrypted = AesCbcHmacCryptor.Encrypt(data, aesKey, hmacKey);

            Assert.Equal(data, new AesCbcHmacPayloadEncryptor().Decrypt(encrypted, key));
        }
    }
}
