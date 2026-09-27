using System.ComponentModel;
using Polhem.Api.Core.MessagePack;
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
            var closure = WireClosure.Types();
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
            var missing = WireClosure.Types()
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
            var closure = WireClosure.Types();

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
        [DisplayName("Every registered formatter covers a type the closure reaches, not only the WireContract ones")]
        public void RegisteredFormatters_AreReachableFromTheClosure()
        {
            var closure = WireClosure.Types();

            // Registered types the closure legitimately omits: `object` members are carried by the value envelope
            // formatter rather than recorded as a type that needs one, and an abstract base such as `FilterNode` is
            // registered for its polymorphic members while only its concrete subtypes are recorded.
            var orphans = RegisteredTypes()
                .Where(t => !closure.Contains(t))
                .Where(t => t != typeof(object))
                .Where(t => !(t.IsAbstract && closure.Any(c => c.IsSubclassOf(t))))
                .Select(t => t.FullName!)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            // `RegisteredContracts_AreReachableFromTheClosure` only looks at `IWireContract`, so an enum, collection or
            // generic registration that nothing reaches (`WireEnumFormatter<PayloadFormat>` was one) went unnoticed.
            Assert.True(
                orphans.Count == 0,
                $"These types have a registered formatter but are not in the wire type closure. Either the registration is dead, or the closure misses a path:" +
                $"{Environment.NewLine}{string.Join(Environment.NewLine, orphans)}");
        }

        [Fact]
        [DisplayName("The closure walk understands every member shape it meets, so no member type escapes the registration check")]
        public void WireTypeClosure_HasNoUnknownShapes()
        {
            var unknown = WireClosure.Walk().UnknownShapes;

            // The walk used to return silently for a generic other than List<T> / Dictionary<TKey, TValue>, a struct or
            // an interface. A member of such a shape then dropped out of the closure, and with it the check that its
            // formatter is registered: the gate passed instead of failing.
            Assert.True(
                unknown.Count == 0,
                $"The wire closure met member types it has no rule for. Teach `WireClosure.Walk` the shape, or change the member:" +
                $"{Environment.NewLine}{string.Join(Environment.NewLine, unknown)}");
        }

        [Fact]
        [DisplayName("The member list of every WireContract matches the current shape of its type")]
        public void WireContracts_MatchTypeShape()
        {
            var drift = new List<string>();

            foreach (var contract in MessagePackCodec.RegisteredFormatters.OfType<IWireContract>())
            {
                var expected = WireClosure.MemberNames(contract.WireType);
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
    }
}
