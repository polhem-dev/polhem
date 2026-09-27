using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using Polhem.Business.Providers;
using Polhem.Base.Exceptions;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Behavior tests for <see cref="DerivedApiEncryptionKeyProvider"/>.
    /// The key point is that the same token and root key always yield the same key. A session rebuilt from
    /// `st_session` gets a usable key back precisely because of this determinism.
    /// </summary>
    public class DerivedApiEncryptionKeyProviderTests
    {
        private static byte[] CreateRootKey(byte seed)
        {
            var key = new byte[64];
            for (int i = 0; i < key.Length; i++) key[i] = (byte)(i + seed);
            return key;
        }

        [Fact]
        [DisplayName("GetKey returns 64 bytes")]
        public void GetKey_Returns64Bytes()
        {
            var provider = new DerivedApiEncryptionKeyProvider(CreateRootKey(0));

            var key = provider.GetKey(Guid.NewGuid());

            Assert.Equal(64, key.Length);
        }

        [Fact]
        [DisplayName("The same token and root key derive the same key")]
        public void GetKey_SameTokenAndRootKey_ReturnsSameKey()
        {
            var token = Guid.NewGuid();
            var a = new DerivedApiEncryptionKeyProvider(CreateRootKey(0)).GetKey(token);
            var b = new DerivedApiEncryptionKeyProvider(CreateRootKey(0)).GetKey(token);

            Assert.Equal(a, b);
        }

        [Fact]
        [DisplayName("GenerateKeyForLogin derives the same key as GetKey for the same token")]
        public void GenerateKeyForLogin_MatchesGetKey()
        {
            var provider = new DerivedApiEncryptionKeyProvider(CreateRootKey(0));
            var token = Guid.NewGuid();

            var generated = provider.GenerateKeyForLogin(token);
            var fetched = provider.GetKey(token);

            Assert.Equal(generated, fetched);
        }

        [Fact]
        [DisplayName("Different tokens derive different keys")]
        public void GetKey_DifferentTokens_ReturnDifferentKeys()
        {
            var provider = new DerivedApiEncryptionKeyProvider(CreateRootKey(0));

            var a = provider.GetKey(Guid.NewGuid());
            var b = provider.GetKey(Guid.NewGuid());

            Assert.NotEqual(a, b);
        }

        [Fact]
        [DisplayName("Different root keys derive different keys (rotating the root key invalidates existing sessions)")]
        public void GetKey_DifferentRootKeys_ReturnDifferentKeys()
        {
            var token = Guid.NewGuid();

            var a = new DerivedApiEncryptionKeyProvider(CreateRootKey(0)).GetKey(token);
            var b = new DerivedApiEncryptionKeyProvider(CreateRootKey(7)).GetKey(token);

            Assert.NotEqual(a, b);
        }

        [Fact]
        [DisplayName("GetKey throws AuthenticationRequiredException for Guid.Empty")]
        public void GetKey_EmptyToken_Throws()
        {
            var provider = new DerivedApiEncryptionKeyProvider(CreateRootKey(0));

            Assert.Throws<AuthenticationRequiredException>(() => provider.GetKey(Guid.Empty));
        }

        [Fact]
        [DisplayName("GenerateKeyForLogin throws ArgumentException for Guid.Empty")]
        public void GenerateKeyForLogin_EmptyToken_Throws()
        {
            var provider = new DerivedApiEncryptionKeyProvider(CreateRootKey(0));

            Assert.Throws<ArgumentException>(() => provider.GenerateKeyForLogin(Guid.Empty));
        }

        [Fact]
        [DisplayName("FromMasterKey derives a stable root key that differs from the master key itself")]
        public void FromMasterKey_DerivesStableRootKey()
        {
            var masterKey = CreateRootKey(3);
            var token = Guid.NewGuid();

            var a = DerivedApiEncryptionKeyProvider.FromMasterKey(masterKey).GetKey(token);
            var b = DerivedApiEncryptionKeyProvider.FromMasterKey(masterKey).GetKey(token);

            Assert.Equal(a, b);
            // This is the fallback when `ApiEncryptionKey` is not set, and it must not amount to using the master key as the root key.
            Assert.NotEqual(new DerivedApiEncryptionKeyProvider(masterKey).GetKey(token), a);
        }

        [Fact]
        [DisplayName("FromMasterKey throws ArgumentException for an empty master key")]
        public void FromMasterKey_EmptyMasterKey_Throws()
        {
            Assert.Throws<ArgumentException>(() => DerivedApiEncryptionKeyProvider.FromMasterKey([]));
        }

        [Fact]
        [DisplayName("The constructor throws ArgumentNullException for a null root key")]
        public void Ctor_NullRootKey_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new DerivedApiEncryptionKeyProvider(null!));
        }

        [Fact]
        [DisplayName("The constructor throws ArgumentException for an empty root key")]
        public void Ctor_EmptyRootKey_Throws()
        {
            Assert.Throws<ArgumentException>(() => new DerivedApiEncryptionKeyProvider([]));
        }

        [Fact]
        [DisplayName("GetKey derives with the HKDF label polhem-api-session-key")]
        public void GetKey_DerivesWithSessionKeyLabel()
        {
            // Changing the label changes every session key and so invalidates live sessions. The other
            // tests here compare the provider with itself and stay green whatever the label is, so this
            // one recomputes the key independently from the literal label.
            var rootKey = CreateRootKey(3);
            var token = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e");
            var provider = new DerivedApiEncryptionKeyProvider(rootKey);

            var expected = DeriveSessionKey(rootKey, token);

            Assert.Equal(expected, provider.GetKey(token));
        }

        [Fact]
        [DisplayName("FromMasterKey derives the root key with the HKDF label polhem-api-encryption-root-key")]
        public void FromMasterKey_DerivesRootKeyWithRootKeyLabel()
        {
            var masterKey = CreateRootKey(9);
            var token = new Guid("7c9e6679-7425-40de-944b-e07fc1f90ae7");
            var provider = DerivedApiEncryptionKeyProvider.FromMasterKey(masterKey);

            var rootKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey, 64, salt: null,
                info: Encoding.UTF8.GetBytes("polhem-api-encryption-root-key"));
            var expected = DeriveSessionKey(rootKey, token);

            Assert.Equal(expected, provider.GetKey(token));
        }

        private static byte[] DeriveSessionKey(byte[] rootKey, Guid token)
        {
            byte[] info = [.. Encoding.UTF8.GetBytes("polhem-api-session-key"), .. token.ToByteArray()];
            return HKDF.DeriveKey(HashAlgorithmName.SHA256, rootKey, 64, salt: null, info: info);
        }
    }
}
