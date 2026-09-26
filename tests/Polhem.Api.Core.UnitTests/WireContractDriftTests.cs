using System.ComponentModel;
using System.Data;
using System.Reflection;
using System.Text.Json.Serialization;
using Polhem.Api.Core.MessagePack;
using Polhem.Base.Collections;
using MessagePack.Formatters;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Guards against drift between the wire types and their formatter registrations.
    /// </summary>
    /// <remarks>
    /// The compiler does not bind a type to its formatter: adding a type under `Polhem.Api.Core.Messages.*`,
    /// or a property to an existing wire type, reminds nobody to add the registration. On desktop the contractless
    /// resolver silently takes over, until iOS blows up with <c>FormatterNotRegisteredException</c> or a field
    /// silently disappears.
    /// <para>
    /// This test walks the type closure from the same roots as <c>WireContracts</c> and compares it with the
    /// registration list type by type. It is the only automated check of the "must register explicitly" rule.
    /// </para>
    /// </remarks>
    public class WireContractDriftTests
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
        /// Types the closure must always contain. If any is missing, the closure itself is broken.
        /// </summary>
        /// <remarks>
        /// The closure starts from a namespace **string** match. If a namespace is renamed or the message types move,
        /// the closure does not become empty; it **partly shrinks** to only <c>s_extraRoots</c>. Then
        /// <c>missing.Count == 0</c> still holds and both checks become vacuously true. A plain <c>NotEmpty</c>
        /// cannot catch that shrinkage, so specific types are pinned here.
        /// </remarks>
        /// <remarks>
        /// The types are deliberately taken from different reachability paths: the root of the message namespace,
        /// the root of the contracts namespace, a collection hanging off <c>ApiMessageBase</c> (and so passed by
        /// every message), and a polymorphic subtype. A broken path is named, instead of only showing up as a
        /// smaller number.
        /// <para>
        /// Note that definition types such as <c>FormSchema</c> are **not** in the closure: they travel on the wire
        /// as XML strings, not as objects. The first version listed it as a canary and this test rejected it on the
        /// spot, which also shows why the lower-bound assertion cannot be just a number.
        /// </para>
        /// </remarks>
        private static readonly Type[] s_closureCanaries =
        [
            typeof(Polhem.Api.Core.Messages.Form.GetListRequest),
            typeof(Polhem.Api.Core.Messages.System.LoginRequest),
            typeof(Polhem.Definition.Collections.ParameterCollection),
            typeof(Polhem.Definition.Filters.FilterCondition),
        ];

        [Fact]
        [DisplayName("The type closure and the registration list are neither empty nor shrunken (so the drift checks cannot pass vacuously)")]
        public void WireTypeClosure_AndRegistrations_AreNotVacuous()
        {
            var closure = WireTypeClosure();
            var contracts = MessagePackCodec.RegisteredFormatters.OfType<IWireContract>().ToList();

            // The lower bound is deliberately looser than the current count. It is meant to catch shrinkage on the
            // scale of "only `s_extraRoots` left", not to force a number change every time a type is added.
            Assert.True(closure.Count > 80,
                $"The wire type closure has only {closure.Count} types, far fewer than expected. The closure roots come from a namespace string match; " +
                "if a namespace is renamed or the message types move, the drift checks below silently become vacuously true.");
            Assert.True(contracts.Count > 80,
                $"Only {contracts.Count} IWireContract registrations, far fewer than expected. " +
                "WireContracts_MatchTypeShape would then loop over nothing and pass vacuously.");

            var missingCanaries = s_closureCanaries
                .Where(t => !closure.Contains(t))
                .Select(t => t.FullName!)
                .ToList();
            Assert.True(missingCanaries.Count == 0,
                $"These types must be in the wire type closure, but are not:{Environment.NewLine}" +
                string.Join(Environment.NewLine, missingCanaries));
        }

        [Fact]
        [DisplayName("Every type in the wire type closure has an explicitly registered formatter")]
        public void WireTypeClosure_IsFullyRegistered()
        {
            var registered = RegisteredTypes();
            var missing = WireTypeClosure()
                .Where(t => !registered.Contains(t))
                .Select(t => t.FullName!)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            Assert.True(
                missing.Count == 0,
                $"These wire types have no explicit formatter and will fail on iOS (no dynamic code):{Environment.NewLine}" +
                string.Join(Environment.NewLine, missing));
        }

        [Fact]
        [DisplayName("Every registered wire contract is for a type reachable from the closure (the reverse direction)")]
        public void RegisteredContracts_AreReachableFromTheClosure()
        {
            var closure = WireTypeClosure();

            var unreachable = MessagePackCodec.RegisteredFormatters
                .OfType<IWireContract>()
                .Select(c => c.WireType)
                .Where(t => !closure.Contains(t))
                .Select(t => t.FullName!)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            // `WireTypeClosure_IsFullyRegistered` checks "closure ⊆ registrations" and cannot catch the reverse,
            // "registered but reachable from nowhere". `ApiErrorInfo` was such a case: long replaced by `JsonRpcError`
            // with zero consumers, it still sat in `WireContracts`, and no mechanism pointed it out in two health checks.
            // This check closes that gap. An extra registration is not harmless redundancy: it makes a dead type look alive.
            Assert.True(
                unreachable.Count == 0,
                $"These types have a registered wire contract but are not in the wire type closure. Either they are dead code, or the closure misses a path:" +
                $"{Environment.NewLine}{string.Join(Environment.NewLine, unreachable)}");
        }

        [Fact]
        [DisplayName("The member list of every WireContract matches the current shape of its type")]
        public void WireContracts_MatchTypeShape()
        {
            var drift = new List<string>();

            foreach (var contract in MessagePackCodec.RegisteredFormatters.OfType<IWireContract>())
            {
                var expected = WireMemberNames(contract.WireType);
                var actual = contract.WireMemberNames.ToList();

                var onlyOnType = expected.Except(actual, StringComparer.Ordinal).ToList();
                var onlyInContract = actual.Except(expected, StringComparer.Ordinal).ToList();

                if (onlyOnType.Count > 0)
                    drift.Add($"{contract.WireType.FullName}: on the type but not registered → {string.Join(", ", onlyOnType)}");
                if (onlyInContract.Count > 0)
                    drift.Add($"{contract.WireType.FullName}: registered but no longer on the type → {string.Join(", ", onlyInContract)}");
            }

            Assert.True(
                drift.Count == 0,
                $"Wire contracts do not match the type shapes:{Environment.NewLine}{string.Join(Environment.NewLine, drift)}");
        }

        /// <summary>
        /// Wire members are defined the same way as for JSON: public readable and writable properties not excluded by
        /// <c>[JsonIgnore]</c>. The framework-managed members (<c>Tag</c> / <c>Key</c> / <c>SerializeState</c>) all
        /// carry that attribute, so they are excluded automatically.
        /// </summary>
        /// <remarks>
        /// WARNING: <see cref="JsonIgnoreAttribute.Condition"/> must be read; the presence of the attribute is not
        /// enough. <c>[JsonIgnore(Condition = JsonIgnoreCondition.Never)]</c> means **never ignore**, and a presence
        /// check would judge it "ignored", the exact opposite. <c>FormField</c> and <c>DbField</c> already use this
        /// form. They are not in the wire closure today, so nothing broke, but that is luck, not design.
        /// <para>
        /// The same bug once existed in POLHEM4007 (the rule checked only that the attribute was present and did not
        /// read <c>Condition</c>). Its cause was recorded when that rule was removed on 2026-07-30, and then it lived
        /// on here.
        /// </para>
        /// </remarks>
        private static List<string> WireMemberNames(Type type) =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length == 0)
                .Where(p => p.GetMethod is { IsPublic: true } && p.SetMethod is { IsPublic: true })
                .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is not { } ignore
                            || ignore.Condition == JsonIgnoreCondition.Never)
                .Select(p => p.Name)
                .ToList();

        /// <summary>
        /// The types covered by the registered formatters (taken from the T of <c>IMessagePackFormatter&lt;T&gt;</c>).
        /// </summary>
        private static HashSet<Type> RegisteredTypes()
        {
            var types = new HashSet<Type>();
            foreach (var formatter in MessagePackCodec.RegisteredFormatters)
            {
                foreach (var i in formatter.GetType().GetInterfaces())
                {
                    if (!i.IsGenericType || i.GetGenericTypeDefinition() != typeof(IMessagePackFormatter<>))
                        continue;
                    var t = i.GetGenericArguments()[0];
                    types.Add(Nullable.GetUnderlyingType(t) ?? t);
                }
            }
            return types;
        }

        /// <summary>
        /// Walks the type closure from the API message contracts and returns the types in it that need an explicit
        /// formatter.
        /// </summary>
        private static HashSet<Type> WireTypeClosure()
        {
            var needs = new HashSet<Type>();
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

            return needs;

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
                    return;
                }

                if (!type.IsClass) return;

                if (FrameworkCollectionItem(type) is { } item)
                {
                    needs.Add(type);
                    Visit(item);
                    return;
                }

                if (!type.IsAbstract) needs.Add(type);
                foreach (var name in WireMemberNames(type))
                    Visit(type.GetProperty(name)!.PropertyType);
                foreach (var derived in type.Assembly.GetTypes().Where(x => x.BaseType == type && !x.IsAbstract))
                    Visit(derived);
            }
        }

        private static Type? FrameworkCollectionItem(Type type)
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
