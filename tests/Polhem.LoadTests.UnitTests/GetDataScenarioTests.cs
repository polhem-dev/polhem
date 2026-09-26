using System.ComponentModel;
using Polhem.LoadTests.Configuration;
using Polhem.LoadTests.Scenarios;

namespace Polhem.LoadTests.UnitTests
{
    /// <summary>
    /// Tests for <see cref="GetDataScenario"/>.
    /// </summary>
    public class GetDataScenarioTests
    {
        private static VirtualUserPool Pool() => new(new AuthOptions());

        [Fact]
        [DisplayName("GetDataScenario name matches the key in the configuration file")]
        public void Name_MatchesConfigurationKey()
        {
            Assert.Equal("GetData", new GetDataScenario(Pool(), "Customer", [Guid.NewGuid()]).Name);
        }

        [Fact]
        [DisplayName("GetDataScenario constructor throws when there are no keys, with a message pointing to the remedy")]
        public void Constructor_NoRowIds_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => new GetDataScenario(Pool(), "Customer", []));

            Assert.Contains("prepare", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("GetDataScenario constructor accepts an empty progId (falls back to the default)")]
        public void Constructor_EmptyProgId_FallsBackToDefault()
        {
            var scenario = new GetDataScenario(Pool(), string.Empty, [Guid.NewGuid()]);

            Assert.Equal("GetData", scenario.Name);
        }
    }
}
