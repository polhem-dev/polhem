using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Reflection;
using System.Text;
using Polhem.Api.Core.MessagePack;
using Polhem.Api.Core.Messages;
using Polhem.Base.Serialization;

namespace Polhem.Api.Core.UnitTests.Contracts
{
    /// <summary>
    /// Verifies that **every** API contract type (subtypes of <see cref="ApiRequest"/> / <see cref="ApiResponse"/>)
    /// serializes in **both the MessagePack and JSON** wire formats and round-trips faithfully. The assembly is
    /// enumerated by reflection, so current and future contracts are covered automatically.
    /// </summary>
    /// <remarks>
    /// What <see cref="Contract_SerializesAndRoundTrips"/> checks: every settable scalar property, including those of
    /// nested classes up to two levels deep, is given a non-default value and must come back equal after the round
    /// trip, and re-serializing the restored object must give the same bytes. Collections, dictionaries,
    /// <c>DataSet</c> and <c>DataTable</c> properties keep their defaults here, so a change that only breaks those is
    /// caught by the dedicated round-trip tests of the types that carry them, not by this one.
    /// <para>
    /// The two serializer strategies follow polhem's real wire paths: MessagePack goes through
    /// <see cref="MessagePackCodec"/> (SafeMessagePackSerializerOptions, custom formatters and the resolver chain), and
    /// JSON goes through <see cref="JsonCodec"/> (DataSet/DataTable converters, camelCase, enum-as-string and the
    /// <c>{Property}Specified</c> convention).
    /// </para>
    /// </remarks>
    public class ApiContractSerializationTests
    {
        private static readonly Guid s_sampleGuid = new("11111111-2222-3333-4444-555555555555");
        private static readonly DateTime s_sampleUtc = new(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        private static readonly DateTimeOffset s_sampleOffset = new(2026, 7, 22, 10, 0, 0, TimeSpan.Zero);

        // `JsonCodec` only has the generic `Deserialize<T>(string, bool)`, so the reflection scan calls it through `MakeGenericMethod`.
        private static readonly MethodInfo s_jsonDeserializeGeneric = typeof(JsonCodec)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(JsonCodec.Deserialize)
                && m.IsGenericMethodDefinition
                && m.GetParameters() is [{ ParameterType.FullName: "System.String" }, ..]);

        // The two wire serialization strategies. A contract change must round-trip faithfully in both, compared as byte arrays.
        private static readonly IReadOnlyDictionary<string, SerializerStrategy> s_strategies =
            new Dictionary<string, SerializerStrategy>(StringComparer.Ordinal)
            {
                ["msgpack"] = new SerializerStrategy(
                    (obj, type) => MessagePackCodec.Serialize(obj, type),
                    (bytes, type) => MessagePackCodec.Deserialize(bytes, type)),
                ["json"] = new SerializerStrategy(
                    (obj, _) => Encoding.UTF8.GetBytes(JsonCodec.Serialize(obj)),
                    (bytes, type) => s_jsonDeserializeGeneric
                        .MakeGenericMethod(type)
                        .Invoke(null, [Encoding.UTF8.GetString(bytes)])),
            };

        public static TheoryData<string, Type> Cases()
        {
            var data = new TheoryData<string, Type>();
            var contracts = typeof(ApiRequest).Assembly.GetTypes()
                .Where(t => t is { IsAbstract: false, IsClass: true }
                    && (typeof(ApiRequest).IsAssignableFrom(t) || typeof(ApiResponse).IsAssignableFrom(t)))
                .OrderBy(t => t.FullName, StringComparer.Ordinal);

            foreach (var type in contracts)
            {
                foreach (var strategy in s_strategies.Keys)
                {
                    data.Add(strategy, type);
                }
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(Cases))]
        [DisplayName("API contract types round-trip faithfully through MessagePack and JSON")]
        public void Contract_SerializesAndRoundTrips(string serializerName, Type type)
        {
            var strategy = s_strategies[serializerName];
            var instance = Activator.CreateInstance(type)!;
            Populate(instance, depth: 0);

            var bytes = strategy.Serialize(instance, type);
            var restored = strategy.Deserialize(bytes, type);

            Assert.NotNull(restored);
            Assert.IsType(type, restored);

            // A property dropped on write would be missing from both byte arrays below, so the populated values are
            // compared one by one first.
            AssertPopulatedValuesEqual(instance, restored!, type.Name, depth: 0);

            // Fidelity: serialization is deterministic, so serializing the restored object again yields the same bytes when no value was lost.
            Assert.Equal(bytes, strategy.Serialize(restored!, type));
        }

