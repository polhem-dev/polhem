using System.ComponentModel;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Tests for the ApiPayloadConverter transforms and its TypeName allow list (pure logic, no dependency on process-wide state).
    /// </summary>
    public class ApiPayloadConverterTests
    {
        private static byte[] MakeKey()
        {
            // AES-CBC-HMAC needs a 64-byte combined key (32 bytes AES + 32 bytes HMAC).
            var key = new byte[64];
            for (int i = 0; i < key.Length; i++) key[i] = (byte)i;
            return key;
        }

        [Fact]
        [DisplayName("TransformTo Plain sets Format and returns without changing the value")]
        public void TransformTo_Plain_SetsFormatAndReturns()
        {
            var payload = new JsonRpcParams { Value = "hello" };

            ApiPayloadConverter.TransformTo(payload, PayloadFormat.Plain);

            Assert.Equal(PayloadFormat.Plain, payload.Format);
            Assert.Equal("hello", payload.Value);
        }

        [Fact]
        [DisplayName("TransformTo Encoded throws InvalidOperationException when Value is null")]
        public void TransformTo_Encoded_NullValue_Throws()
        {
            var payload = new JsonRpcParams { Value = null };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                ApiPayloadConverter.TransformTo(payload, PayloadFormat.Encoded));

            Assert.Contains("Payload.Value", ex.Message);
        }

        [Fact]
        [DisplayName("TransformTo Encrypted throws InvalidOperationException when the key is null")]
        public void TransformTo_Encrypted_NullKey_Throws()
        {
            var payload = new JsonRpcParams { Value = "hello" };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                ApiPayloadConverter.TransformTo(payload, PayloadFormat.Encrypted, null));

            Assert.Contains("Encryption key", ex.Message);
        }

        [Fact]
        [DisplayName("TransformTo Encrypted throws InvalidOperationException when the key is an empty array")]
        public void TransformTo_Encrypted_EmptyKey_Throws()
        {
            var payload = new JsonRpcParams { Value = "hello" };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                ApiPayloadConverter.TransformTo(payload, PayloadFormat.Encrypted, Array.Empty<byte>()));

            Assert.Contains("Encryption key", ex.Message);
        }

        [Fact]
        [DisplayName("RestoreFrom Encoded after TransformTo Encoded restores the original string")]
        public void TransformTo_Encoded_RoundTrip_RestoresOriginalValue()
        {
            var payload = new JsonRpcParams { Value = "哈囉,世界" };

            ApiPayloadConverter.TransformTo(payload, PayloadFormat.Encoded);

            Assert.Equal(PayloadFormat.Encoded, payload.Format);
            Assert.IsType<byte[]>(payload.Value);
            Assert.False(string.IsNullOrEmpty(payload.TypeName));

            ApiPayloadConverter.RestoreFrom(payload, PayloadFormat.Encoded);

            Assert.Equal(PayloadFormat.Plain, payload.Format);
            Assert.Equal("哈囉,世界", payload.Value);
        }

        [Fact]
        [DisplayName("RestoreFrom Encrypted after TransformTo Encrypted restores the original string")]
        public void TransformTo_Encrypted_RoundTrip_RestoresOriginalValue()
        {
            var key = MakeKey();
            var payload = new JsonRpcParams { Value = "secret-data" };

            ApiPayloadConverter.TransformTo(payload, PayloadFormat.Encrypted, key);

            Assert.Equal(PayloadFormat.Encrypted, payload.Format);
            Assert.IsType<byte[]>(payload.Value);

            ApiPayloadConverter.RestoreFrom(payload, PayloadFormat.Encrypted, key);

            Assert.Equal(PayloadFormat.Plain, payload.Format);
            Assert.Equal("secret-data", payload.Value);
        }

        [Fact]
        [DisplayName("TransformTo Plain does not throw even when the key is null")]
        public void TransformTo_Plain_NullKey_DoesNotThrow()
        {
            var payload = new JsonRpcParams { Value = "x" };

            var ex = Record.Exception(() =>
                ApiPayloadConverter.TransformTo(payload, PayloadFormat.Plain, null));

            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("RestoreFrom throws InvalidCastException when Value is not a byte[]")]
        public void RestoreFrom_NonByteArrayValue_ThrowsInvalidCastException()
        {
            var payload = new JsonRpcParams
            {
                Value = "not-bytes",
                TypeName = "System.String"
            };

            Assert.Throws<InvalidCastException>(() =>
                ApiPayloadConverter.RestoreFrom(payload, PayloadFormat.Encoded));
        }

        [Fact]
        [DisplayName("RestoreFrom throws InvalidOperationException when TypeName cannot be resolved to a Type")]
        public void RestoreFrom_UnresolvableTypeName_Throws()
        {
            // Passes the allow list (`Polhem.Api.Core.*`) but names no real type.
            var payload = new JsonRpcParams
            {
                Value = new byte[] { 0x01 },
                TypeName = "Polhem.Api.Core.DoesNotExistClass, Polhem.Api.Core"
            };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                ApiPayloadConverter.RestoreFrom(payload, PayloadFormat.Encoded));

            Assert.Contains("Unable to load type", ex.Message);
        }

        [Fact]
        [DisplayName("RestoreFrom Encrypted throws InvalidOperationException when the key is null")]
        public void RestoreFrom_Encrypted_NullKey_Throws()
        {
            var payload = new JsonRpcParams
            {
                Value = new byte[] { 0x01, 0x02 },
                TypeName = "System.String"
            };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                ApiPayloadConverter.RestoreFrom(payload, PayloadFormat.Encrypted, null));

            Assert.Contains("encryption key", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [DisplayName("RestoreFrom Encrypted throws InvalidOperationException when the key is an empty array")]
        public void RestoreFrom_Encrypted_EmptyKey_Throws()
        {
            var payload = new JsonRpcParams
            {
                Value = new byte[] { 0x01, 0x02 },
                TypeName = "System.String"
            };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                ApiPayloadConverter.RestoreFrom(payload, PayloadFormat.Encrypted, Array.Empty<byte>()));

            Assert.Contains("encryption key", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("Polhem.Api.Core.Messages.System.LoginRequest, Polhem.Api.Core")]
        [InlineData("Polhem.Definition.Collections.ParameterCollection, Polhem.Definition")]
        [InlineData("Polhem.Core.SomeClass, Polhem.Core")]
        [InlineData("Polhem.Api.Contracts.SomeDto, Polhem.Api.Contracts")]
        [InlineData("System.Int32")]
        [DisplayName("RestoreFrom allows a TypeName inside the allow list")]
        public void RestoreFrom_AllowedTypeName_DoesNotThrowValidationError(string typeName)
        {
            var payload = new JsonRpcParams
            {
                Value = new byte[] { 0x01 },
                TypeName = typeName
            };

            // The call may throw due to deserialization failure (invalid bytes),
            // but it should NOT throw the whitelist validation error.
            var ex = Record.Exception(() =>
                ApiPayloadConverter.RestoreFrom(payload, PayloadFormat.Encoded));

            if (ex != null)
            {
                Assert.DoesNotContain("not in the allowed type whitelist", ex.Message);
            }
        }

        [Theory]
        [InlineData("System.Diagnostics.Process, System.Diagnostics.Process")]
        [InlineData("System.IO.FileInfo, System.IO.FileSystem")]
        [InlineData("Evil.Namespace.Exploit, Evil.Assembly")]
        [InlineData("System.Runtime.Serialization.Formatters.Binary.BinaryFormatter, mscorlib")]
        // A disallowed type smuggled in as a generic argument of an allowed outer type. The
        // argument's own comma comes before the assembly separator, so splitting on the first
        // comma yields a fragment that still starts with `Polhem.Core.` and the argument goes
        // unscreened.
        [InlineData("Polhem.Core.Collections.Dictionary`1[[System.Diagnostics.Process, System.Diagnostics.Process]], Polhem.Core")]
        [InlineData("Polhem.Definition.Something`1[[Evil.Namespace.Exploit, Evil.Assembly]], Polhem.Definition")]
        // Nested one level deeper: the outer two types are allowed, the innermost is not.
        [InlineData("Polhem.Core.A`1[[Polhem.Core.B`1[[Evil.Namespace.Exploit, Evil.Assembly]], Polhem.Core]], Polhem.Core")]
        // An array of a disallowed element type.
        [InlineData("Evil.Namespace.Exploit[], Evil.Assembly")]
        // Malformed names must fail closed rather than fall through to `Type.GetType`.
        [InlineData("Polhem.Core.Broken`1[[Evil.Namespace.Exploit, Evil.Assembly], Polhem.Core")]
        [DisplayName("RestoreFrom rejects a TypeName outside the allow list")]
        public void RestoreFrom_DisallowedTypeName_ThrowsInvalidOperationException(string typeName)
        {
            var payload = new JsonRpcParams
            {
                Value = new byte[] { 0x01 },
                TypeName = typeName
            };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                ApiPayloadConverter.RestoreFrom(payload, PayloadFormat.Encoded));

            Assert.Contains("not in the allowed type whitelist", ex.Message);
        }

        [Fact]
        [DisplayName("RestoreFrom with the Plain format skips TypeName validation")]
        public void RestoreFrom_PlainFormat_SkipsValidation()
        {
            var payload = new JsonRpcParams
            {
                Value = "some value",
                TypeName = "Evil.Namespace.Exploit, Evil.Assembly"
            };

            // Plain format should skip all validation and return immediately
            ApiPayloadConverter.RestoreFrom(payload, PayloadFormat.Plain);
            Assert.Equal(PayloadFormat.Plain, payload.Format);
        }

        [Fact]
        [DisplayName("RestoreFrom throws InvalidOperationException when TypeName is missing")]
        public void RestoreFrom_MissingTypeName_ThrowsInvalidOperationException()
        {
            var payload = new JsonRpcParams
            {
                Value = new byte[] { 0x01 },
                TypeName = null!
            };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                ApiPayloadConverter.RestoreFrom(payload, PayloadFormat.Encoded));

            Assert.Contains("TypeName is missing", ex.Message);
        }
    }
}
