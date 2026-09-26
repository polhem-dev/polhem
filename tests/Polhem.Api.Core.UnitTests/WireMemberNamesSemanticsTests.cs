using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Deciding whether a member is on the wire must read <see cref="JsonIgnoreAttribute.Condition"/>, not only
    /// whether the attribute is present.
    /// </summary>
    /// <remarks>
    /// <c>[JsonIgnore(Condition = JsonIgnoreCondition.Never)]</c> means **never ignore**. Checking only for presence
    /// judges it "ignored", the exact opposite, so the member drops out of the wire closure and a missing formatter
    /// registration escapes the drift gate.
    /// <para>
    /// The same kind of bug was why POLHEM4007 was removed (the 2026-07-30 commit message states that the rule only
    /// checked that the attribute existed without reading Condition, an implementation bug), and then the same bug
    /// lived on in <c>WireContractDriftTests</c>.
    /// </para>
    /// </remarks>
    public class WireMemberNamesSemanticsTests
    {
        private sealed class Sample
        {
            /// <summary>An ordinary member: counts as a wire member.</summary>
            public string Plain { get; set; } = string.Empty;

            /// <summary>Never ignored: semantically **is** a wire member.</summary>
            [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
            public string NeverIgnored { get; set; } = string.Empty;

            /// <summary>Always ignored: not a wire member.</summary>
            [JsonIgnore]
            public string AlwaysIgnored { get; set; } = string.Empty;
        }

        [Fact]
        [DisplayName("A member with Condition = Never counts as a wire member, and an always-ignored one does not")]
        public void WireMemberNames_ReadsTheIgnoreCondition()
        {
            var method = typeof(WireContractDriftTests).GetMethod(
                "WireMemberNames", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            var names = (List<string>)method!.Invoke(null, [typeof(Sample)])!;

            Assert.Contains("Plain", names);
            Assert.Contains("NeverIgnored", names);
            Assert.DoesNotContain("AlwaysIgnored", names);
        }
    }
}