        /// <summary>
        /// Compares every property <see cref="Populate"/> set, recursing into nested classes the same way it does.
        /// </summary>
        private static void AssertPopulatedValuesEqual(object expected, object actual, string path, int depth)
        {
            foreach (var property in expected.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetMethod is not { IsPublic: true } || property.SetMethod is not { IsPublic: true }
                    || SampleValue(property.PropertyType, depth) is null)
                {
                    continue;
                }

                var expectedValue = property.GetValue(expected);
                var actualValue = property.GetValue(actual);
                string propertyPath = path + "." + property.Name;
                var target = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

                if (target.IsClass && target != typeof(string) && target != typeof(byte[]))
                {
                    Assert.True(actualValue is not null, $"{propertyPath} was lost in the round trip.");
                    AssertPopulatedValuesEqual(expectedValue!, actualValue!, propertyPath, depth + 1);
                }
                else
                {
                    Assert.True(Equals(expectedValue, actualValue) || StructurallyEqualBytes(expectedValue, actualValue),
                        $"{propertyPath}: expected '{expectedValue}', got '{actualValue}'.");
                }
            }
        }

        private static bool StructurallyEqualBytes(object? expected, object? actual)
            => expected is byte[] a && actual is byte[] b && a.AsSpan().SequenceEqual(b);

        /// <summary>
        /// Fills writable (public setter) scalar properties with non-default sample values so the fidelity check means
        /// something. Nested classes are populated recursively with scalars; collections, dictionaries and DataSets keep their defaults.
        /// </summary>
        private static void Populate(object instance, int depth)
        {
            foreach (var property in instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // Only properties with a public getter and a public setter, which excludes read-only framework members such as `Collection`.
                if (property.GetMethod is not { IsPublic: true } || property.SetMethod is not { IsPublic: true })
                {
                    continue;
                }

                var value = SampleValue(property.PropertyType, depth);
                if (value is not null)
                {
                    property.SetValue(instance, value);
                }
            }
        }

        private static object? SampleValue(Type type, int depth)
        {
            var target = Nullable.GetUnderlyingType(type) ?? type;

            if (target == typeof(string)) { return "sample"; }
            if (target == typeof(Guid)) { return s_sampleGuid; }
            if (target == typeof(bool)) { return true; }
            if (target == typeof(int)) { return 7; }
            if (target == typeof(long)) { return 7L; }
            if (target == typeof(short)) { return (short)7; }
            if (target == typeof(decimal)) { return 7.5m; }
            if (target == typeof(double)) { return 7.5d; }
            if (target == typeof(DateTime)) { return s_sampleUtc; }
            if (target == typeof(DateTimeOffset)) { return s_sampleOffset; }
            if (target == typeof(byte[])) { return new byte[] { 1, 2, 3, 4 }; }

            if (target.IsEnum)
            {
                // Take the second declared member when there is one, otherwise the only one, so the fidelity check means something for enums.
                var values = Enum.GetValues(target);
                return values.Length > 1 ? values.GetValue(1) : values.GetValue(0);
            }

            // DataSet and DataTable: populating them blindly would set properties such as `EnforceConstraints` at
            // random, which is fragile and unreliable. Their deep round-trip is covered with real data by the existing
            // Form/*MessagePackTests, AuditLog/* and *JsonRpcRoundTripTests. They stay null here, and this breadth test
            // still covers the contract's other scalar properties.
            if (target == typeof(DataSet) || target == typeof(DataTable)) { return null; }

            // Collections and dictionaries keep their default (empty) to avoid the complexity of generic filling. Empty collections still round-trip.
            if (target != typeof(string) && typeof(IEnumerable).IsAssignableFrom(target)) { return null; }

            // Nested complex types are populated recursively with scalars, which checks that object graphs serialize too.
            if (target is { IsClass: true } && target != typeof(object)
                && depth < 2 && target.GetConstructor(Type.EmptyTypes) is not null)
            {
                var nested = Activator.CreateInstance(target)!;
                Populate(nested, depth + 1);
                return nested;
            }

            return null;
        }

        private sealed class SerializerStrategy
        {
            public SerializerStrategy(Func<object, Type, byte[]> serialize, Func<byte[], Type, object?> deserialize)
            {
                Serialize = serialize;
                Deserialize = deserialize;
            }

            public Func<object, Type, byte[]> Serialize { get; }

            public Func<byte[], Type, object?> Deserialize { get; }
        }
    }
}
