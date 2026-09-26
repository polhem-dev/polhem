using System.Buffers;
using System.ComponentModel;
using Polhem.Api.Core.MessagePack;
using Polhem.Definition.Collections;
using Polhem.Definition.Filters;
using Polhem.Tests.Shared;
using MessagePack;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Allow-list and round-trip tests for <c>WireValueFormatter</c>.
    /// </summary>
    /// <remarks>
    /// Formerly <c>SafeTypelessFormatterTests</c>. When the formatter changed from wrapping <c>TypelessFormatter</c>
    /// to a closed discriminated set, the allow-list semantics stayed the same; what changed is the framing of the
    /// unknown-type path.
    /// </remarks>
    public class WireValueFormatterTests
    {
        [Fact(DisplayName = "ParameterCollection round-trips safe primitive types")]
        public void ParameterCollection_AllowedPrimitiveTypes_RoundTrip()
        {
            var original = new ParameterCollection
            {
                { "IntValue", 42 },
                { "StringValue", "Hello" },
                { "BoolValue", true },
                { "DecimalValue", 99.99m },
                { "DateTimeValue", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                { "NullValue", null! }
            };

            var bytes = MessagePackCodec.Serialize(original);
            var restored = MessagePackCodec.Deserialize<ParameterCollection>(bytes);

            Assert.NotNull(restored);
            Assert.Equal(original.Count, restored.Count);
            Assert.Equal(42, restored["IntValue"].Value);
            Assert.Equal("Hello", restored["StringValue"].Value);
            Assert.True((bool)restored["BoolValue"].Value!);
            Assert.Equal(99.99m, restored["DecimalValue"].Value);
            Assert.Null(restored["NullValue"].Value);
        }

        [Theory]
        [InlineData((sbyte)-8)]
        [InlineData((byte)8)]
        [InlineData((short)-16)]
        [InlineData((ushort)16)]
        [InlineData(-32)]
        [InlineData(32u)]
        [InlineData(-64L)]
        [InlineData(64UL)]
        [InlineData(1.5f)]
        [InlineData(2.5d)]
        [DisplayName("Integers and floating-point values of every width round-trip to their original type")]
        public void ParameterValue_NumericWidths_RoundTrip(object value)
        {
            var restored = RoundTripValue(value);

            Assert.Equal(value.GetType(), restored!.GetType());
            Assert.Equal(value, restored);
        }

        [Fact]
        [DisplayName("Known non-numeric types round-trip to their original type")]
        public void ParameterValue_KnownReferenceAndStructTypes_RoundTrip()
        {
            var guid = Guid.NewGuid();
            var bytes = new byte[] { 1, 2, 3 };

            Assert.Equal(guid, RoundTripValue(guid));
            Assert.Equal(new DateOnly(2026, 7, 25), RoundTripValue(new DateOnly(2026, 7, 25)));
            Assert.Equal(TimeSpan.FromMinutes(90), RoundTripValue(TimeSpan.FromMinutes(90)));
            Assert.Equal(bytes, (byte[])RoundTripValue(bytes)!);
            Assert.Equal(DBNull.Value, RoundTripValue(DBNull.Value));
        }

        [Fact]
        [DisplayName("An object[] condition value (IN clause) round-trips recursively")]
        public void ParameterValue_ObjectArray_RoundTrip()
        {
            var guid = Guid.NewGuid();
            var restored = (object?[])RoundTripValue(new object[] { 1, "two", guid })!;

            Assert.Equal(3, restored.Length);
            Assert.Equal(1, restored[0]);
            Assert.Equal("two", restored[1]);
            Assert.Equal(guid, restored[2]);
        }

        [DynamicCodeFact(DisplayName = "ParameterCollection round-trips a type in a Polhem namespace (named-type path, needs dynamic code)")]
        public void ParameterCollection_AllowedPolhemTypes_RoundTrip()
        {
            // This takes the named-type branch of `WireValueFormatter`. The type is outside the closed discriminated set
            // and can only recurse through the non-generic overload, so it is unavailable on a runtime without dynamic
            // code (iOS). That is an inherent limit of the configurable `SysInfo.AllowedTypeNamespaces` extension point,
            // not a defect.
            var inner = new ParameterCollection
            {
                { "Nested", "value" }
            };

            var original = new ParameterCollection
            {
                { "Child", inner }
            };

            var bytes = MessagePackCodec.Serialize(original);
            var restored = MessagePackCodec.Deserialize<ParameterCollection>(bytes);

            Assert.NotNull(restored);
            var restoredChild = restored["Child"].Value as ParameterCollection;
            Assert.NotNull(restoredChild);
            Assert.Equal("value", restoredChild["Nested"].Value);
        }

        [Fact(DisplayName = "ParameterCollection round-trips DateOnly (a calendar-date filter value)")]
        public void ParameterCollection_DateOnly_RoundTrip()
        {
            var original = new ParameterCollection
            {
                { "DateOnlyValue", new DateOnly(2026, 7, 25) }
            };

            var bytes = MessagePackCodec.Serialize(original);
            var restored = MessagePackCodec.Deserialize<ParameterCollection>(bytes);

            Assert.NotNull(restored);
            Assert.Equal(new DateOnly(2026, 7, 25), restored["DateOnlyValue"].Value);
        }

        [Fact(DisplayName = "A DateOnly condition value of FilterCondition round-trips")]
        public void FilterCondition_DateOnlyValue_RoundTrip()
        {
            var original = FilterCondition.Equal("hire_date", new DateOnly(2026, 7, 25));

            var bytes = MessagePackCodec.Serialize(original);
            var restored = MessagePackCodec.Deserialize<FilterCondition>(bytes);

            Assert.NotNull(restored);
            Assert.Equal(new DateOnly(2026, 7, 25), restored.Value);
        }

        [Theory]
        [InlineData("System.Int32")]
        [InlineData("System.String")]
        [InlineData("System.Boolean")]
        [InlineData("System.Decimal")]
        [InlineData("System.DateTime")]
        [InlineData("System.DateOnly")]
        [InlineData("System.Guid")]
        [InlineData("System.Byte[]")]
        [InlineData("System.DBNull")]
        [InlineData("System.Data.DataTable")]
        [InlineData("Polhem.Base.SomeClass")]
        [InlineData("Polhem.Definition.Collections.Parameter")]
        [InlineData("Polhem.Api.Contracts.SomeDto")]
        [InlineData("Polhem.Api.Core.Something")]
        [InlineData("Polhem.Business.Employee")]
        [DisplayName("IsTypeAllowed allows primitive types and allow-listed namespaces")]
        public void IsTypeAllowed_AllowedTypes_ReturnsTrue(string fullName)
        {
            Assert.True(WireTypeWhitelist.IsTypeAllowed(fullName));
        }

        [Theory]
        [InlineData("System.Diagnostics.Process")]
        [InlineData("System.IO.File")]
        [InlineData("System.IO.FileInfo")]
        [InlineData("System.Runtime.Serialization.Formatters.Binary.BinaryFormatter")]
        [InlineData("Evil.Namespace.Exploit")]
        [InlineData("SomeMalicious.Attacker.Type")]
        [InlineData("System.Data.DataRow")]
        [DisplayName("IsTypeAllowed rejects types outside the allow list")]
        public void IsTypeAllowed_DisallowedTypes_ReturnsFalse(string fullName)
        {
            Assert.False(WireTypeWhitelist.IsTypeAllowed(fullName));
        }

        [Fact]
        [DisplayName("WireValueFormatter.Instance provides a singleton")]
        public void Instance_IsNotNull()
        {
            Assert.NotNull(WireValueFormatter.Instance);
        }

        [Fact]
        [DisplayName("Deserialize returns null for a nil payload")]
        public void Deserialize_NilPayload_ReturnsNull()
        {
            var bytes = MessagePackCodec.Serialize<object?>(null);

            Assert.Null(DeserializeViaFormatter(bytes));
        }

        [Fact]
        [DisplayName("Deserialize blocks a type outside the allow list before resolving the type")]
        public void Deserialize_DisallowedType_ThrowsInvalidOperation()
        {
            // Builds a named-type envelope by hand, naming a type outside the allow list.
            // The check runs before `Type.GetType`, so the type is never loaded.
            var bytes = BuildNamedEnvelope(typeof(global::System.Diagnostics.Process).AssemblyQualifiedName!);

            var exception = Assert.Throws<InvalidOperationException>(
                () => DeserializeViaFormatter(bytes));

            Assert.Contains("blocked", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Deserialize throws MessagePackSerializationException for an unknown discriminator")]
        public void Deserialize_UnknownCode_Throws()
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            writer.WriteArrayHeader(2);
            writer.Write(9999);
            writer.WriteNil();
            writer.Flush();

            Assert.Throws<MessagePackSerializationException>(
                () => DeserializeViaFormatter(buffer.WrittenSpan.ToArray()));
        }

        private static byte[] BuildNamedEnvelope(string assemblyQualifiedName)
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            writer.WriteArrayHeader(2);
            writer.Write(assemblyQualifiedName);
            writer.WriteNil();
            writer.Flush();
            return buffer.WrittenSpan.ToArray();
        }

        private static object? RoundTripValue(object value)
        {
            var source = new ParameterCollection { { "v", value } };
            var restored = MessagePackCodec.Deserialize<ParameterCollection>(MessagePackCodec.Serialize(source));
            return restored!["v"].Value;
        }

        /// <summary>
        /// A method rather than a lambda: a lambda cannot capture a `ref` local (CS8175).
        /// </summary>
        private static object? DeserializeViaFormatter(byte[] bytes)
        {
            var reader = new MessagePackReader(new ReadOnlySequence<byte>(bytes));
            return WireValueFormatter.Instance.Deserialize(ref reader, MessagePackSerializerOptions.Standard);
        }
    }
}
