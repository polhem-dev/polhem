using Polhem.Api.Core.MessagePack;
using Polhem.Definition.Collections;
using System.Reflection;
using Polhem.Base.Serialization;
using MessagePack;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Helper methods for tests.
    /// </summary>
    internal static class TestFunc
    {
        /// <summary>
        /// Tests MessagePack serialization and deserialization, comparing each restored property value.
        /// </summary>
        /// <remarks>
        /// The comparison covers "every public readable property not marked <see cref="IgnoreMemberAttribute"/>".
        /// It deliberately does not use the presence of <c>[Key]</c> as the gate. After the name-based key migration of
        /// adr-030, most types use <c>[MessagePackObject(keyAsPropertyName: true)]</c> and their properties no longer
        /// carry <c>[Key]</c>. With that gate the comparison loop would never run, and the whole helper would degrade
        /// into a false green light with nothing but <c>Assert.NotNull</c>.
        /// </remarks>
        public static void TestMessagePackSerialization<T>(T obj)
        {
            var serialized = MessagePackCodec.Serialize(obj);
            var deserialized = MessagePackCodec.Deserialize<T>(serialized);

            Assert.NotNull(deserialized);

            var comparedCount = 0;
            foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead) { continue; }

                // An indexer cannot be read without index arguments.
                if (property.GetIndexParameters().Length > 0) { continue; }

                // Members marked as not serialized are never restored, so comparing them would only produce false failures.
                if (property.IsDefined(typeof(IgnoreMemberAttribute), inherit: true)) { continue; }

                var originalValue = property.GetValue(obj);
                var deserializedValue = property.GetValue(deserialized);
                comparedCount++;

                if (originalValue == null && deserializedValue == null)
                {
                    continue;
                }

                if (IsSimpleType(property.PropertyType))
                {
                    Assert.Equal(originalValue, deserializedValue);
                }
                else if (originalValue is IEnumerable<Parameter> origList && deserializedValue is IEnumerable<Parameter> deserList)
                {
                    // A `ParameterCollection` is compared by its contents.
                    Assert.Equal(origList.Count(), deserList.Count());

                    foreach (var origItem in origList)
                    {
                        var match = deserList.FirstOrDefault(x => x.Key == origItem.Key);
                        Assert.NotNull(match);
                        Assert.Equal(origItem.Value, match.Value);
                    }
                }
                else
                {
                    // Other complex types have no deep recursive comparison, so they are compared through their XML serialization.
                    Assert.NotNull(originalValue);
                    Assert.NotNull(deserializedValue);
                    var xml1 = XmlCodec.Serialize(originalValue);
                    var xml2 = XmlCodec.Serialize(deserializedValue);
                    Assert.Equal(xml1, xml2);
                }
            }

            // Keeps this helper from silently degrading again. Comparing no property at all means the gate condition is wrong.
            Assert.True(comparedCount > 0,
                $"No property of type {typeof(T).Name} was compared; the helper may no longer work.");
        }

        /// <summary>
        /// Determines whether the type is a simple type.
        /// </summary>
        private static bool IsSimpleType(Type type)
        {
            var actual = Nullable.GetUnderlyingType(type) ?? type;

            return actual.IsPrimitive
                || actual.IsEnum
                || actual == typeof(string)
                || actual == typeof(DateTime)
                || actual == typeof(DateTimeOffset)
                || actual == typeof(DateOnly)
                || actual == typeof(TimeOnly)
                || actual == typeof(TimeSpan)
                || actual == typeof(decimal)
                || actual == typeof(Guid);
        }
    }
}
