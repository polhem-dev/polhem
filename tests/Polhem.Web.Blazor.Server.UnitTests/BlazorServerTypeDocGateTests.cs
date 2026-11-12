using System.ComponentModel;
using Polhem.Tests.Shared;

namespace Polhem.Web.Blazor.Server.UnitTests
{
    /// <summary>
    /// Every public type of Polhem.Web.Blazor.Server, its Razor components included, has a type entry in the package's documentation
    /// file. See <see cref="TypeDocCoverage"/> for why the compiler does not check the components.
    /// </summary>
    public class BlazorServerTypeDocGateTests
    {
        [Fact]
        [DisplayName("Every public type of Polhem.Web.Blazor.Server, its Razor components included, has a summary in the documentation file")]
        public void PublicTypes_AllHaveTypeDocumentation()
        {
            var assembly = typeof(Polhem.Web.Blazor.Server.Components.PolhemAccessTokenProvider).Assembly;

            var undocumented = TypeDocCoverage.FindUndocumentedTypes(assembly, out var checkedTypes);

            Assert.Contains("T:Polhem.Web.Blazor.Server.Components.PolhemAccessTokenProvider", checkedTypes);
            Assert.True(undocumented.Count == 0, "Without a type summary: " + string.Join(", ", undocumented));
        }
    }
}
