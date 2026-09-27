using System.Data;
using System.Reflection;
using System.Text.Json.Serialization;
using Polhem.Api.Core.MessagePack;
using Polhem.Base.Collections;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// The one definition of "what goes on the wire" shared by the wire gates: which members a type carries, and which
    /// types the message contracts reach.
    /// </summary>
    /// <remarks>
    /// <c>WireContractDriftTests</c>, <c>WireContractGenerator</c> and <c>WireCodecParityTests</c> all read it. The
    /// generator once kept its own member rule, in which <c>[JsonIgnore]</c> counted by presence and non-public setters
    /// were admitted, so the contract it wrote and the registrations the drift test checked could describe different
    /// member sets without either noticing.
    /// </remarks>
    internal static class WireClosure
    {
        /// <summary>
        /// Types that are not reached through any message property but do go on the wire (hidden inside an
        /// `object` member, or obtained as definition data).
        /// </summary>
        private static readonly Type[] s_extraRoots =
        [
            typeof(Polhem.Definition.Collections.ListItemCollection),
            typeof(Polhem.Definition.Collections.PropertyCollection),
            typeof(Polhem.Definition.Settings.CurrencySettings),
            typeof(Polhem.Definition.Settings.UnitSettings),
            typeof(SerializableDataSet),
            typeof(SerializableDataTable),
        ];

        /// <summary>
        /// The wire members of a type: public readable and writable properties that <c>[JsonIgnore]</c> does not
        /// always exclude. The framework-managed members (<c>Tag</c> / <c>Key</c>) carry that attribute, so they are
        /// excluded automatically.
        /// </summary>
        /// <remarks>
        /// WARNING: <see cref="JsonIgnoreAttribute.Condition"/> must be read; the presence of the attribute is not
        /// enough. <c>[JsonIgnore(Condition = JsonIgnoreCondition.Never)]</c> means **never ignore**, and a presence
        /// check would judge it "ignored", the exact opposite. The two <c>WhenWriting…</c> conditions only omit a
        /// member while it holds a default value, so such a member is on the wire too. Only
        /// <see cref="JsonIgnoreCondition.Always"/>, the attribute's default, takes a member off it.
        /// <para>
        /// The same bug once existed in POLHEM4007 (the rule checked only that the attribute was present and did not
        /// read <c>Condition</c>). Its cause was recorded when that rule was removed on 2026-07-30, and then it lived
        /// on in the drift test and the contract generator.
        /// </para>
        /// </remarks>
        public static IReadOnlyList<PropertyInfo> Members(Type type) =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length == 0)
                .Where(p => p.GetMethod is { IsPublic: true } && p.SetMethod is { IsPublic: true })
                .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is not { Condition: JsonIgnoreCondition.Always })
                .ToList();

        /// <summary>
        /// The names of <see cref="Members"/>, in declaration order.
        /// </summary>
        public static List<string> MemberNames(Type type) => Members(type).Select(p => p.Name).ToList();

        /// <summary>
        /// Whether the JSON wires write the member even when it holds its default value.
        /// </summary>
        /// <remarks>
        /// The JSON body codec and <c>Plain</c> omit a member equal to its CLR default. A member whose initialiser is
        /// not that default opts out with <c>[JsonIgnore(Condition = JsonIgnoreCondition.Never)]</c>, so an explicit
        /// default is written instead of being replaced by the initialiser on the reading end.
        /// </remarks>
        public static bool IsAlwaysWritten(PropertyInfo property)
            => property.GetCustomAttribute<JsonIgnoreAttribute>() is { Condition: JsonIgnoreCondition.Never };

        /// <summary>
        /// Walks the type closure from the API message contracts.
        /// </summary>
        /// <returns>
        /// The types in the closure that need an explicit formatter, and the member types the walk met but has no
        /// rule for. The second set must stay empty: a shape the walk does not understand is a type whose formatter
        /// registration nothing checks.
        /// </returns>
        public static (HashSet<Type> Types, SortedSet<string> UnknownShapes) Walk()
        {
            var needs = new HashSet<Type>();
            var unknown = new SortedSet<string>(StringComparer.Ordinal);
            var seen = new HashSet<Type>();
            var apiCore = typeof(MessagePackCodec).Assembly;
            var contracts = typeof(Polhem.Api.Contracts.Form.IGetListRequest).Assembly;

            foreach (var asm in new[] { apiCore, contracts })
            {
                foreach (var t in asm.GetTypes())
                {
                    if (!t.IsClass || t.IsAbstract || t.IsGenericTypeDefinition) continue;
                    var ns = t.Namespace ?? string.Empty;
                    if (ns.StartsWith("Polhem.Api.Core.Messages", StringComparison.Ordinal) ||
                        ns.StartsWith("Polhem.Api.Contracts", StringComparison.Ordinal))
                    {
                        Visit(t);
                    }
                }
            }
            foreach (var t in s_extraRoots) Visit(t);

            return (needs, unknown);

            void Visit(Type type)
            {
                var underlying = Nullable.GetUnderlyingType(type);
                if (underlying != null)
                {
                    needs.Add(underlying);
                    type = underlying;
                }
                if (!seen.Add(type)) return;

                if (type.IsEnum) { needs.Add(type); return; }
                if (IsBuiltIn(type)) return;
                if (type.IsArray)
                {
                    var element = type.GetElementType()!;
                    if (element != typeof(byte) && element != typeof(object)) needs.Add(type);
                    Visit(element);
                    return;
                }
                if (type == typeof(DataTable) || type == typeof(DataSet)) { needs.Add(type); return; }

                if (type.IsGenericType)
                {
                    var definition = type.GetGenericTypeDefinition();
                    if (definition == typeof(List<>) || definition == typeof(Dictionary<,>))
                    {
                        needs.Add(type);
                        foreach (var a in type.GetGenericArguments()) Visit(a);
                    }
                    else
                    {
                        unknown.Add($"{type} (generic type other than List<T> or Dictionary<TKey, TValue>)");
                    }
                    return;
                }

                if (type.IsInterface) { unknown.Add($"{type} (interface)"); return; }
                if (!type.IsClass) { unknown.Add($"{type} (struct)"); return; }

                if (FrameworkCollectionItem(type) is { } item)
                {
                    needs.Add(type);
                    Visit(item);
                    return;
                }

                if (!type.IsAbstract) needs.Add(type);
                foreach (var property in Members(type))
                    Visit(property.PropertyType);
                foreach (var derived in type.Assembly.GetTypes().Where(x => x.BaseType == type && !x.IsAbstract))
                    Visit(derived);
            }
        }

        /// <summary>
        /// The types in the wire closure that need an explicit formatter.
        /// </summary>
        public static HashSet<Type> Types() => Walk().Types;

        /// <summary>
        /// The item type of a framework collection (<see cref="CollectionBase{T}"/> or
        /// <see cref="KeyCollectionBase{T}"/>), or null.
        /// </summary>
        public static Type? FrameworkCollectionItem(Type type)
        {
            for (var b = type.BaseType; b != null; b = b.BaseType)
            {
                if (!b.IsGenericType) continue;
                var d = b.GetGenericTypeDefinition();
                if (d == typeof(CollectionBase<>) || d == typeof(KeyCollectionBase<>))
                    return b.GetGenericArguments()[0];
            }
            return null;
        }

        private static bool IsBuiltIn(Type t) =>
            t.IsPrimitive || t == typeof(string) || t == typeof(decimal) || t == typeof(Guid) ||
            t == typeof(DateTime) || t == typeof(DateTimeOffset) || t == typeof(TimeSpan) ||
            t == typeof(DateOnly) || t == typeof(TimeOnly) || t == typeof(object) ||
            t == typeof(byte[]) || t == typeof(Type) || t == typeof(Uri) || t == typeof(Version);
    }
}
