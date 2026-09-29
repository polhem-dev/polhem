using System.ComponentModel;
using Polhem.Core.Security;

namespace Polhem.Core.UnitTests
{
    /// <summary>
    /// Unit tests for ApiKeyHasher: the salt + SHA-256 round trip and the rejection cases.
    /// </summary>
    public class ApiKeyHasherTests
    {
        [Fact]
        [DisplayName("A hash produced by HashSecret passes VerifySecret")]
        public void HashSecret_RoundTrip_Verifies()
        {
            string secret = ApiKeyFormat.CreateSecret();

            string hashed = ApiKeyHasher.HashSecret(secret);

            Assert.True(ApiKeyHasher.VerifySecret(secret, hashed));
        }

        [Fact]
        [DisplayName("HashSecret produces different hashes for the same secret twice (random salt)")]
        public void HashSecret_SameSecretTwice_ProducesDifferentHashes()
        {
            string secret = ApiKeyFormat.CreateSecret();

            string first = ApiKeyHasher.HashSecret(secret);
            string second = ApiKeyHasher.HashSecret(secret);

            Assert.NotEqual(first, second);
            Assert.True(ApiKeyHasher.VerifySecret(secret, first));
            Assert.True(ApiKeyHasher.VerifySecret(secret, second));
        }

        [Fact]
        [DisplayName("HashSecret stores the hash with the v1. version prefix in a three-part format")]
        public void HashSecret_UsesVersionedThreePartFormat()
        {
            string hashed = ApiKeyHasher.HashSecret("secret-value");

            Assert.StartsWith("v1.", hashed, StringComparison.Ordinal);
            Assert.Equal(3, hashed.Split('.').Length);
        }

        [Fact]
        [DisplayName("VerifySecret returns false when the secret does not match")]
        public void VerifySecret_WrongSecret_ReturnsFalse()
        {
            string hashed = ApiKeyHasher.HashSecret(ApiKeyFormat.CreateSecret());

            Assert.False(ApiKeyHasher.VerifySecret(ApiKeyFormat.CreateSecret(), hashed));
        }

        [Theory]
        [DisplayName("VerifySecret fails closed on a malformed stored hash")]
        [InlineData("")]
        [InlineData("not-versioned")]
        [InlineData("v1.only-two-parts")]
        [InlineData("v1.!!!notbase64!!!.!!!notbase64!!!")]
        [InlineData("v2.c2FsdA==.aGFzaA==")]
        public void VerifySecret_MalformedStoredHash_ReturnsFalse(string hashedSecret)
        {
            Assert.False(ApiKeyHasher.VerifySecret("any-secret", hashedSecret));
        }

        [Theory]
        [DisplayName("VerifySecret returns false when the secret is null or empty")]
        [InlineData(null)]
        [InlineData("")]
        public void VerifySecret_EmptySecret_ReturnsFalse(string? secret)
        {
            string hashed = ApiKeyHasher.HashSecret("real-secret");

            Assert.False(ApiKeyHasher.VerifySecret(secret, hashed));
        }
    }
}
