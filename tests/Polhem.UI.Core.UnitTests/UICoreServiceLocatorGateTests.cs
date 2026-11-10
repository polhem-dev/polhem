using System.ComponentModel;
using System.Reflection;
using System.Reflection.Emit;
using Polhem.UI.Core.DataObjects;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// Nothing in <c>Polhem.UI.Core</c> but <see cref="UIServices"/> itself reads the process-wide <see cref="UIServices"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Blazor Server head runs this assembly (<see cref="FormDataObject"/> and the value rules it calls) for many users
    /// in one process. A value read from <see cref="UIServices"/> there would belong to whoever registered it, so anything
    /// per user (the time zone, for one) is passed in by the head that constructs the object. The scan covers the whole
    /// assembly rather than <see cref="FormDataObject"/> alone, because the rules it calls live in other namespaces.
    /// </para>
    /// <para>
    /// The Blazor head's own gate, <c>UIServicesUsageGateTests</c>, reads the type references of the Blazor assemblies.
    /// A call made inside <c>Polhem.UI.Core</c> is to a type of the same assembly and leaves no type reference, so that
    /// gate cannot see it. This one reads the IL of every method in the assembly instead, compiler-generated closures
    /// and async state machines included.
    /// </para>
    /// </remarks>
    public class UICoreServiceLocatorGateTests
    {
        private static readonly Dictionary<short, OpCode> s_opCodes = typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (OpCode)f.GetValue(null)!)
            .ToDictionary(op => op.Value);

        private static IEnumerable<MethodBase> MethodsOf(Type type)
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var method in type.GetMethods(all))
                yield return method;
            foreach (var constructor in type.GetConstructors(all))
                yield return constructor;
            foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            {
                foreach (var method in MethodsOf(nested))
                    yield return method;
            }
        }

        /// <summary>Returns every member a method's IL refers to through a metadata token.</summary>
        private static IEnumerable<MemberInfo> ReferencedMembers(MethodBase method)
        {
            var il = method.GetMethodBody()?.GetILAsByteArray();
            if (il is null) yield break;

            var position = 0;
            while (position < il.Length)
            {
                short value = il[position++];
                if (value == 0xFE) value = (short)(0xFE00 | il[position++]);
                var opCode = s_opCodes[value];

                switch (opCode.OperandType)
                {
                    case OperandType.InlineMethod:
                    case OperandType.InlineField:
                    case OperandType.InlineTok:
                        var token = BitConverter.ToInt32(il, position);
                        var member = Resolve(method, token);
                        if (member is not null) yield return member;
                        position += 4;
                        break;
                    case OperandType.InlineSwitch:
                        position += 4 + (4 * BitConverter.ToInt32(il, position));
                        break;
                    case OperandType.InlineNone:
                        break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar:
                        position += 1;
                        break;
                    case OperandType.InlineVar:
                        position += 2;
                        break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR:
                        position += 8;
                        break;
                    default:
                        position += 4;
                        break;
                }
            }
        }

        private static MemberInfo? Resolve(MethodBase method, int token)
        {
            var typeArguments = method.DeclaringType?.IsGenericType == true
                ? method.DeclaringType.GetGenericArguments()
                : null;
            var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
            try
            {
                return method.Module.ResolveMember(token, typeArguments, methodArguments);
            }
            catch (ArgumentException)
            {
                // A token for a type or a signature, not a member; neither can be a call into UIServices.
                return null;
            }
        }

        private static bool IsUIServicesMember(MemberInfo member) => member.DeclaringType == typeof(UIServices);

        // Types declared in source only: coverage instrumentation injects a tracker type with no namespace.
        private static MethodBase[] UICoreMethods() => typeof(UIServices).Assembly.GetTypes()
            .Where(t => !t.IsNested && t != typeof(UIServices)
                && t.Namespace?.StartsWith("Polhem.UI.Core", StringComparison.Ordinal) == true)
            .SelectMany(MethodsOf)
            .ToArray();

        [Fact]
        [DisplayName("No method in Polhem.UI.Core but UIServices itself calls UIServices")]
        public void UICore_DoesNotCallUIServices()
        {
            var methods = UICoreMethods();

            var offenders = methods
                .Where(m => ReferencedMembers(m).Any(IsUIServicesMember))
                .Select(m => $"{m.DeclaringType?.FullName}.{m.Name}")
                .ToArray();

            Assert.True(offenders.Length == 0,
                $"These methods read the process-wide UIServices; take the value from the constructing head instead: {string.Join(", ", offenders)}");
        }

        [Fact]
        [DisplayName("The scan reaches the data object's members, its async state machines and the calls inside them")]
        public void Scan_SeesDataObjectCalls()
        {
            var methods = UICoreMethods();

            // `SetField` calls `FormValueBinding.ToColumnValue` directly; `LoadAsync` calls
            // `FormDataGuard.RequireConnector` from inside its compiler-generated state machine. A type outside
            // `Polhem.UI.Core.DataObjects` is scanned too.
            Assert.Contains(methods, m => ReferencedMembers(m).Any(r =>
                r.DeclaringType == typeof(FormValueBinding) && r.Name == nameof(FormValueBinding.ToColumnValue)));
            Assert.Contains(methods, m => m.DeclaringType?.DeclaringType == typeof(FormDataObject)
                && ReferencedMembers(m).Any(r => r.DeclaringType == typeof(FormDataGuard)));
            Assert.Contains(methods, m => m.DeclaringType == typeof(FormValueBinding));
        }

        [Fact]
        [DisplayName("The scan reports a call to UIServices, so the gate cannot pass vacuously")]
        public void Scan_DetectsUIServicesCall()
        {
            var probe = typeof(UICoreServiceLocatorGateTests).GetMethod(
                nameof(ReadsUIServices), BindingFlags.NonPublic | BindingFlags.Static)!;

            Assert.Contains(ReferencedMembers(probe), IsUIServicesMember);
        }

        private static string ReadsUIServices() => UIServices.Get<IUserContext>()?.User?.TimeZone ?? string.Empty;
    }
}
