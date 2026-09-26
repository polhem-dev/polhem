using System.ComponentModel;
using Polhem.Api.Core.Authorization;
using Polhem.Api.Core.Transformers;
using Polhem.Definition.Settings;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// ApiServiceOptions tests. ApiServiceOptions is a static class, so the tests save and restore the original
    /// implementations to avoid affecting other tests.
    /// </summary>
    /// <remarks>
    /// Restoring in try/finally only holds when tests run serially, so this class joins the
    /// <c>ApiServiceOptionsState</c> collection to serialize with the other test classes that modify the same statics.
    /// </remarks>
    [Collection("ApiServiceOptionsState")]
    public class ApiServiceOptionsTests
    {
        [Fact]
        [DisplayName("The default implementations are the matching built-in types")]
        public void DefaultImplementations_AreBuiltInTypes()
        {
            Assert.IsType<ApiAuthorizationValidator>(ApiServiceOptions.AuthorizationValidator);
            Assert.IsType<ApiPayloadTransformer>(ApiServiceOptions.PayloadTransformer);
            Assert.IsType<MessagePackPayloadSerializer>(ApiServiceOptions.PayloadSerializer);
            Assert.IsType<GzipPayloadCompressor>(ApiServiceOptions.PayloadCompressor);
        }

        [Fact]
        [DisplayName("CurrentSettingsSummary contains the Serializer, Compressor and Encryptor entries")]
        public void CurrentSettingsSummary_ContainsMethodNames()
        {
            var summary = ApiServiceOptions.CurrentSettingsSummary;

            Assert.Contains("Serializer:", summary);
            Assert.Contains("Compressor:", summary);
            Assert.Contains("Encryptor:", summary);
        }

        [Fact]
        [DisplayName("Initialize(ApiPayloadOptions, isDebugMode) creates the compressor and encryptor by name")]
        public void Initialize_WithOptions_SetsImplementations()
        {
            var originalSerializer = ApiServiceOptions.PayloadSerializer;
            var originalCompressor = ApiServiceOptions.PayloadCompressor;
            var originalEncryptor = ApiServiceOptions.PayloadEncryptor;
            try
            {
                var options = new ApiPayloadOptions
                {
                    Compressor = "none",
                    Encryptor = "none"
                };

                ApiServiceOptions.Initialize(options, isDebugMode: true);

                // The serializer is not configured here. Every codec is always available and each request declares its
                // own, so `PayloadSerializer` keeps its default to serve requests that declare none.
                Assert.IsType<MessagePackPayloadSerializer>(ApiServiceOptions.PayloadSerializer);
                Assert.IsType<NoCompressionCompressor>(ApiServiceOptions.PayloadCompressor);
                Assert.IsType<NoEncryptionEncryptor>(ApiServiceOptions.PayloadEncryptor);
            }
            finally
            {
                ApiServiceOptions.PayloadSerializer = originalSerializer;
                ApiServiceOptions.PayloadCompressor = originalCompressor;
                ApiServiceOptions.PayloadEncryptor = originalEncryptor;
            }
        }

        [Fact]
        [DisplayName("Initialize(serializer, compressor, encryptor) uses the supplied instances as they are")]
        public void Initialize_WithInstances_SetsImplementations()
        {
            var originalSerializer = ApiServiceOptions.PayloadSerializer;
            var originalCompressor = ApiServiceOptions.PayloadCompressor;
            var originalEncryptor = ApiServiceOptions.PayloadEncryptor;
            try
            {
                var serializer = new MessagePackPayloadSerializer();
                var compressor = new NoCompressionCompressor();
                var encryptor = new NoEncryptionEncryptor();

                ApiServiceOptions.Initialize(serializer, compressor, encryptor);

                Assert.Same(serializer, ApiServiceOptions.PayloadSerializer);
                Assert.Same(compressor, ApiServiceOptions.PayloadCompressor);
                Assert.Same(encryptor, ApiServiceOptions.PayloadEncryptor);
            }
            finally
            {
                ApiServiceOptions.PayloadSerializer = originalSerializer;
                ApiServiceOptions.PayloadCompressor = originalCompressor;
                ApiServiceOptions.PayloadEncryptor = originalEncryptor;
            }
        }

        [Fact]
        [DisplayName("Initialize throws ArgumentNullException for a null serializer")]
        public void Initialize_NullSerializer_Throws()
        {
            AssertThrowsAndRestore(() =>
                ApiServiceOptions.Initialize(null!, new NoCompressionCompressor(), new NoEncryptionEncryptor()));
        }

        [Fact]
        [DisplayName("Initialize throws ArgumentNullException for a null compressor")]
        public void Initialize_NullCompressor_Throws()
        {
            AssertThrowsAndRestore(() =>
                ApiServiceOptions.Initialize(new MessagePackPayloadSerializer(), null!, new NoEncryptionEncryptor()));
        }

        [Fact]
        [DisplayName("Initialize throws ArgumentNullException for a null encryptor")]
        public void Initialize_NullEncryptor_Throws()
        {
            AssertThrowsAndRestore(() =>
                ApiServiceOptions.Initialize(new MessagePackPayloadSerializer(), new NoCompressionCompressor(), null!));
        }

        /// <summary>
        /// Asserts that <paramref name="initialize"/> throws <see cref="ArgumentNullException"/> and
        /// restores the static implementations afterwards.
        /// </summary>
        /// <remarks>
        /// <see cref="ApiServiceOptions.Initialize(Polhem.Api.Core.Transformers.IApiPayloadSerializer, Polhem.Api.Core.Transformers.IApiPayloadCompressor, Polhem.Api.Core.Transformers.IApiPayloadEncryptor)"/>
        /// assigns its arguments one at a time, so a null in a later position throws after the earlier
        /// ones are already in place. Without the restore, whichever test in this class runs next sees
        /// the no-compression implementation, and the test order changes whenever the namespace does.
        /// </remarks>
        private static void AssertThrowsAndRestore(Action initialize)
        {
            var originalSerializer = ApiServiceOptions.PayloadSerializer;
            var originalCompressor = ApiServiceOptions.PayloadCompressor;
            var originalEncryptor = ApiServiceOptions.PayloadEncryptor;
            try
            {
                Assert.Throws<ArgumentNullException>(initialize);
            }
            finally
            {
                ApiServiceOptions.PayloadSerializer = originalSerializer;
                ApiServiceOptions.PayloadCompressor = originalCompressor;
                ApiServiceOptions.PayloadEncryptor = originalEncryptor;
            }
        }

        [Fact]
        [DisplayName("Setting AuthorizationValidator to null throws ArgumentNullException")]
        public void AuthorizationValidator_SetNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ApiServiceOptions.AuthorizationValidator = null!);
        }

        [Fact]
        [DisplayName("Setting PayloadTransformer to null throws ArgumentNullException")]
        public void PayloadTransformer_SetNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ApiServiceOptions.PayloadTransformer = null!);
        }

        [Fact]
        [DisplayName("Setting PayloadSerializer to null throws ArgumentNullException")]
        public void PayloadSerializer_SetNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ApiServiceOptions.PayloadSerializer = null!);
        }

        [Fact]
        [DisplayName("Setting PayloadCompressor to null throws ArgumentNullException")]
        public void PayloadCompressor_SetNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ApiServiceOptions.PayloadCompressor = null!);
        }

        [Fact]
        [DisplayName("Setting PayloadEncryptor to null throws ArgumentNullException")]
        public void PayloadEncryptor_SetNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ApiServiceOptions.PayloadEncryptor = null!);
        }
    }
}
