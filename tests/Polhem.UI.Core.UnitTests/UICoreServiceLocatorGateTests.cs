using System.ComponentModel;
using System.Reflection;
using Polhem.Tests.Shared;
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
        private static bool IsUIServicesMember(MemberInfo member) => member.DeclaringType == typeof(UIServices);

        // Types declared in source only: coverage instrumentation injects a tracker type with no namespace.
        private static MethodBase[] UICoreMethods() => typeof(UIServices).Assembly.GetTypes()
            .Where(t => !t.IsNested && t != typeof(UIServices)
                && t.Namespace?.StartsWith("Polhem.UI.Core", StringComparison.Ordinal) == true)
            .SelectMany(IlMemberReferences.MethodsOf)
            .ToArray();

        [Fact]
        [DisplayName("No method in Polhem.UI.Core but UIServices itself calls UIServices")]
        public void UICore_DoesNotCallUIServices()
        {
            var methods = UICoreMethods();

            var offenders = methods
                .Where(m => IlMemberReferences.Of(m).Any(IsUIServicesMember))
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
            Assert.Contains(methods, m => IlMemberReferences.Of(m).Any(r =>
                r.DeclaringType == typeof(FormValueBinding) && r.Name == nameof(FormValueBinding.ToColumnValue)));
            Assert.Contains(methods, m => m.DeclaringType?.DeclaringType == typeof(FormDataObject)
                && IlMemberReferences.Of(m).Any(r => r.DeclaringType == typeof(FormDataGuard)));
            Assert.Contains(methods, m => m.DeclaringType == typeof(FormValueBinding));
        }

        [Fact]
        [DisplayName("The scan reports a call to UIServices, so the gate cannot pass vacuously")]
        public void Scan_DetectsUIServicesCall()
        {
            var probe = typeof(UICoreServiceLocatorGateTests).GetMethod(
                nameof(ReadsUIServices), BindingFlags.NonPublic | BindingFlags.Static)!;

            Assert.Contains(IlMemberReferences.Of(probe), IsUIServicesMember);
        }

        private static string ReadsUIServices() => UIServices.Get<IUserContext>()?.User?.TimeZone ?? string.Empty;
    }
}
