using System.ComponentModel;
using Polhem.Business.Providers;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Behavior tests for <see cref="StaticApiEncryptionKeyProvider"/>.
    /// The provider receives a byte[] key through its constructor, so the tests construct it directly without any process-wide static.
    /// </summary>
    public class StaticApiEncryptionKeyProviderTests
    {
        [Fact]
        [DisplayName("GetKey returns the key injected at construction")]
        public void GetKey_ReturnsInjectedKey()
        {
            var key = new byte[64];
            for (int i = 0; i < key.Length; i++) key[i] = (byte)i;
            var provider = new StaticApiEncryptionKeyProvider(key);

            var actual = provider.GetKey(Guid.Empty);

            Assert.Same(key, actual);
        }

        [Fact]
        [DisplayName("GenerateKeyForLogin returns the same shared key as GetKey")]
        public void GenerateKeyForLogin_ReturnsSameSharedKey()
        {
            var key = new byte[64];
            var provider = new StaticApiEncryptionKeyProvider(key);

            var a = provider.GetKey(Guid.NewGuid());
            var b = provider.GenerateKeyForLogin(Guid.NewGuid());

            Assert.Same(a, b);
        }

        [Fact]
        [DisplayName("The constructor throws ArgumentNullException for a null key")]
        public void Ctor_NullKey_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new StaticApiEncryptionKeyProvider(null!));
        }

        [Fact]
        [DisplayName("The constructor throws ArgumentException for an empty byte[]")]
        public void Ctor_EmptyKey_Throws()
        {
            Assert.Throws<ArgumentException>(() => new StaticApiEncryptionKeyProvider(Array.Empty<byte>()));
        }
    }
}
