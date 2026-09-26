using System.ComponentModel;
using Polhem.Api.Core.Transformers;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// NoEncryptionEncryptor tests. They test behavior only; the security rules forbid using this class in production.
    /// </summary>
    public class NoEncryptionEncryptorTests
    {
        [Fact]
        [DisplayName("EncryptionMethod is \"none\"")]
        public void EncryptionMethod_IsNone()
        {
            var encryptor = new NoEncryptionEncryptor();

            Assert.Equal("none", encryptor.EncryptionMethod);
        }

        [Fact]
        [DisplayName("Encrypt returns the original byte array")]
        public void Encrypt_ReturnsSameBytes()
        {
            var encryptor = new NoEncryptionEncryptor();
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var key = new byte[] { 9, 8, 7 };

            var result = encryptor.Encrypt(data, key);

            Assert.Same(data, result);
        }

        [Fact]
        [DisplayName("Decrypt returns the original byte array")]
        public void Decrypt_ReturnsSameBytes()
        {
            var encryptor = new NoEncryptionEncryptor();
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var key = new byte[] { 9, 8, 7 };

            var result = encryptor.Decrypt(data, key);

            Assert.Same(data, result);
        }
    }
}
