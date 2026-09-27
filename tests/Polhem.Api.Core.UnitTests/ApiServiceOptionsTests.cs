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
        [DisplayName("Initialize(ApiPayloadOptions, isDebugMode) creates the compressor and encryptor by name")]
        public void Initialize_WithOptions_SetsImplementations()
        {
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
                // own; `PayloadSerializer` is fixed and serves requests that declare none.
                Assert.IsType<MessagePackPayloadSerializer>(ApiServiceOptions.PayloadSerializer);
                Assert.IsType<NoCompressionCompressor>(ApiServiceOptions.PayloadCompressor);
                Assert.IsType<NoEncryptionEncryptor>(ApiServiceOptions.PayloadEncryptor);
            }
            finally
            {
                ApiServiceOptions.PayloadCompressor = originalCompressor;
                ApiServiceOptions.PayloadEncryptor = originalEncryptor;
            }
        }

        [Fact]
        [DisplayName("Initialize(compressor, encryptor) uses the supplied instances as they are")]
        public void Initialize_WithInstances_SetsImplementations()
        {
            var originalCompressor = ApiServiceOptions.PayloadCompressor;
            var originalEncryptor = ApiServiceOptions.PayloadEncryptor;
            try
            {
                var compressor = new NoCompressionCompressor();
                var encryptor = new NoEncryptionEncryptor();

                ApiServiceOptions.Initialize(compressor, encryptor);

                Assert.Same(compressor, ApiServiceOptions.PayloadCompressor);
                Assert.Same(encryptor, ApiServiceOptions.PayloadEncryptor);
            }
            finally
            {
                ApiServiceOptions.PayloadCompressor = originalCompressor;
                ApiServiceOptions.PayloadEncryptor = originalEncryptor;
            }
        }

        [Fact]
        [DisplayName("Initialize throws ArgumentNullException for a null compressor")]
        public void Initialize_NullCompressor_Throws()
        {
            AssertThrowsAndRestore(() =>
                ApiServiceOptions.Initialize(null!, new NoEncryptionEncryptor()));
        }

        [Fact]
        [DisplayName("Initialize throws ArgumentNullException for a null encryptor")]
        public void Initialize_NullEncryptor_Throws()
        {
            AssertThrowsAndRestore(() =>
                ApiServiceOptions.Initialize(new NoCompressionCompressor(), null!));
        }

        /// <summary>
        /// Asserts that <paramref name="initialize"/> throws <see cref="ArgumentNullException"/> and
        /// restores the static implementations afterwards.
        /// </summary>
        /// <remarks>
        /// <see cref="ApiServiceOptions.Initialize(Polhem.Api.Core.Transformers.IApiPayloadCompressor, Polhem.Api.Core.Transformers.IApiPayloadEncryptor)"/>
        /// assigns its arguments one at a time, so a null in a later position throws after the earlier
        /// ones are already in place. Without the restore, whichever test in this class runs next sees
        /// the no-compression implementation, and the test order changes whenever the namespace does.
        /// </remarks>
        private static void AssertThrowsAndRestore(Action initialize)
        {
            var originalCompressor = ApiServiceOptions.PayloadCompressor;
            var originalEncryptor = ApiServiceOptions.PayloadEncryptor;
            try
            {
                Assert.Throws<ArgumentNullException>(initialize);
            }
            finally
            {
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

        [Fact]
        [DisplayName("A registered codec resolves by its name and is listed as accepted")]
        public void RegisterPayloadCodec_CustomCodec_ResolvesByName()
        {
            var codec = new NamedCodec("test-codec");
            try
            {
                ApiServiceOptions.RegisterPayloadCodec(codec);

                Assert.Same(codec, ApiServiceOptions.ResolvePayloadSerializer("test-codec"));
                Assert.Contains("test-codec", ApiServiceOptions.AcceptedPayloadCodecs);
            }
            finally
            {
                ApiServiceOptions.ResetPayloadCodecs();
            }
        }

        [Fact]
        [DisplayName("Registering a codec leaves an undeclared codec reading as MessagePack")]
        public void RegisterPayloadCodec_CustomCodec_DoesNotChangeUndeclaredDefault()
        {
            try
            {
                ApiServiceOptions.RegisterPayloadCodec(new NamedCodec("test-codec"));

                Assert.IsType<MessagePackPayloadSerializer>(ApiServiceOptions.ResolvePayloadSerializer(null));
                Assert.IsType<MessagePackPayloadSerializer>(ApiServiceOptions.ResolvePayloadSerializer(string.Empty));
            }
            finally
            {
                ApiServiceOptions.ResetPayloadCodecs();
            }
        }

        [Theory]
        [InlineData(PayloadCodecNames.MessagePack)]
        [InlineData(PayloadCodecNames.Json)]
        [DisplayName("Registering a codec under a built-in name throws InvalidOperationException")]
        public void RegisterPayloadCodec_BuiltInName_Throws(string name)
        {
            Assert.Throws<InvalidOperationException>(() => ApiServiceOptions.RegisterPayloadCodec(new NamedCodec(name)));
            Assert.IsNotType<NamedCodec>(ApiServiceOptions.ResolvePayloadSerializer(name));
        }

        [Theory]
        [InlineData("")]
        [InlineData("Upper")]
        [InlineData("has space")]
        [DisplayName("Registering a codec with a malformed name throws ArgumentException")]
        public void RegisterPayloadCodec_MalformedName_Throws(string name)
        {
            Assert.Throws<ArgumentException>(() => ApiServiceOptions.RegisterPayloadCodec(new NamedCodec(name)));
        }

        [Fact]
        [DisplayName("Registering a null codec throws ArgumentNullException")]
        public void RegisterPayloadCodec_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ApiServiceOptions.RegisterPayloadCodec(null!));
        }

        /// <summary>
        /// A codec that only reports a name; the registration tests never serialize with it.
        /// </summary>
        private sealed class NamedCodec : IApiPayloadSerializer
        {
            public NamedCodec(string name) { SerializationMethod = name; }

            public string SerializationMethod { get; }

            public byte[] Serialize(object payload, Type type) => throw new NotSupportedException();

            public object? Deserialize(byte[] bytes, Type type) => throw new NotSupportedException();
        }
    }
}
