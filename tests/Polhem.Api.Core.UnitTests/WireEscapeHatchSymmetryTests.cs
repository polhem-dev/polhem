using System.ComponentModel;
using System.Reflection;
using MessagePack;
using Polhem.Api.Core.MessagePack;
using Polhem.Api.Core.Messages;
using Polhem.Api.Core.Transformers;
using Polhem.Definition.Collections;
using Polhem.Definition.Filters;
using Polhem.Tests.Shared;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Both ends of the named-type escape hatch must apply the same rule, on both body codecs: a value the writer
    /// accepts is one the reader accepts.
    /// </summary>
    /// <remarks>
    /// The writer used to screen the runtime type, which walks array element types, while the reader screened the
    /// name, which bound the array rank to the element name and then found <c>System.Int32[]</c> in no list. An
    /// <c>int[]</c>, <c>string[]</c> or <c>Guid[]</c> parameter therefore serialized on the client and was refused on
    /// the server, so the failure surfaced in the other process. Single-dimensional arrays of allowed element types
    /// are now allowed at both ends; multi-dimensional arrays are refused at both, since System.Text.Json cannot carry
    /// them.
    /// </remarks>
    public class WireEscapeHatchSymmetryTests
    {
        private static readonly int[] s_ints = [1, 2];
        private static readonly string[] s_strings = ["a", "b"];
        private static readonly Guid[] s_guids = [Guid.Parse("0b3c8f55-2d71-4e3c-9f7c-4d1b0e6a9c21")];

        [Theory]
        [InlineData(typeof(int[]))]
        [InlineData(typeof(string[]))]
        [InlineData(typeof(Guid[]))]
        [InlineData(typeof(decimal[][]))]
        [InlineData(typeof(ComparisonOperator[]))]
        [DisplayName("A single-dimensional array of allowed element types passes the writer's screen and the reader's name screen")]
        public void SingleDimensionalArrayOfAllowedElements_IsAllowedAtBothEnds(Type type)
        {
            Assert.True(WireTypeWhitelist.IsNamedValueTypeAllowed(type));
            Assert.True(WireTypeWhitelist.IsAssemblyQualifiedNameAllowed(type.AssemblyQualifiedName));
            Assert.True(WireTypeWhitelist.IsRuntimeTypeAllowed(type));
        }

        [Theory]
        [InlineData(typeof(int[,]))]
        [InlineData(typeof(global::System.Text.StringBuilder[]))]
        [InlineData(typeof(List<object>))]
        [InlineData(typeof(TimeOnly))]
        [InlineData(typeof(char))]
        [InlineData(typeof(DateTimeKind))]
        [DisplayName("A type the reader refuses is refused by the writer too")]
        public void TypeTheReaderRefuses_IsRefusedByTheWriter(Type type)
        {
            var readerAccepts = WireTypeWhitelist.IsAssemblyQualifiedNameAllowed(type.AssemblyQualifiedName)
                && WireTypeWhitelist.IsRuntimeTypeAllowed(type);

            Assert.False(readerAccepts);
            Assert.False(WireTypeWhitelist.IsNamedValueTypeAllowed(type));
        }

        [Theory]
        [InlineData("System.Int32[,], System.Private.CoreLib")]
        [InlineData("System.Int32[ ], System.Private.CoreLib")]
        [InlineData("System.Int32[][][][][][][][][][], System.Private.CoreLib")]
        [DisplayName("The name screen refuses a multi-dimensional rank, a malformed rank and nesting past the depth limit")]
        public void IsAssemblyQualifiedNameAllowed_UnsupportedArrayShape_ReturnsFalse(string name)
        {
            Assert.False(WireTypeWhitelist.IsAssemblyQualifiedNameAllowed(name));
        }

        public static TheoryData<string, object> ValuesAcrossTheBoundary() => new()
        {
            { "int[]", s_ints },
            { "string[]", s_strings },
            { "Guid[]", s_guids },
            { "int[,]", new int[1, 1] },
            { "List<object>", new List<object> { 1 } },
            { "TimeOnly", new TimeOnly(9, 30) },
            { "char", 'c' },
            { "DateTimeKind", DateTimeKind.Utc },
        };

        [Theory]
        [MemberData(nameof(ValuesAcrossTheBoundary), DisableDiscoveryEnumeration = true)]
        [DisplayName("JSON body codec: a parameter value either fails on the writer or round-trips; it never passes the writer and fails the reader")]
        public void JsonCodec_ParameterValue_FailsOnTheWriterOrRoundTrips(string label, object value)
        {
            AssertWriterFailsOrRoundTrips(new JsonPayloadSerializer(), label, value);
        }

        [Theory]
        [MemberData(nameof(ValuesAcrossTheBoundary), DisableDiscoveryEnumeration = true)]
        [DisplayName("MessagePack: a parameter value either fails on the writer or round-trips; it never passes the writer and fails the reader")]
        public void MessagePack_ParameterValue_FailsOnTheWriterOrRoundTrips(string label, object value)
        {
            AssertWriterFailsOrRoundTrips(new MessagePackPayloadSerializer(), label, value);
        }

        [Fact]
        [DisplayName("JSON body codec: an int[] parameter value round-trips as an int[]")]
        public void JsonCodec_IntArrayParameter_RoundTrips()
        {
            var restored = RoundTrip(new JsonPayloadSerializer(), s_ints);

            Assert.Equal(s_ints, Assert.IsType<int[]>(restored));
        }

        [DynamicCodeFact]
        [DisplayName("MessagePack: an int[] parameter value round-trips as an int[] where dynamic code is available")]
        public void MessagePack_IntArrayParameter_RoundTrips()
        {
            // The named escape hatch serializes through the non-generic overload, which needs dynamic code; on the
            // mobile heads the writer refuses an array loudly instead (`WireValueFormatter.EnsureDynamicCode`).
            var restored = RoundTrip(new MessagePackPayloadSerializer(), s_ints);

            Assert.Equal(s_ints, Assert.IsType<int[]>(restored));
        }

        private static void AssertWriterFailsOrRoundTrips(IApiPayloadSerializer codec, string label, object value)
        {
            var request = CreateRequest(value);

            byte[] bytes;
            try
            {
                bytes = codec.Serialize(request, typeof(ExecFuncRequest));
            }
            catch (InvalidOperationException)
            {
                return;   // Refused on the writer: the failure stays in the process that made the mistake.
            }
            catch (NotSupportedException)
            {
                return;   // Refused on the writer where dynamic code is unavailable.
            }
            catch (MessagePackSerializationException)
            {
                return;   // MessagePack wraps the writer's refusal; it is still the writer refusing.
            }
            catch (TargetInvocationException ex) when (ex.InnerException is MessagePackSerializationException)
            {
                return;   // Without dynamic code the top-level non-generic call is invoked by reflection, which wraps it once more.
            }

            var restored = (ExecFuncRequest)codec.Deserialize(bytes, typeof(ExecFuncRequest))!;
            var restoredValue = restored.Parameters!["p"].Value;
            Assert.True(restoredValue != null && restoredValue.GetType() == value.GetType(),
                $"{codec.SerializationMethod}: {label} passed the writer but came back as {restoredValue?.GetType().Name ?? "null"}.");
        }

        private static object? RoundTrip(IApiPayloadSerializer codec, object value)
        {
            var bytes = codec.Serialize(CreateRequest(value), typeof(ExecFuncRequest));
            var restored = (ExecFuncRequest)codec.Deserialize(bytes, typeof(ExecFuncRequest))!;
            return restored.Parameters!["p"].Value;
        }

        private static ExecFuncRequest CreateRequest(object value)
        {
            var request = new ExecFuncRequest("f");
            request.Parameters!.Add(new Parameter("p", value));
            return request;
        }
    }
}
