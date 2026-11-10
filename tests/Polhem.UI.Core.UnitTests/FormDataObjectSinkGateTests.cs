using System.ComponentModel;
using System.Reflection;
using Polhem.UI.Core.DataObjects;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// <see cref="FormDataObject"/> must not carry its own copy of the value conversion rules in
    /// <see cref="FormValueBinding"/> and <see cref="FormDataGuard"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These members used to have a verbatim copy in the Avalonia head and in the Blazor head, and the doc said
    /// "deliberately parallel, nothing enforces it". Then they really drifted: one side of <c>ConvertToColumnValue</c>
    /// fixed "do not write <c>DBNull</c> into a NOT NULL column" while the other kept running with the bug.
    /// </para>
    /// <para>
    /// NOTE: This gate catches **a copy pasted back under the original name**, which is the mistake that actually happened
    /// (hitting a bug, not knowing about the shared implementation, and adding a private method on the spot).
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
        [DisplayName("FormDataObject does not redeclare the value rules of FormValueBinding and FormDataGuard")]
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
                $"These members belong to FormValueBinding or FormDataGuard and must not be redeclared here: {string.Join(", ", redeclared)}");
        }
    }
}
