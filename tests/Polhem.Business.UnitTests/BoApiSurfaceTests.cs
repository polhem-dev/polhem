using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;
using Polhem.Definition.Attributes;
using Polhem.Definition.Security;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Reflective audit of the BO public API surface. Locks the set of
    /// <c>[ApiAccessControl]</c>-decorated public methods on
    /// <see cref="BusinessObject"/> and its derivatives against a hard-coded
    /// baseline so additions / removals / access-level changes always require
    /// an intentional baseline update.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Whenever this test fails:
    /// </para>
    /// <list type="number">
    /// <item><description>Decide whether the change is intentional. Renames /
    ///   removals / access tightening or loosening must all be reviewed at the
    ///   security level.</description></item>
    /// <item><description>Update the <see cref="s_expectedSurface"/> baseline
    ///   below to match the new API surface.</description></item>
    /// <item><description>Update <c>docs/en/api-method-reference.md</c>
    ///   (and the zh-TW counterpart) so the human-facing reference does not
    ///   drift from the code.</description></item>
    /// </list>
    /// <para>
    /// The <c>polhem-add-bo-method</c> skill checklist references this test —
    /// adding a new BO method without updating the baseline will fail CI.
    /// </para>
    /// </remarks>
    public partial class BoApiSurfaceTests
    {
        /// <summary>
        /// Canonical list of every public API method currently exposed by
        /// <c>Polhem.Business</c>, identified by <c>{DeclaringType.Name}.{MethodName}</c>.
        /// Sorted alphabetically for stable diffs.
        /// </summary>
        private static readonly IReadOnlyList<ApiSurfaceEntry> s_expectedSurface = new[]
        {
            // Audit-log axis — AuditLogBusinessObject (read-only queries over st_log_*).
            new ApiSurfaceEntry("AuditLogBusinessObject", "GetAccessLog",        ApiProtectionLevel.Encrypted, ApiAccessRequirement.Authenticated),
            new ApiSurfaceEntry("AuditLogBusinessObject", "GetApiAnomalyLog",    ApiProtectionLevel.Encrypted, ApiAccessRequirement.Authenticated),
            new ApiSurfaceEntry("AuditLogBusinessObject", "GetApiAnomalySummary",ApiProtectionLevel.Encrypted, ApiAccessRequirement.Authenticated),
            new ApiSurfaceEntry("AuditLogBusinessObject", "GetChangeDetail",     ApiProtectionLevel.Encrypted, ApiAccessRequirement.Authenticated),
            new ApiSurfaceEntry("AuditLogBusinessObject", "GetChangeLog",        ApiProtectionLevel.Encrypted, ApiAccessRequirement.Authenticated),
            new ApiSurfaceEntry("AuditLogBusinessObject", "GetDbAnomalyLog",     ApiProtectionLevel.Encrypted, ApiAccessRequirement.Authenticated),
            new ApiSurfaceEntry("AuditLogBusinessObject", "GetDbAnomalySummary", ApiProtectionLevel.Encrypted, ApiAccessRequirement.Authenticated),
            new ApiSurfaceEntry("AuditLogBusinessObject", "GetLoginLog",         ApiProtectionLevel.Encrypted, ApiAccessRequirement.Authenticated),
            new ApiSurfaceEntry("AuditLogBusinessObject", "GetTopApiMethods",    ApiProtectionLevel.Encrypted, ApiAccessRequirement.Authenticated),

            // Base axis — defined on BusinessObject, inherited by every BO.
            new ApiSurfaceEntry("BusinessObject", "ExecFunc",          ApiProtectionLevel.Public, ApiAccessRequirement.Authenticated, ApiReplayProtection.UniqueSequence),
            new ApiSurfaceEntry("BusinessObject", "ExecFuncAnonymous", ApiProtectionLevel.Public, ApiAccessRequirement.Anonymous),

            // Form axis — FormBusinessObject (FormSchema-driven CRUD).
            new ApiSurfaceEntry("FormBusinessObject", "Delete",     ApiProtectionLevel.Public, ApiAccessRequirement.Authenticated, ApiReplayProtection.UniqueSequence),
            new ApiSurfaceEntry("FormBusinessObject", "GetData",    ApiProtectionLevel.Public, ApiAccessRequirement.Authenticated),
            new ApiSurfaceEntry("FormBusinessObject", "GetList",    ApiProtectionLevel.Public, ApiAccessRequirement.Authenticated),
            new ApiSurfaceEntry("FormBusinessObject", "GetLookup",  ApiProtectionLevel.Public, ApiAccessRequirement.Authenticated),
            new ApiSurfaceEntry("FormBusinessObject", "GetNewData", ApiProtectionLevel.Public, ApiAccessRequirement.Authenticated),
            new ApiSurfaceEntry("FormBusinessObject", "Save",       ApiProtectionLevel.Public, ApiAccessRequirement.Authenticated, ApiReplayProtection.UniqueSequence),

            // System axis — SystemBusinessObject (system-level operations).
            // Encrypted (formerly LocalOnly): the gate is handed to `IDeploymentAuthorizationService`. A remote caller must be a
            // deployment-level administrator; being authenticated is not enough. Local calls need no administrator, which keeps the bootstrap path for the first key.
            new ApiSurfaceEntry("SystemBusinessObject", "CreateApiKey",           ApiProtectionLevel.Encrypted, ApiAccessRequirement.Authenticated, ApiReplayProtection.UniqueSequence),
            // LocalOnly: it issues a token directly from a UserId without checking credentials, so it is a trusted-caller operation.
            // It used to be Public + Anonymous, and was only unexploited because `SessionInfoCache.CreateInstance` was not yet implemented.
            new ApiSurfaceEntry("SystemBusinessObject", "CreateSession",          ApiProtectionLevel.LocalOnly, ApiAccessRequirement.Anonymous),
            new ApiSurfaceEntry("SystemBusinessObject", "EnterCompany",           ApiProtectionLevel.Public,  ApiAccessRequirement.Authenticated, ApiReplayProtection.UniqueSequence),
            new ApiSurfaceEntry("SystemBusinessObject", "GetCommonConfiguration", ApiProtectionLevel.Public,  ApiAccessRequirement.Anonymous),
            new ApiSurfaceEntry("SystemBusinessObject", "GetCustomizeFormLayout", ApiProtectionLevel.Public,  ApiAccessRequirement.Authenticated),
            new ApiSurfaceEntry("SystemBusinessObject", "GetCustomizeLanguage",   ApiProtectionLevel.Public,  ApiAccessRequirement.Authenticated),
            new ApiSurfaceEntry("SystemBusinessObject", "GetCustomizePluginSettings", ApiProtectionLevel.LocalOnly, ApiAccessRequirement.Authenticated),
            new ApiSurfaceEntry("SystemBusinessObject", "GetDefine",              ApiProtectionLevel.Public,  ApiAccessRequirement.Authenticated),
            new ApiSurfaceEntry("SystemBusinessObject", "GetDepartmentTree",      ApiProtectionLevel.Public,  ApiAccessRequirement.Authenticated),
            new ApiSurfaceEntry("SystemBusinessObject", "GetFormLayout",          ApiProtectionLevel.Public,  ApiAccessRequirement.Authenticated),
            new ApiSurfaceEntry("SystemBusinessObject", "GetFormSchema",          ApiProtectionLevel.Public,  ApiAccessRequirement.Authenticated),
            new ApiSurfaceEntry("SystemBusinessObject", "GetLanguage",            ApiProtectionLevel.Public,  ApiAccessRequirement.Authenticated),
            new ApiSurfaceEntry("SystemBusinessObject", "LeaveCompany",           ApiProtectionLevel.Public,  ApiAccessRequirement.Authenticated, ApiReplayProtection.UniqueSequence),
            // `ListApiKeys`, `SetApiKeyEnabled` and `SetApiKeyExpiry` share the gate of `CreateApiKey`: keys belong to the whole deployment, a remote caller
            // must be a deployment-level administrator, and local calls pass through to keep the bootstrap. `ListApiKeys` returns no hashes.
            new ApiSurfaceEntry("SystemBusinessObject", "ListApiKeys",           ApiProtectionLevel.Encrypted, ApiAccessRequirement.Authenticated),
            new ApiSurfaceEntry("SystemBusinessObject", "Login",                  ApiProtectionLevel.Public,  ApiAccessRequirement.Anonymous),
            new ApiSurfaceEntry("SystemBusinessObject", "Logout",                 ApiProtectionLevel.Public,  ApiAccessRequirement.Authenticated),
            new ApiSurfaceEntry("SystemBusinessObject", "Ping",                   ApiProtectionLevel.Public,  ApiAccessRequirement.Anonymous),
            // LocalOnly for `SaveCustomizePluginSettings` and `SaveDefine`: writing definitions is a deployment-time operation. `SaveDefine` used to block only SystemSettings / DatabaseSettings,
            // so any authenticated account could overwrite the other definition types (including PermissionModels, DbCategorySettings and FormSchema).
            new ApiSurfaceEntry("SystemBusinessObject", "SaveCustomizePluginSettings", ApiProtectionLevel.LocalOnly, ApiAccessRequirement.Authenticated),
            new ApiSurfaceEntry("SystemBusinessObject", "SaveDefine",             ApiProtectionLevel.LocalOnly, ApiAccessRequirement.Authenticated),
            new ApiSurfaceEntry("SystemBusinessObject", "SetApiKeyEnabled",      ApiProtectionLevel.Encrypted, ApiAccessRequirement.Authenticated, ApiReplayProtection.UniqueSequence),
            new ApiSurfaceEntry("SystemBusinessObject", "SetApiKeyExpiry",       ApiProtectionLevel.Encrypted, ApiAccessRequirement.Authenticated, ApiReplayProtection.UniqueSequence),
            // LocalOnly: appointing a deployment-level administrator is a privilege change and a deployment-time operation. For the same reason as SaveDefine / CreateApiKey,
            // a remote account that is merely authenticated must not be able to promote itself or others to administrator.
            new ApiSurfaceEntry("SystemBusinessObject", "SetDeploymentAdmin",     ApiProtectionLevel.LocalOnly, ApiAccessRequirement.Authenticated),
        };

        [Fact]
        [DisplayName("The BO API public surface matches the baseline, which is checked against docs/{lang}/api-method-reference.md")]
        public void PublicApiSurface_MatchesBaseline()
        {
            var actual = ScanBusinessAssembly();

            string expectedDump = FormatSurface(s_expectedSurface);
            string actualDump = FormatSurface(actual);

            // Equality on the formatted dumps gives a clear diff in the xUnit
            // failure message — much easier to read than collection asserts.
            Assert.Equal(expectedDump, actualDump);
        }

        /// <summary>
        /// Every baseline row must appear in the bilingual <c>docs/{lang}/api-method-reference.md</c>, and vice versa.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The DisplayName of this test and the opening of those two documents both said they were kept in sync by this test or the build would fail,
        /// yet before 2026-09-04 <b>this file did not read any file at all</b>: the sync relied on discipline, not a mechanism.
        /// That sentence was a guarantee in a public document, so either the mechanism had to be added or the sentence changed; this adds the mechanism.
        /// </para>
        /// <para>
        /// It compares <c>(method, Protection, Auth)</c> triples rather than whole lines: the document is split into sections by axis
        /// and carries a Purpose column and prose, so a line-by-line comparison would break on unrelated layout changes. Method names are
        /// unique in both the baseline and the documents (if a duplicate name ever distorts this test, that itself should be visible).
        /// </para>
        /// </remarks>
        [Theory]
        [InlineData("en")]
        [InlineData("zh-TW")]
        [DisplayName("The BO API baseline matches docs/{lang}/api-method-reference.md entry by entry")]
        public void Baseline_MatchesPublicMethodReference(string lang)
        {
            string path = Path.Combine(RepoRoot.Find(), "docs", lang, "api-method-reference.md");
            Assert.True(File.Exists(path), $"Cannot find {path}.");

            var documented = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in DocRowPattern().Matches(File.ReadAllText(path)))
                documented.Add($"{m.Groups[1].Value} | {m.Groups[2].Value} | {m.Groups[3].Value}");

            // Guards against a vacuous pass: if the regex does not match the document format, the set equality below compares two empty sets and is always green.
            Assert.NotEmpty(documented);

            var expected = new HashSet<string>(
                s_expectedSurface.Select(e => $"{e.Method} | {e.ProtectionLevel} | {e.AccessRequirement}"),
                StringComparer.Ordinal);

            var missing = expected.Except(documented).OrderBy(x => x, StringComparer.Ordinal).ToList();
            var extra = documented.Except(expected).OrderBy(x => x, StringComparer.Ordinal).ToList();

            Assert.True(
                missing.Count == 0 && extra.Count == 0,
                $"docs/{lang}/api-method-reference.md is out of sync with the baseline.\nMissing from the document:\n  {string.Join("\n  ", missing)}\n" +
                $"Extra in the document (or column values differ):\n  {string.Join("\n  ", extra)}");
        }

        /// <summary>
        /// The methods that declare replay protection must match the Replay protection list in the bilingual documents.
        /// </summary>
        /// <remarks>
        /// This column is the third access dimension, added in 4.26.0, and it is a behavioral contract for client authors (without an increasing sequence number the call gets
        /// <c>-32005 ReplayRejected</c>). The triple comparison above deliberately leaves it out: the document has it as a list rather than a table column,
        /// and forcing it into the table would add a <c>None</c> to every row.
        /// </remarks>
        [Theory]
        [InlineData("en")]
        [InlineData("zh-TW")]
        [DisplayName("The methods that declare replay protection match the list in docs/{lang}/api-method-reference.md")]
        public void ReplayProtectedMethods_MatchPublicMethodReference(string lang)
        {
            string text = File.ReadAllText(Path.Combine(RepoRoot.Find(), "docs", lang, "api-method-reference.md"));

            var expected = s_expectedSurface
                .Where(e => e.ReplayProtection == ApiReplayProtection.UniqueSequence)
                .Select(e => e.Method)
                .OrderBy(m => m, StringComparer.Ordinal)
                .ToList();

            // Guards against a vacuous pass: if the baseline marks nothing, the every-method-is-found check below is always true.
            Assert.NotEmpty(expected);

            var documented = ReplayListPattern().Matches(text)
                .Select(m => m.Groups[1].Value)
                .OrderBy(m => m, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(expected, documented);
        }

        /// <summary>
        /// The method names in the Replay protection section.
        /// </summary>
        [GeneratedRegex(@"^- `(\w+)`\s*$", RegexOptions.Multiline)]
        private static partial Regex ReplayListPattern();

        /// <summary>
        /// One row of the document table: <c>| `Method` | Protection | Auth | Purpose |</c>.
        /// </summary>
        [GeneratedRegex(@"^\|\s*`(\w+)`\s*\|\s*(\w+)\s*\|\s*(\w+)\s*\|", RegexOptions.Multiline)]
        private static partial Regex DocRowPattern();

        /// <summary>
        /// Reflects over the <c>Polhem.Business</c> assembly and collects every
        /// public method decorated with <see cref="ApiAccessControlAttribute"/>.
        /// </summary>
        private static List<ApiSurfaceEntry> ScanBusinessAssembly()
        {
            var assembly = typeof(BusinessObject).Assembly;
            var entries = new List<ApiSurfaceEntry>();

            foreach (var type in assembly.GetTypes())
            {
                // Skip abstract base helpers / nested compiler-generated types — only
                // concrete BO surfaces ship API methods.
                if (!type.IsPublic || type.IsAbstract && type.IsSealed)
                    continue;

                // DeclaredOnly: skip inherited methods so an attribute on the base
                // (e.g. BusinessObject.ExecFunc) shows up exactly once, on BusinessObject.
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    var attr = method.GetCustomAttribute<ApiAccessControlAttribute>(inherit: false);
                    if (attr is null)
                        continue;
                    entries.Add(new ApiSurfaceEntry(
                        type.Name, method.Name, attr.ProtectionLevel, attr.AccessRequirement, attr.ReplayProtection));
                }
            }

            // Stable sort for deterministic dump output.
            entries.Sort((a, b) =>
            {
                int byType = string.CompareOrdinal(a.Type, b.Type);
                return byType != 0 ? byType : string.CompareOrdinal(a.Method, b.Method);
            });
            return entries;
        }

        private static string FormatSurface(IEnumerable<ApiSurfaceEntry> entries)
        {
            return string.Join('\n', entries.Select(e =>
                $"{e.Type}.{e.Method} | {e.ProtectionLevel} | {e.AccessRequirement} | {e.ReplayProtection}"));
        }

        /// <summary>
        /// One baseline row. <paramref name="ReplayProtection"/> has a default only so the baseline does not have to
        /// write <c>None</c> on every row. It does not hide any change: the actual value always comes from the reflection scan,
        /// so when the source removes <c>UniqueSequence</c>, the scan result no longer matches the value written here.
        /// </summary>
        private readonly record struct ApiSurfaceEntry(
            string Type,
            string Method,
            ApiProtectionLevel ProtectionLevel,
            ApiAccessRequirement AccessRequirement,
            ApiReplayProtection ReplayProtection = ApiReplayProtection.None);
    }
}
