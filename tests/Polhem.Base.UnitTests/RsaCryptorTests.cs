using System.ComponentModel;
using Polhem.Base.Security;

namespace Polhem.Base.UnitTests
{
    /// <summary>
    /// Tests for RSA encryption and decryption.
    /// </summary>
    public class RsaCryptorTests
    {
        [Fact]
        [DisplayName("Decrypting with the private key what the public key encrypted restores the original plaintext")]
        public void EncryptAndDecrypt_ValidKeyPair_ReturnsOriginalText()
        {
            // Arrange
            RsaCryptor.GenerateRsaKeyPair(out var publicKey, out var privateKey);
            string originalText = "aes-session-key-1234567890";

            // Act
            string encrypted = RsaCryptor.EncryptWithPublicKey(originalText, publicKey);
            string decrypted = RsaCryptor.DecryptWithPrivateKey(encrypted, privateKey);

            // Assert
            Assert.False(string.IsNullOrEmpty(encrypted));
            Assert.Equal(originalText, decrypted);
        }

        [Fact]
        [DisplayName("Decrypting with the wrong private key throws")]
        public void Decrypt_WrongPrivateKey_ThrowsException()
        {
            // Arrange
            RsaCryptor.GenerateRsaKeyPair(out var publicKey1, out _);
            RsaCryptor.GenerateRsaKeyPair(out _, out var privateKey2);

            string originalText = "this-will-fail";
            string encrypted = RsaCryptor.EncryptWithPublicKey(originalText, publicKey1);

            // Act & Assert
            Assert.ThrowsAny<Exception>(() =>
            {
                var _ = RsaCryptor.DecryptWithPrivateKey(encrypted, privateKey2);
            });
        }

        [Fact]
        [DisplayName("GenerateRsaKeyPair produces PEM strings (SPKI public key, PKCS#1 private key)")]
        public void GenerateRsaKeyPair_ReturnsPemFormattedStrings()
        {
            RsaCryptor.GenerateRsaKeyPair(out var publicKey, out var privateKey);

            Assert.StartsWith("-----BEGIN PUBLIC KEY-----", publicKey);
            Assert.Contains("-----END PUBLIC KEY-----", publicKey);
            Assert.StartsWith("-----BEGIN RSA PRIVATE KEY-----", privateKey);
            Assert.Contains("-----END RSA PRIVATE KEY-----", privateKey);
        }
    }
}
