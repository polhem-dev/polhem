using System.ComponentModel;
using System.Reflection;
using Polhem.Api.Core.Validator;
using Polhem.Business;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Pins the protection level and access requirement of every API method, so that raising the bar becomes a
    /// decision someone has to face.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Raising a method's <c>ProtectionLevel</c> (for example Public → Encrypted) or changing its
    /// <c>AccessRequirement</c> from Anonymous to Authenticated makes **existing clients get rejected on the spot**.
    /// No mechanism used to notice such a change: attribute **arguments** do not go into <c>PublicAPI.Shipped.txt</c>,
    /// so the analyzer cannot see them, and neither can the wire fixtures, because the change does not alter the
    /// payload's shape, only who may send it.
    /// </para>
    /// <para>
    /// Most other contract changes are already guarded: message properties, type names, method names and enum
    /// members are in <c>PublicAPI.Shipped.txt</c> (message types do not use <c>[JsonPropertyName]</c>, so the
    /// name on the wire is the C# name converted to camelCase), and renaming one turns the analyzer red.
    /// Access control is the one exception, so it is pinned here on its own.
    /// </para>
    /// <para>
    /// Adding an API method also turns this test red. That is deliberate: adding the row forces an answer to
    /// "who may call this method, and at what protection level" instead of reusing a copied attribute.
    /// </para>
    /// </remarks>
    public class ApiAccessControlPinTests
    {
        /// <summary>
        /// The expected values are deliberately **strings**, not enum references such as <c>ApiProtectionLevel.Public</c>.
        /// With enum references, renaming a member or changing its meaning would change this table too and the test
        /// would still pass. Strings catch both "switched to another level" and "the level was renamed".
        /// </summary>
        private static readonly Dictionary<string, (string Protection, string Requirement)> s_expected = new(StringComparer.Ordinal)
        {
            { "BusinessObject.ExecFunc", ("Public", "Authenticated") },
            { "BusinessObject.ExecFuncAnonymous", ("Public", "Anonymous") },
            { "FormBusinessObject.Delete", ("Public", "Authenticated") },
            { "FormBusinessObject.GetData", ("Public", "Authenticated") },
            { "FormBusinessObject.GetList", ("Public", "Authenticated") },
            { "FormBusinessObject.GetLookup", ("Public", "Authenticated") },
            { "FormBusinessObject.GetNewData", ("Public", "Authenticated") },
            { "FormBusinessObject.Save", ("Public", "Authenticated") },
            { "LogBusinessObject.GetAccessLog", ("Encrypted", "Authenticated") },
            { "LogBusinessObject.GetApiAnomalyLog", ("Encrypted", "Authenticated") },
            { "LogBusinessObject.GetApiAnomalySummary", ("Encrypted", "Authenticated") },
            { "LogBusinessObject.GetChangeDetail", ("Encrypted", "Authenticated") },
            { "LogBusinessObject.GetChangeLog", ("Encrypted", "Authenticated") },
            { "LogBusinessObject.GetDbAnomalyLog", ("Encrypted", "Authenticated") },
            { "LogBusinessObject.GetDbAnomalySummary", ("Encrypted", "Authenticated") },
            { "LogBusinessObject.GetLoginLog", ("Encrypted", "Authenticated") },
            { "LogBusinessObject.GetTopApiMethods", ("Encrypted", "Authenticated") },
            { "SystemBusinessObject.CreateApiKey", ("Encrypted", "Authenticated") },
            { "SystemBusinessObject.CreateSession", ("LocalOnly", "Anonymous") },
            { "SystemBusinessObject.EnterCompany", ("Public", "Authenticated") },
            { "SystemBusinessObject.GetCommonConfiguration", ("Public", "Anonymous") },
            { "SystemBusinessObject.GetCustomizeFormLayout", ("Public", "Authenticated") },
            { "SystemBusinessObject.GetCustomizeLanguage", ("Public", "Authenticated") },
            { "SystemBusinessObject.GetCustomizePluginSettings", ("LocalOnly", "Authenticated") },
            { "SystemBusinessObject.GetDefine", ("Public", "Authenticated") },
            { "SystemBusinessObject.GetDepartmentTree", ("Public", "Authenticated") },
            { "SystemBusinessObject.GetFormLayout", ("Public", "Authenticated") },
            { "SystemBusinessObject.GetFormSchema", ("Public", "Authenticated") },
            { "SystemBusinessObject.GetLanguage", ("Public", "Authenticated") },
            { "SystemBusinessObject.LeaveCompany", ("Public", "Authenticated") },
            { "SystemBusinessObject.ListApiKeys", ("Encrypted", "Authenticated") },
            { "SystemBusinessObject.Login", ("Public", "Anonymous") },
            { "SystemBusinessObject.Logout", ("Public", "Authenticated") },
            { "SystemBusinessObject.Ping", ("Public", "Anonymous") },
            { "SystemBusinessObject.SaveCustomizePluginSettings", ("LocalOnly", "Authenticated") },
            { "SystemBusinessObject.SaveDefine", ("LocalOnly", "Authenticated") },
            { "SystemBusinessObject.SetApiKeyEnabled", ("Encrypted", "Authenticated") },
            { "SystemBusinessObject.SetApiKeyExpiry", ("Encrypted", "Authenticated") },
            { "SystemBusinessObject.SetDeploymentAdmin", ("LocalOnly", "Authenticated") },
        };

        /// <summary>
        /// Scans the actual access control declarations with the same resolution rules as <see cref="JsonRpcExecutor"/>.
        /// </summary>
        /// <remarks>
        /// Goes through <see cref="ApiAccessValidator.FindAccessControl"/> instead of reading the attribute directly:
        /// only that method has the method → base method → declaring type precedence, and it is what runs in production.
        /// </remarks>
        private static Dictionary<string, (string Protection, string Requirement)> Actual()
        {
            return typeof(BusinessObject).Assembly.GetTypes()
                .Where(t => typeof(BusinessObject).IsAssignableFrom(t))
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(m => !m.IsSpecialName)
                .Select(m => (Method: m, Attr: ApiAccessValidator.FindAccessControl(m)))
                .Where(x => x.Attr != null)
                .ToDictionary(
                    x => $"{x.Method.DeclaringType!.Name}.{x.Method.Name}",
                    x => (x.Attr!.ProtectionLevel.ToString(), x.Attr.AccessRequirement.ToString()),
                    StringComparer.Ordinal);
        }

        [Fact]
        [DisplayName("API method protection levels and access requirements cannot change unnoticed")]
        public void AccessControl_MatchesPinnedDeclarations()
        {
            var actual = Actual();

            // Finding no methods means the scan itself is broken (a type moved, the inheritance changed), not that
            // everything complies. Without this check, the comparison below would pass vacuously.
            Assert.NotEmpty(actual);

            var problems = new List<string>();

            foreach (var (name, expected) in s_expected)
            {
                if (!actual.TryGetValue(name, out var found))
                {
                    problems.Add($"{name}: pinned but not found (method removed or renamed? existing clients will get MethodNotFound)");
                    continue;
                }

                if (found != expected)
                {
                    problems.Add(
                        $"{name}: declaration changed from ({expected.Protection}, {expected.Requirement}) to " +
                        $"({found.Protection}, {found.Requirement})");
                }
            }

            foreach (var name in actual.Keys.Where(k => !s_expected.ContainsKey(k)))
            {
                var found = actual[name];
                problems.Add($"{name}: new API method ({found.Protection}, {found.Requirement}), not pinned yet");
            }

            Assert.True(problems.Count == 0,
                "API access control declarations do not match the pinned list:" + global::System.Environment.NewLine +
                string.Join(global::System.Environment.NewLine, problems) + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Raising the protection level or switching to Authenticated rejects existing clients on the spot, " +
                "including front ends that are not released with the framework. Update this list only after checking " +
                "the impact. A new method requires answering once: who should be allowed to call it?");
        }
    }
}
