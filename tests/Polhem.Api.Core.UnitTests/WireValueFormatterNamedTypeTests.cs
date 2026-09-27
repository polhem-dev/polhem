using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;
using Polhem.Api.Core.MessagePack;
using Polhem.Definition.Collections;
using Polhem.Definition.Logging;
using Polhem.Definition.Settings;
using Polhem.Tests.Shared;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Tests for the named-type branch of <c>WireValueFormatter</c>: values outside the closed discriminated set that
    /// travel as their assembly-qualified type name followed by their payload.
    /// </summary>
    /// <remarks>
    /// Enums and nested <see cref="ParameterCollection"/> values take this branch and must work without dynamic code,
    /// which is what .NET for iOS and Mac Catalyst run with. The mobile AOT gate runs these tests with
    /// <c>-p:DynamicCodeSupport=false</c>. The golden-byte tests pin the wire shape so the AOT-safe path writes what the
    /// earlier non-generic path wrote.
    /// </remarks>
    public class WireValueFormatterNamedTypeTests
    {
        [Fact]
        [DisplayName("A framework enum in a parameter value round-trips to the same enum type and value")]
        public void ParameterValue_RegisteredFrameworkEnum_RoundTrips()
        {
            var restored = RoundTripValue(PermissionAction.Create);

            Assert.IsType<PermissionAction>(restored);
            Assert.Equal(PermissionAction.Create, restored);
        }

        [Fact]
        [DisplayName("An enum with no registered formatter round-trips to the same enum type and value")]
        public void ParameterValue_UnregisteredFrameworkEnum_RoundTrips()
        {
            var restored = RoundTripValue(AuditRuleMode.Off);

            Assert.IsType<AuditRuleMode>(restored);
            Assert.Equal(AuditRuleMode.Off, restored);
        }

        [Theory]
        [InlineData(WideSignedMode.Lowest)]
        [InlineData(WideSignedMode.Negative)]
        [InlineData(WideSignedMode.Highest)]
        [DisplayName("An enum with a signed 64-bit underlying type round-trips at both ends of its range")]
        public void ParameterValue_SignedWideEnum_RoundTrips(WideSignedMode value)
        {
            Assert.Equal(value, RoundTripValue(value));
        }

        [Theory]
        [InlineData(WideUnsignedMode.Zero)]
        [InlineData(WideUnsignedMode.Highest)]
        [DisplayName("An enum with an unsigned 64-bit underlying type round-trips beyond the signed range")]
        public void ParameterValue_UnsignedWideEnum_RoundTrips(WideUnsignedMode value)
        {
            Assert.Equal(value, RoundTripValue(value));
        }

        [Fact]
        [DisplayName("A ParameterCollection nested in a parameter value round-trips")]
        public void ParameterCollection_NestedCollection_RoundTrips()
        {
            var original = new ParameterCollection
            {
                { "Child", new ParameterCollection { { "Nested", "value" }, { "Mode", PermissionAction.Update } } }
            };

            var restored = MessagePackCodec.Deserialize<ParameterCollection>(MessagePackCodec.Serialize(original));

            var child = Assert.IsType<ParameterCollection>(restored["Child"].Value);
            Assert.Equal("value", child["Nested"].Value);
            Assert.Equal(PermissionAction.Update, child["Mode"].Value);
        }

        [Fact]
        [DisplayName("An enum value is written as its assembly-qualified type name followed by its underlying integer")]
        public void Serialize_Enum_WritesTypeNameAndInteger()
        {
            var expected = Envelope(typeof(PermissionAction), (ref MessagePackWriter writer) => writer.Write((int)PermissionAction.Create));

            Assert.Equal(expected, SerializeValue(PermissionAction.Create));
        }

        [Fact]
        [DisplayName("A nested ParameterCollection is written as its assembly-qualified type name followed by the collection payload")]
        public void Serialize_NestedParameterCollection_WritesTypeNameAndCollection()
        {
            var inner = new ParameterCollection { { "Nested", "value" } };
            var payload = MessagePackSerializer.Serialize(inner, MessagePackCodec.SerializerOptions);
            var expected = Envelope(typeof(ParameterCollection), (ref MessagePackWriter writer) => writer.WriteRaw(payload));

            Assert.Equal(expected, SerializeValue(inner));
        }

        [DynamicCodeFact(DisplayName = "Enums of every underlying width serialize to the same bytes the non-generic MessagePack overload writes")]
        public void Serialize_Enums_MatchNonGenericPayload()
        {
            // The non-generic overload is what the named-type branch used for every value before enums had an AOT-safe
            // path, so matching it keeps the wire unchanged for desktop and server peers.
            object[] values =
            [
                PermissionAction.Create, AuditRuleMode.Off, NarrowMode.Top,
                WideSignedMode.Lowest, WideSignedMode.Negative, WideSignedMode.Highest, WideUnsignedMode.Highest,
            ];

            foreach (var value in values)
            {
                var type = value.GetType();
                var expected = Envelope(type, (ref MessagePackWriter writer)
                    => MessagePackSerializer.Serialize(type, ref writer, value, MessagePackCodec.SerializerOptions));

                Assert.Equal(expected, SerializeValue(value));
            }
        }

        [DynamicCodeFact(DisplayName = "An allow-listed type with no AOT-safe path still round-trips where dynamic code is available")]
        public void ParameterValue_CustomAllowListedType_RoundTripsWithDynamicCode()
        {
            var restored = RoundTripValue(new CustomPayload { Name = "custom" });

            Assert.Equal("custom", Assert.IsType<CustomPayload>(restored).Name);
        }

        [Fact]
        [DisplayName("Without dynamic code, a type that needs it fails with a message that names the type")]
        public void EnsureDynamicCode_Unavailable_ThrowsNamingTheType()
        {
            var exception = Assert.Throws<NotSupportedException>(
                () => WireValueFormatter.EnsureDynamicCode(typeof(CustomPayload), isDynamicCodeSupported: false));

            Assert.Contains(typeof(CustomPayload).FullName!, exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("With dynamic code available, the check lets the non-generic path run")]
        public void EnsureDynamicCode_Available_DoesNotThrow()
        {
            var exception = Record.Exception(
                () => WireValueFormatter.EnsureDynamicCode(typeof(CustomPayload), isDynamicCodeSupported: true));

            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("On a runtime without dynamic code, serializing a type that needs it names the type in the failure")]
        public void Serialize_CustomTypeWithoutDynamicCode_NamesTheType()
        {
            if (RuntimeFeature.IsDynamicCodeSupported)
            {
                // Only the mobile AOT gate can reach this branch. On a desktop run the value serializes normally,
                // which `ParameterValue_CustomAllowListedType_RoundTripsWithDynamicCode` covers.
                Assert.NotEmpty(SerializeValue(new CustomPayload()));
                return;
            }

            var exception = Assert.ThrowsAny<Exception>(() => SerializeValue(new CustomPayload()));

            Assert.Contains(typeof(CustomPayload).FullName!, FlattenMessages(exception), StringComparison.Ordinal);
        }

        private delegate void PayloadWriter(ref MessagePackWriter writer);

        private static byte[] Envelope(Type type, PayloadWriter payload)
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            writer.WriteArrayHeader(2);
            writer.Write(type.AssemblyQualifiedName);
            payload(ref writer);
            writer.Flush();
            return buffer.WrittenSpan.ToArray();
        }

        private static byte[] SerializeValue(object value)
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            WireValueFormatter.Instance.Serialize(ref writer, value, MessagePackCodec.SerializerOptions);
            writer.Flush();
            return buffer.WrittenSpan.ToArray();
        }

        private static object? RoundTripValue(object value)
        {
            var source = new ParameterCollection { { "v", value } };
            var restored = MessagePackCodec.Deserialize<ParameterCollection>(MessagePackCodec.Serialize(source));
            return restored!["v"].Value;
        }

        private static string FlattenMessages(Exception exception)
        {
            var messages = new List<string>();
            for (Exception? current = exception; current != null; current = current.InnerException)
                messages.Add(current.Message);
            return string.Join(" | ", messages);
        }

        /// <summary>An enum with a narrow underlying type.</summary>
        public enum NarrowMode : byte
        {
            /// <summary>The lowest value.</summary>
            Bottom = 0,

            /// <summary>A value that needs the full byte.</summary>
            Top = 200,
        }

        /// <summary>An enum with a signed 64-bit underlying type.</summary>
        public enum WideSignedMode : long
        {
            /// <summary>The lowest value.</summary>
            Lowest = long.MinValue,

            /// <summary>A small negative value.</summary>
            Negative = -5,

            /// <summary>The highest value.</summary>
            Highest = long.MaxValue,
        }

        /// <summary>An enum with an unsigned 64-bit underlying type.</summary>
        public enum WideUnsignedMode : ulong
        {
            /// <summary>Zero.</summary>
            Zero = 0,

            /// <summary>The highest value, beyond the signed range.</summary>
            Highest = ulong.MaxValue,
        }

        /// <summary>An allow-listed class with no registered formatter.</summary>
        public sealed class CustomPayload
        {
            /// <summary>Gets or sets the name.</summary>
            public string Name { get; set; } = string.Empty;
        }
    }
}
