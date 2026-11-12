using System.ComponentModel;
using Polhem.Tests.Shared;

namespace Polhem.Web.Blazor.Fluent.UnitTests
{
    /// <summary>
    /// Every public type in the Polhem.Web.Blazor.Fluent namespace, its Razor components included, has a type entry in
    /// the package's documentation file. See <see cref="TypeDocCoverage"/> for why the compiler does not check the
    /// components.
    /// </summary>
    public class FluentTypeDocGateTests
    {
        [Fact]
        [DisplayName("Every public type in the Polhem.Web.Blazor.Fluent namespace, its Razor components included, has a summary in the documentation file")]
        public void PublicTypes_AllHaveTypeDocumentation()
        {
            var assembly = typeof(Polhem.Web.Blazor.Fluent.Controls.GridControl).Assembly;

            var undocumented = TypeDocCoverage.FindUndocumentedTypes(assembly, out var checkedTypes);

            Assert.Contains("T:Polhem.Web.Blazor.Fluent.Controls.GridControl", checkedTypes);
            Assert.True(undocumented.Count == 0, "Without a type summary: " + string.Join(", ", undocumented));
        }
    }
}
