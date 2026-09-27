using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Polhem.Api.Core.Conversion;
using Polhem.Api.Core.Messages.AuditLog;
using Polhem.Api.Core.Messages.System;
using Polhem.Api.Core.Transformers;
using Polhem.Base.Serialization;
using Polhem.Definition.Paging;
using Polhem.Definition.Sorting;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Keeps a wire message meaning the same thing on every codec although the JSON wires leave out default values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The JSON body codec and <c>Plain</c> omit a member equal to its CLR default; MessagePack writes every member.
    /// When a member's initialiser is not that default, an explicit default (<c>ExpiresIn = 0</c>) is left out by the
    /// JSON writer and the reader's initialiser fills the gap, so the server saw 3600 over JSON and 0 over MessagePack:
    /// invalid input rejected on one codec and silently replaced on the other.
    /// </para>
    /// <para>
    /// The rule these tests enforce keeps the wire as small as it was: a member whose initialiser differs from its
    /// CLR default carries <c>[JsonIgnore(Condition = JsonIgnoreCondition.Never)]</c> and is always written, and every
    /// other member may be omitted, because absent then means exactly the CLR default on every reader.
    /// </para>
    /// </remarks>
    public class WireDefaultOmissionTests
    {
        [Fact]
        [DisplayName("The JSON body codec and Plain omit a member equal to its CLR default, the premise of the initialiser rule")]
        public void JsonWires_OmitDefaultValuedMembers()
        {
            var sortField = new SortField { FieldName = "a", Direction = SortDirection.Asc };

            var codecJson = Encoding.UTF8.GetString(new JsonPayloadSerializer().Serialize(sortField, typeof(SortField)));
            var plainJson = JsonCodec.Serialize(sortField);

            // If either wire starts writing defaults, the rule below is no longer needed there; revisit it rather than
            // delete this premise.
            Assert.DoesNotContain("direction", codecJson, StringComparison.Ordinal);
            Assert.DoesNotContain("direction", plainJson, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("No wire member initialises to a value other than its CLR default unless the JSON wires always write it")]
        public void WireMembers_WithNonDefaultInitialisers_AreAlwaysWritten()
        {
            var nullability = new NullabilityInfoContext();
            var violations = new List<string>();
            var inspected = 0;

            foreach (var type in WireClosure.Types().Where(IsInspectable).OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                var instance = Activator.CreateInstance(type)!;
                inspected++;

                foreach (var property in WireClosure.Members(type))
                {
                    if (WireClosure.IsAlwaysWritten(property)) continue;

                    var value = property.GetValue(instance);
                    if (!IsAmbiguousWhenOmitted(property, value, nullability)) continue;

                    violations.Add($"{type.FullName}.{property.Name} = {value}");
                }
            }

            // Anti-vacuous: the filter above must still admit the message types.
            Assert.True(inspected > 80, $"Only {inspected} wire types were inspected; the closure or the filter is broken.");
            Assert.True(
                violations.Count == 0,
                "These wire members initialise to a value other than their CLR default. The JSON wires omit a default " +
                "value, and the reader's initialiser then replaces it, so the member means different things on JSON and " +
                "MessagePack. Mark each with [JsonIgnore(Condition = JsonIgnoreCondition.Never)], or drop the initialiser:" +
                $"{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
        }

        [Fact]
        [DisplayName("CreateSessionRequest.ExpiresIn = 0 arrives as 0 over the JSON body codec, Plain and MessagePack alike")]
        public void CreateSessionRequest_ZeroExpiresIn_SurvivesEveryCodec()
        {
            var request = new CreateSessionRequest { ExpiresIn = 0 };

            Assert.Equal(0, ViaJsonCodec(request).ExpiresIn);
            Assert.Equal(0, ViaPlain(request).ExpiresIn);
            Assert.Equal(0, ViaMessagePack(request).ExpiresIn);
        }

        [Fact]
        [DisplayName("PagingOptions with Page and PageSize of 0 arrive as 0 over the JSON body codec, Plain and MessagePack alike")]
        public void PagingOptions_ZeroValues_SurviveEveryCodec()
        {
            var paging = new PagingOptions { Page = 0, PageSize = 0 };

            foreach (var restored in new[] { ViaJsonCodec(paging), ViaPlain(paging), ViaMessagePack(paging) })
            {
                Assert.Equal(0, restored.Page);
                Assert.Equal(0, restored.PageSize);
            }
        }

        [Fact]
        [DisplayName("GetTopApiMethodsRequest.TopN = 0 arrives as 0 over the JSON body codec, Plain and MessagePack alike")]
        public void GetTopApiMethodsRequest_ZeroTopN_SurvivesEveryCodec()
        {
            var request = new GetTopApiMethodsRequest { TopN = 0 };

            Assert.Equal(0, ViaJsonCodec(request).TopN);
            Assert.Equal(0, ViaPlain(request).TopN);
            Assert.Equal(0, ViaMessagePack(request).TopN);
        }

        private static bool IsInspectable(Type type) =>
            type.IsClass
            && !type.IsAbstract
            && type.Namespace?.StartsWith("Polhem.", StringComparison.Ordinal) == true
            && type.GetConstructor(Type.EmptyTypes) != null;

        /// <summary>
        /// Whether leaving this member out would let the reader substitute a value the writer did not mean.
        /// </summary>
        private static bool IsAmbiguousWhenOmitted(PropertyInfo property, object? initialValue, NullabilityInfoContext nullability)
        {
            var type = property.PropertyType;
            if (type.IsValueType)
            {
                var clrDefault = Nullable.GetUnderlyingType(type) != null ? null : Activator.CreateInstance(type);
                return !Equals(initialValue, clrDefault);
            }

            // A reference member is omitted only when null. That is ambiguous only if null is a legal value: a
            // non-nullable member initialised to an empty string or collection never legitimately carries null.
            // An empty collection is not ambiguous either, since null and empty both mean "no items" (the lazily
            // created `ApiMessageBase.Parameters` is the case in point).
            return initialValue != null
                && initialValue is not ICollection { Count: 0 }
                && nullability.Create(property).WriteState == NullabilityState.Nullable;
        }

        private static T ViaJsonCodec<T>(T value) where T : class
        {
            var codec = new JsonPayloadSerializer();
            return (T)codec.Deserialize(codec.Serialize(value, typeof(T)), typeof(T))!;
        }

        private static T ViaPlain<T>(T value) where T : class
        {
            // The client writes a Plain body with `JsonCodec`, and the server binds it with `ApiInputConverter`.
            using var document = JsonDocument.Parse(JsonCodec.Serialize(value));
            return (T)ApiInputConverter.Convert(document.RootElement.Clone(), typeof(T))!;
        }

        private static T ViaMessagePack<T>(T value) where T : class
        {
            var codec = new MessagePackPayloadSerializer();
            return (T)codec.Deserialize(codec.Serialize(value, typeof(T)), typeof(T))!;
        }
    }
}
