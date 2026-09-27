using System.ComponentModel;
using Polhem.Api.Core.Transformers;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// ApiPayloadTransformer tests. The <see cref="ApiServiceOptions"/> static state is saved and restored to avoid
    /// affecting other tests.
    /// </summary>
    /// <remarks>
    /// Restoring in try/finally only holds when tests run serially, so this class joins the
    /// <c>ApiServiceOptionsState</c> collection to serialize with the other test classes that modify the same statics.
    /// </remarks>
    [Collection(ApiServiceOptionsStateCollection.Name)]
    public class ApiPayloadTransformerTests
    {
        [Fact]
        [DisplayName("Decode after Encode restores the original string")]
        public void EncodeDecode_String_RoundTrip()
        {
            var transformer = new ApiPayloadTransformer();
            const string original = "哈囉,世界";

            byte[] encoded = transformer.Encode(original, typeof(string));
            var decoded = transformer.Decode(encoded, typeof(string));

            Assert.Equal(original, decoded);
        }

        [Fact]
        [DisplayName("Encode throws ArgumentNullException for a null payload")]
        public void Encode_NullPayload_ThrowsArgumentNullException()
        {
            var transformer = new ApiPayloadTransformer();

            Assert.Throws<ArgumentNullException>(() => transformer.Encode(null!, typeof(string)));
        }

        [Fact]
        [DisplayName("Decode throws ArgumentNullException for a null payload")]
        public void Decode_NullPayload_ThrowsArgumentNullException()
        {
            var transformer = new ApiPayloadTransformer();

            Assert.Throws<ArgumentNullException>(() => transformer.Decode(null!, typeof(string)));
        }

        [Fact]
        [DisplayName("Decode throws InvalidOperationException for a payload that is not a byte array")]
        public void Decode_NonByteArray_ThrowsInvalidOperationException()
        {
            var transformer = new ApiPayloadTransformer();

            var ex = Assert.Throws<InvalidOperationException>(() => transformer.Decode("not-bytes", typeof(string)));
            Assert.IsType<InvalidCastException>(ex.InnerException);
        }

        [Fact]
        [DisplayName("Encrypt and Decrypt return the original bytes with NoEncryptionEncryptor")]
        public void EncryptDecrypt_NoEncryption_ReturnsSameBytes()
        {
            var originalEncryptor = ApiServiceOptions.PayloadEncryptor;
            try
            {
                ApiServiceOptions.PayloadEncryptor = new NoEncryptionEncryptor();
                var transformer = new ApiPayloadTransformer();
                var raw = new byte[] { 1, 2, 3, 4 };
                var key = new byte[] { 9, 9, 9 };

                var encrypted = transformer.Encrypt(raw, key);
                var decrypted = transformer.Decrypt(encrypted, key);

                Assert.Same(raw, encrypted);
                Assert.Same(raw, decrypted);
            }
            finally
            {
                ApiServiceOptions.PayloadEncryptor = originalEncryptor;
            }
        }

        [Fact]
        [DisplayName("Encrypt throws ArgumentNullException for null rawBytes")]
        public void Encrypt_NullBytes_ThrowsArgumentNullException()
        {
            var transformer = new ApiPayloadTransformer();

            Assert.Throws<ArgumentNullException>(() => transformer.Encrypt(null!, new byte[] { 1 }));
        }

        [Fact]
        [DisplayName("Decrypt throws ArgumentNullException for null encryptedBytes")]
        public void Decrypt_NullBytes_ThrowsArgumentNullException()
        {
            var transformer = new ApiPayloadTransformer();

            Assert.Throws<ArgumentNullException>(() => transformer.Decrypt(null!, new byte[] { 1 }));
        }
    }
}
