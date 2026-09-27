using System.ComponentModel;
using Polhem.Base.Security;

namespace Polhem.Base.UnitTests
{
    /// <summary>
    /// Tests the non-reversible forms of an access token that are persisted in its place.
    /// </summary>
    public class AccessTokenHasherTests
    {
        private static readonly Guid s_token = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

        [Fact]
        [DisplayName("ComputeStorageKey is SHA-256 over the token's RFC 4122 bytes, truncated to 16 bytes")]
        public void ComputeStorageKey_KnownToken_MatchesPinnedVector()
        {
            // Vector computed independently: SHA-256 of bytes 00 11 22 ... ee ff. Pinning it keeps the byte order
            // language-neutral, so a tool outside .NET derives the same key.
            Assert.Equal(Guid.Parse("a8faed6a-bbf3-5c12-a4b2-6e40f6feb19d"), AccessTokenHasher.ComputeStorageKey(s_token));
        }

        [Fact]
        [DisplayName("ComputeFingerprint is the first 8 bytes of the same digest as 16 lowercase hex digits")]
        public void ComputeFingerprint_KnownToken_MatchesPinnedVector()
        {
            Assert.Equal("a8faed6abbf35c12", AccessTokenHasher.ComputeFingerprint(s_token));
        }

        [Fact]
        [DisplayName("The fingerprint is a prefix of the storage key, so a log row can be matched to its session row")]
        public void ComputeFingerprint_IsPrefixOfStorageKey()
        {
            var token = Guid.NewGuid();

            string key = AccessTokenHasher.ComputeStorageKey(token).ToString("N");

            Assert.StartsWith(AccessTokenHasher.ComputeFingerprint(token)!, key, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Neither derived form contains the token")]
        public void DerivedForms_DoNotContainToken()
        {
            var token = Guid.NewGuid();

            Assert.NotEqual(token, AccessTokenHasher.ComputeStorageKey(token));
            Assert.DoesNotContain(AccessTokenHasher.ComputeFingerprint(token)!, token.ToString("N"), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("ComputeStorageKey is deterministic and separates different tokens")]
        public void ComputeStorageKey_Deterministic_AndDistinct()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();

            Assert.Equal(AccessTokenHasher.ComputeStorageKey(a), AccessTokenHasher.ComputeStorageKey(a));
            Assert.NotEqual(AccessTokenHasher.ComputeStorageKey(a), AccessTokenHasher.ComputeStorageKey(b));
        }

        [Fact]
        [DisplayName("ComputeFingerprint returns null for Guid.Empty, which stands for no session")]
        public void ComputeFingerprint_EmptyToken_ReturnsNull()
        {
            Assert.Null(AccessTokenHasher.ComputeFingerprint(Guid.Empty));
        }
    }
}
