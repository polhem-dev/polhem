using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// Checks every type the definition files reach against the shape rules of the reflection-only
    /// <c>XmlSerializer</c> the iOS heads use.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each rule breaks only on a device, and each with a misleading symptom: a collection with more than one public
    /// <c>Add</c> throws <c>AmbiguousMatchException</c>, a type without a public parameterless constructor throws
    /// <c>MissingMethodException</c>, and a collection mapped to repeated <c>[XmlElement]</c> without a public setter
    /// throws "Property set method not found". The desktop serializer accepts all three.
    /// </para>
    /// <para>
    /// The rules were checked by a reflection scan run by hand. The CI AOT gate would only catch a violation if some
    /// test happened to round-trip a populated instance of the offending type; this walks every type the roots reach.
    /// </para>
    /// </remarks>
    public class XmlSerializerShapeGateTests
    {
        private static readonly Type[] s_roots =
        [
            typeof(ClientSettings),
            typeof(CommonConfiguration),
            typeof(SessionUser),
            typeof(PermissionModels),
            typeof(PluginSettings),
            typeof(MenuSettings),
            typeof(ProgramSettings),
            typeof(SystemSettings),
            typeof(DatabaseSettings),
            typeof(DbCategorySettings),
            typeof(FormSchema),
            typeof(FormLayout),
            typeof(TableSchema),
            typeof(LanguageResource),
            typeof(CompanyNumberFormats),
            typeof(CompanyCashRounding),
            typeof(CompanyAllowedCurrencies),
            typeof(CurrencySettings),
            typeof(UnitSettings),
        ];

        [Fact]
        [DisplayName("Every type the definition files reach has the shape the reflection-only XmlSerializer requires")]
        public void DefinitionTypes_MeetTheReflectionOnlyXmlSerializerShapeRules()
        {
            var types = Walk();
            var violations = new List<string>();

            foreach (var type in types.OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                if (!type.IsAbstract && type.GetConstructor(Type.EmptyTypes) == null)
                    violations.Add($"{type.FullName}: no public parameterless constructor");

                if (IsCollection(type))
                {
                    var adds = type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Count(m => m.Name == "Add");
                    if (adds > 1)
                        violations.Add($"{type.FullName}: {adds} public Add methods; exactly one is allowed");
                    continue;
                }

                foreach (var property in XmlMembers(type))
                {
                    if (property.IsDefined(typeof(XmlElementAttribute)) && IsCollection(property.PropertyType)
                        && property.SetMethod is not { IsPublic: true })
                    {
                        violations.Add($"{type.FullName}.{property.Name}: a collection mapped to [XmlElement] without a public setter");
                    }
                }
            }

            // Anti-vacuous: the walk must still reach well past the roots.
            Assert.True(types.Count > 50, $"The walk reached only {types.Count} types.");
            Assert.Contains(typeof(FormField), types);
            Assert.True(
                violations.Count == 0,
                $"These definition types break the reflection-only XmlSerializer on the iOS heads:{Environment.NewLine}" +
                string.Join(Environment.NewLine, violations));
        }

        [Fact]
        [DisplayName("A definition member excluded from XML is excluded from JSON too")]
        public void XmlIgnoredMembers_AreAlsoJsonIgnored()
        {
            // `JsonCodec` can write a definition as well as `XmlCodec`. A member only `[XmlIgnore]` hides still reaches
            // the JSON output, and a get-only one is read to do so, which for `FormTable.RelationFieldReferences`
            // forces its lazy index to build.
            var asymmetric = Walk()
                .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(p => p.GetIndexParameters().Length == 0 && p.GetMethod is { IsPublic: true })
                    .Where(p => p.IsDefined(typeof(XmlIgnoreAttribute)))
                    .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is not { Condition: JsonIgnoreCondition.Always })
                    .Select(p => $"{t.FullName}.{p.Name}"))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            Assert.True(
                asymmetric.Count == 0,
                $"These members are [XmlIgnore] but still serialized to JSON; add [JsonIgnore]:{Environment.NewLine}" +
                string.Join(Environment.NewLine, asymmetric));
        }

        [Fact]
        [DisplayName("An XmlSerializer can be constructed for every definition file root")]
        public void DefinitionRoots_BuildAnXmlSerializer()
        {
            foreach (var root in s_roots)
            {
                var exception = Record.Exception(() => new XmlSerializer(root));
                Assert.True(exception == null, $"{root.Name}: {exception}");
            }
        }

        /// <summary>
        /// Collects every Polhem type reachable from the roots through mapped members, collection items,
        /// <c>[XmlInclude]</c> and the types named by <c>[XmlElement]</c> / <c>[XmlArrayItem]</c>.
        /// </summary>
        private static HashSet<Type> Walk()
        {
            var seen = new HashSet<Type>();
            var pending = new Queue<Type>(s_roots);
            while (pending.Count > 0)
            {
                var type = pending.Dequeue();
                type = Nullable.GetUnderlyingType(type) ?? type;
                if (type.IsArray) type = type.GetElementType()!;
                if (!type.IsClass || type.Namespace?.StartsWith("Polhem.", StringComparison.Ordinal) != true) continue;
                if (!seen.Add(type)) continue;

                foreach (var include in type.GetCustomAttributes<XmlIncludeAttribute>())
                {
                    if (include.Type != null) pending.Enqueue(include.Type);
                }

                if (IsCollection(type))
                {
                    if (ItemType(type) is { } item) pending.Enqueue(item);
                    continue;
                }

                foreach (var property in XmlMembers(type))
                {
                    pending.Enqueue(property.PropertyType);
                    foreach (var element in property.GetCustomAttributes<XmlElementAttribute>())
                    {
                        if (element.Type != null) pending.Enqueue(element.Type);
                    }
                    foreach (var arrayItem in property.GetCustomAttributes<XmlArrayItemAttribute>())
                    {
                        if (arrayItem.Type != null) pending.Enqueue(arrayItem.Type);
                    }
                }
            }
            return seen;
        }

        /// <summary>
        /// The item type of a collection: the parameter of its single <c>Add</c>, which is also what
        /// <c>XmlSerializer</c> reads.
        /// </summary>
        private static Type? ItemType(Type collection)
            => collection.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "Add" && m.GetParameters().Length == 1)
                ?.GetParameters()[0].ParameterType;

        private static bool IsCollection(Type type)
            => type != typeof(string) && !type.IsArray && typeof(IEnumerable).IsAssignableFrom(type);

        /// <summary>
        /// The members <c>XmlSerializer</c> maps: public properties with a public getter that are not
        /// <c>[XmlIgnore]</c>, and either have a public setter or hold a collection it fills in place.
        /// </summary>
        private static IEnumerable<PropertyInfo> XmlMembers(Type type)
            => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length == 0)
                .Where(p => p.GetMethod is { IsPublic: true })
                .Where(p => !p.IsDefined(typeof(XmlIgnoreAttribute)))
                .Where(p => p.SetMethod is { IsPublic: true } || IsCollection(p.PropertyType));
    }
}
