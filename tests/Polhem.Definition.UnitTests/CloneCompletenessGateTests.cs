using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Xml.Serialization;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// Checks that the <c>Clone</c> of every definition type copies every member the definition file can set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each <c>Clone</c> copies its members by hand, so a property added later is silently dropped unless someone
    /// remembers to add it there too. Nothing else notices: the definition still loads, and only the copy is wrong.
    /// <see cref="LayoutField.Clone"/> once dropped <see cref="LayoutField.RowSpan"/> and
    /// <see cref="LayoutField.ColumnSpan"/>, and because the form loader clones every layout before applying the
    /// localized captions, merged fields never reached any front end.
    /// </para>
    /// <para>
    /// The gate sets every public settable member that is not <c>[XmlIgnore]</c> to a value other than its default,
    /// fills every child collection with one item, clones, and compares member by member.
    /// </para>
    /// </remarks>
    public class CloneCompletenessGateTests
    {
        /// <summary>
        /// Members a <c>Clone</c> deliberately does not copy, with the reason. Empty today.
        /// </summary>
        private static readonly Dictionary<string, string> s_notCopied = new(StringComparer.Ordinal);

        [Fact]
        [DisplayName("Clone of every definition type copies every member the definition file can set")]
        public void Clone_CopiesEveryMappedMember()
        {
            var types = CloneableTypes();
            var violations = new List<string>();
            var checkedMembers = 0;

            foreach (var type in types)
            {
                var source = CreatePopulated(type, violations);
                if (source == null) continue;

                var clone = type.GetMethod("Clone", Type.EmptyTypes)!.Invoke(source, null)!;
                Assert.NotSame(source, clone);

                foreach (var property in ScalarMembers(type))
                {
                    if (s_notCopied.ContainsKey($"{type.Name}.{property.Name}")) continue;
                    checkedMembers++;
                    var expected = property.GetValue(source);
                    var actual = property.GetValue(clone);
                    if (!Equals(expected, actual))
                        violations.Add($"{type.FullName}.{property.Name}: expected '{expected}', the clone has '{actual}'");
                }

                foreach (var property in CollectionMembers(type))
                {
                    if (s_notCopied.ContainsKey($"{type.Name}.{property.Name}")) continue;
                    checkedMembers++;
                    var expected = (IEnumerable?)property.GetValue(source);
                    var actual = (IEnumerable?)property.GetValue(clone);
                    var expectedCount = expected?.Cast<object>().Count() ?? 0;
                    var actualCount = actual?.Cast<object>().Count() ?? 0;
                    if (expectedCount != actualCount)
                        violations.Add($"{type.FullName}.{property.Name}: expected {expectedCount} items, the clone has {actualCount}");
                    else if (expectedCount > 0 && ReferenceEquals(expected, actual))
                        violations.Add($"{type.FullName}.{property.Name}: the clone shares the source collection");
                }
            }

            // Anti-vacuous: the filters above must still reach the types and members this gate exists for.
            Assert.True(types.Count >= 15, $"Only {types.Count} cloneable definition types were found.");
            Assert.Contains(typeof(LayoutField), types);
            Assert.Contains(typeof(FormField), types);
            Assert.True(checkedMembers >= 100, $"Only {checkedMembers} members were compared.");
            Assert.True(
                violations.Count == 0,
                $"These Clone methods lose members; copy them or list them in s_notCopied with the reason:{Environment.NewLine}" +
                string.Join(Environment.NewLine, violations));
        }

        /// <summary>
        /// Public definition types that declare a public parameterless <c>Clone</c> returning their own type.
        /// </summary>
        private static List<Type> CloneableTypes()
            => typeof(FormSchema).Assembly.GetTypes()
                .Where(t => t.IsPublic && t.IsClass && !t.IsAbstract)
                .Where(t => t.Namespace?.StartsWith("Polhem.Definition", StringComparison.Ordinal) == true)
                .Where(t => t.GetMethod("Clone", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)?.ReturnType == t)
                .OrderBy(t => t.FullName, StringComparer.Ordinal)
                .ToList();

        private static IEnumerable<PropertyInfo> MappedMembers(Type type)
            => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length == 0)
                .Where(p => p.GetMethod is { IsPublic: true })
                .Where(p => !p.IsDefined(typeof(XmlIgnoreAttribute)));

        private static IEnumerable<PropertyInfo> ScalarMembers(Type type)
            => MappedMembers(type).Where(p => p.SetMethod is { IsPublic: true } && IsScalar(p.PropertyType));

        private static IEnumerable<PropertyInfo> CollectionMembers(Type type)
            => MappedMembers(type).Where(p => XmlSerializerShapeGateTests.IsCollection(p.PropertyType));

        private static bool IsScalar(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)
                || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(TimeSpan);
        }

        /// <summary>
        /// Creates an instance whose scalar members all differ from their defaults and whose collections hold one item.
        /// </summary>
        private static object? CreatePopulated(Type type, List<string> violations, int depth = 0)
        {
            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                violations.Add($"{type.FullName}: no public parameterless constructor, so the gate cannot build one");
                return null;
            }

            var instance = Activator.CreateInstance(type)!;
            foreach (var property in ScalarMembers(type))
            {
                var current = property.GetValue(instance);
                var value = OtherValue(property, current);
                property.SetValue(instance, value);
                if (Equals(property.GetValue(instance), current))
                    violations.Add($"{type.FullName}.{property.Name}: the gate could not give it a non-default value");
            }

            if (depth > 2) return instance;
            foreach (var property in CollectionMembers(type))
            {
                if (property.GetValue(instance) is not { } collection) continue;
                var add = collection.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "Add" && m.GetParameters().Length == 1);
                if (add == null) continue;
                var item = CreatePopulated(add.GetParameters()[0].ParameterType, violations, depth + 1);
                if (item != null) add.Invoke(collection, [item]);
            }
            return instance;
        }

        private static object OtherValue(PropertyInfo property, object? current)
        {
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (type == typeof(string)) return $"{property.DeclaringType!.Name}.{property.Name}";
            if (type == typeof(bool)) return !(bool)(current ?? false);
            if (type.IsEnum)
            {
                return Enum.GetValues(type).Cast<object>().First(v => !v.Equals(current));
            }
            if (type == typeof(Guid)) return Guid.NewGuid();
            if (type == typeof(DateTime)) return new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
            if (type == typeof(TimeSpan)) return TimeSpan.FromMinutes(7);
            var number = Convert.ToDecimal(current ?? 0, System.Globalization.CultureInfo.InvariantCulture) + 3;
            return Convert.ChangeType(number, type, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
