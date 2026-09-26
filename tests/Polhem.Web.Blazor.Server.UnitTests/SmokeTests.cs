using System.ComponentModel;

namespace Polhem.Web.Blazor.Server.UnitTests
{
    /// <summary>
    /// Smoke test from the scaffolding stage: confirms the project compiles and the assembly loads.
    /// </summary>
    public class SmokeTests
    {
        [Fact]
        [DisplayName("The assembly loads (scaffolding-stage smoke test)")]
        public void AssemblyLoads()
        {
            var assembly = typeof(SmokeTests).Assembly;
            Assert.NotNull(assembly);
        }
    }
}
