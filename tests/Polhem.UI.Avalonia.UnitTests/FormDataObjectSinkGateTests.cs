using System.ComponentModel;
using System.Reflection;
using Polhem.UI.Avalonia.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests
{
    /// <summary>
    /// <see cref="FormDataObject"/> must not carry its own copy of the value conversion rules that moved down to <c>Polhem.Api.Client</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These members used to have a verbatim copy in this head and in <c>Polhem.Web.Blazor.Server</c>, and the doc said "deliberately parallel,
    /// nothing enforces it". Then they really drifted: one side of <c>ConvertToColumnValue</c> fixed
    /// "do not write <c>DBNull</c> into a NOT NULL column" while the other kept running with the bug.
    /// The single implementation now lives in <c>Polhem.Api.Client.FormValueBinding</c> / <c>FormDataGuard</c>.
    /// </para>
    /// <para>
    /// NOTE: This gate catches **a copy pasted back under the original name**, which is the mistake that actually happened
    /// (hitting a bug in this head, not knowing about the shared implementation, and adding a private method on the spot).
    /// It does not catch a renamed copy, so do not treat it as full duplicate detection.
    /// </para>
    /// </remarks>
    public class FormDataObjectSinkGateTests
    {
        private static readonly string[] s_sunkMembers =
        [
            "RequireConnector",
            "RequireMasterRowId",
            "BuildEmptyDataSet",
            "FormatForBinding",
            "ConvertToColumnValue",
            "ResolveEmptyValueForType",
        ];

        [Fact]
        [DisplayName("FormDataObject does not redeclare the members moved down to Polhem.Api.Client")]
        public void FormDataObject_DoesNotRedeclareSunkMembers()
        {
            var declared = typeof(FormDataObject)
                .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(m => m.Name)
                .ToArray();

            // Control check: make sure the member list was really read, so an empty array cannot pass the assertion vacuously.
            Assert.Contains("GetField", declared, StringComparer.Ordinal);

            var redeclared = s_sunkMembers.Where(n => declared.Contains(n, StringComparer.Ordinal)).ToArray();

            Assert.True(
                redeclared.Length == 0,
                $"These members moved down to Polhem.Api.Client and must not be redeclared here: {string.Join(", ", redeclared)}");
        }
    }
}
