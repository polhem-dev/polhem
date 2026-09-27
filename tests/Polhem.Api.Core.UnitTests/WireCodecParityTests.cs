using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using Polhem.Api.Core.MessagePack;
using Polhem.Api.Core.Transformers;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Round-trips every registered wire contract, with every wire member set to a value other than its default,
    /// through both body codecs, and compares the result member by member.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>WireContractDriftTests</c> compares each contract's declared member list with its type, and that is all it
    /// can see. A hand-written formatter reads its members in a <c>switch</c> whose <c>default</c> branch skips unknown
    /// keys, so a member added to the type and to the declared list but forgotten in <c>Deserialize</c> was written,
    /// then silently dropped on the way back in. The existing round-trip tests assert the members someone thought to
    /// list, so they did not notice either.
    /// </para>
    /// <para>
    /// Running the same instance through the JSON body codec as well is the only gate on the two codecs carrying the
    /// same member set: the fixtures pin encoding rules and a handful of messages, not every message.
    /// </para>
    /// </remarks>
    public class WireCodecParityTests
    {
        private const int MaxDepth = 3;

        private static readonly DateTime s_sampleDateTime = new DateTime(2026, 3, 14, 15, 9, 26, DateTimeKind.Utc);
        private static readonly Guid s_sampleGuid = new Guid("5f0c6a2e-3d1b-4c8e-9a47-2b6e1d0f8c35");

        public static TheoryData<string> Contracts()
        {
            var data = new TheoryData<string>();
            foreach (var contract in MessagePackCodec.RegisteredFormatters.OfType<IWireContract>()
                         .OrderBy(c => c.WireType.FullName, StringComparer.Ordinal))
            {
                data.Add(contract.WireType.AssemblyQualifiedName!);
            }
            return data;
        }

        [Fact]
        [DisplayName("The parity theory covers the registered wire contracts, so it cannot pass by looping over nothing")]
        public void Contracts_AreNotVacuous()
        {
            Assert.True(Contracts().Count > 80, $"Only {Contracts().Count} wire contracts were found.");
        }

        [Theory]
        [MemberData(nameof(Contracts))]
        [DisplayName("Every wire member survives a round-trip through MessagePack and the JSON body codec")]
        public void WireContract_RoundTripsEveryMember_OnBothCodecs(string typeName)
        {
            var type = Type.GetType(typeName, throwOnError: true)!;
            var original = Sample(type, depth: 0)!;

            // Anti-vacuous: a member left at its default would round-trip trivially, whatever the formatter does.
            var unset = WireClosure.Members(type)
                .Where(p => IsDefault(p.PropertyType, p.GetValue(original)))
                .Select(p => p.Name)
                .ToList();
            Assert.True(unset.Count == 0, $"The sample for {type.Name} leaves these members at their default: {string.Join(", ", unset)}");

            var codecs = new IApiPayloadSerializer[] { new MessagePackPayloadSerializer(), new JsonPayloadSerializer() };
            var differences = new List<string>();
            foreach (var codec in codecs)
            {
                var restored = codec.Deserialize(codec.Serialize(original, type), type);
                Compare(original, restored, $"{codec.SerializationMethod}:{type.Name}", differences);
            }

            Assert.True(
                differences.Count == 0,
                $"Members did not survive the round-trip:{Environment.NewLine}{string.Join(Environment.NewLine, differences)}");
        }

        private static object? Sample(Type type, int depth)
        {
            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null) return Sample(underlying, depth);

            if (type == typeof(string)) return "s1";
            if (type == typeof(bool)) return true;
            if (type == typeof(byte)) return (byte)7;
            if (type == typeof(sbyte)) return (sbyte)7;
            if (type == typeof(short)) return (short)7;
            if (type == typeof(ushort)) return (ushort)7;
            if (type == typeof(int)) return 7;
            if (type == typeof(uint)) return 7u;
            if (type == typeof(long)) return 7L;
            if (type == typeof(ulong)) return 7UL;
            if (type == typeof(float)) return 1.5f;
            if (type == typeof(double)) return 1.5d;
            if (type == typeof(decimal)) return 12.5m;
            if (type == typeof(Guid)) return s_sampleGuid;
            if (type == typeof(DateTime)) return s_sampleDateTime;
            if (type == typeof(DateTimeOffset)) return new DateTimeOffset(s_sampleDateTime);
            if (type == typeof(TimeSpan)) return TimeSpan.FromMinutes(90);
            if (type == typeof(DateOnly)) return DateOnly.FromDateTime(s_sampleDateTime);
            if (type == typeof(byte[])) return new byte[] { 1, 2, 3 };
            if (type == typeof(object)) return 42;
            if (type.IsEnum) return NonDefaultEnumValue(type);
            if (type == typeof(DataTable)) return SampleTable();
            if (type == typeof(DataSet))
            {
                var dataSet = new DataSet("ds");
                dataSet.Tables.Add(SampleTable());
                return dataSet;
            }

            if (depth > MaxDepth) return null;

            if (type.IsArray)
            {
                var element = type.GetElementType()!;
                var array = Array.CreateInstance(element, 1);
                array.SetValue(Sample(element, depth + 1), 0);
                return array;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = (IList)Activator.CreateInstance(type)!;
                list.Add(Sample(type.GetGenericArguments()[0], depth + 1));
                return list;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var arguments = type.GetGenericArguments();
                var dictionary = (IDictionary)Activator.CreateInstance(type)!;
                dictionary.Add(Sample(arguments[0], depth + 1)!, Sample(arguments[1], depth + 1));
                return dictionary;
            }

            if (type.IsAbstract)
            {
                var concrete = type.Assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(type))
                    .OrderBy(t => t.Name, StringComparer.Ordinal)
                    .First();
                return Sample(concrete, depth);
            }

            var instance = Activator.CreateInstance(type)!;
            if (WireClosure.FrameworkCollectionItem(type) is { } item)
            {
                var add = type.GetMethod("Add", [item])
                    ?? throw new InvalidOperationException($"{type.Name} has no Add({item.Name}).");
                // Past the depth limit a recursive item type (a department's children) stays an empty collection.
                if (Sample(item, depth + 1) is { } element) add.Invoke(instance, [element]);
                return instance;
            }

            foreach (var property in WireClosure.Members(type))
            {
                var value = Sample(property.PropertyType, depth + 1);
                if (value != null) property.SetValue(instance, value);
            }
            return instance;
        }

        private static object NonDefaultEnumValue(Type type)
        {
            var values = Enum.GetValues(type);
            var clrDefault = Activator.CreateInstance(type);
            foreach (var value in values)
            {
                if (!Equals(value, clrDefault)) return value;
            }
            return values.GetValue(0)!;
        }

        private static DataTable SampleTable()
        {
            var table = new DataTable("t");
            table.Columns.Add("code", typeof(string));
            table.Columns.Add("amount", typeof(decimal));
            table.Rows.Add("c1", 12.5m);
            table.AcceptChanges();
            return table;
        }

        private static bool IsDefault(Type type, object? value)
        {
            if (value == null) return true;
            if (value is ICollection { Count: 0 }) return true;
            return type.IsValueType && Nullable.GetUnderlyingType(type) == null && Equals(value, Activator.CreateInstance(type));
        }

        private static void Compare(object? expected, object? actual, string path, List<string> differences)
        {
            if (expected == null || actual == null)
            {
                if (expected != actual) differences.Add($"{path}: expected {Describe(expected)}, got {Describe(actual)}");
                return;
            }

            var type = expected.GetType();
            if (actual.GetType() != type)
            {
                differences.Add($"{path}: expected a {type.Name}, got a {actual.GetType().Name}");
                return;
            }

            switch (expected)
            {
                case DateTime expectedTime:
                    var actualTime = (DateTime)actual;
                    if (expectedTime != actualTime || expectedTime.Kind != actualTime.Kind)
                        differences.Add($"{path}: expected {expectedTime:O}, got {actualTime:O}");
                    return;
                case byte[] expectedBytes:
                    if (!expectedBytes.SequenceEqual((byte[])actual))
                        differences.Add($"{path}: the bytes differ");
                    return;
                case DataTable expectedTable:
                    CompareTables(expectedTable, (DataTable)actual, path, differences);
                    return;
                case DataSet expectedSet:
                    var actualSet = (DataSet)actual;
                    if (expectedSet.Tables.Count != actualSet.Tables.Count)
                    {
                        differences.Add($"{path}: expected {expectedSet.Tables.Count} tables, got {actualSet.Tables.Count}");
                        return;
                    }
                    for (var i = 0; i < expectedSet.Tables.Count; i++)
                        CompareTables(expectedSet.Tables[i], actualSet.Tables[i], $"{path}.Tables[{i}]", differences);
                    return;
                case IDictionary expectedDictionary:
                    var actualDictionary = (IDictionary)actual;
                    if (expectedDictionary.Count != actualDictionary.Count)
                        differences.Add($"{path}: expected {expectedDictionary.Count} entries, got {actualDictionary.Count}");
                    foreach (DictionaryEntry entry in expectedDictionary)
                        Compare(entry.Value, actualDictionary.Contains(entry.Key) ? actualDictionary[entry.Key] : null, $"{path}[{entry.Key}]", differences);
                    return;
            }

            if (type.IsPrimitive || type.IsEnum || expected is string or decimal or Guid or DateTimeOffset or TimeSpan or DateOnly)
            {
                if (!Equals(expected, actual))
                    differences.Add($"{path}: expected {Describe(expected)}, got {Describe(actual)}");
                return;
            }

            if (expected is IEnumerable expectedItems)
            {
                var left = expectedItems.Cast<object?>().ToList();
                var right = ((IEnumerable)actual).Cast<object?>().ToList();
                if (left.Count != right.Count)
                {
                    differences.Add($"{path}: expected {left.Count} items, got {right.Count}");
                    return;
                }
                for (var i = 0; i < left.Count; i++)
                    Compare(left[i], right[i], $"{path}[{i}]", differences);
                return;
            }

            foreach (var property in WireClosure.Members(type))
                Compare(property.GetValue(expected), property.GetValue(actual), $"{path}.{property.Name}", differences);
        }

        private static void CompareTables(DataTable expected, DataTable actual, string path, List<string> differences)
        {
            if (expected.Rows.Count != actual.Rows.Count || expected.Columns.Count != actual.Columns.Count)
            {
                differences.Add($"{path}: expected {expected.Rows.Count} rows of {expected.Columns.Count} columns, got {actual.Rows.Count} of {actual.Columns.Count}");
                return;
            }
            for (var r = 0; r < expected.Rows.Count; r++)
            {
                foreach (DataColumn column in expected.Columns)
                    Compare(expected.Rows[r][column.ColumnName], actual.Rows[r][column.ColumnName], $"{path}[{r}].{column.ColumnName}", differences);
            }
        }

        private static string Describe(object? value)
            => value == null ? "null" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.GetType().Name;
    }
}
