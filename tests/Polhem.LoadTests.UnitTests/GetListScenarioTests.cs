using System.ComponentModel;
using Polhem.LoadTests.Configuration;
using Polhem.LoadTests.Scenarios;

namespace Polhem.LoadTests.UnitTests
{
    /// <summary>
    /// Tests for <see cref="GetListScenario"/>'s configuration handling.
    /// </summary>
    public class GetListScenarioTests
    {
        private static VirtualUserPool Pool() => new(new AuthOptions());

        [Fact]
        [DisplayName("GetListScenario name matches the key in the configuration file")]
        public void Name_MatchesConfigurationKey()
        {
            Assert.Equal("GetList", new GetListScenario(Pool(), "GetList", "Customer", 50, 1).Name);
        }

        [Fact]
        [DisplayName("GetListScenario constructor accepts an empty progId (falls back to the default instead of sending an empty one)")]
        public void Constructor_EmptyProgId_FallsBackToDefault()
        {
            // Constructing must not throw on an unset progId; the default keeps the run usable
            // when the configuration omits it.
            var scenario = new GetListScenario(Pool(), "GetList", string.Empty, 50, 1);

            Assert.Equal("GetList", scenario.Name);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [DisplayName("GetListScenario constructor accepts a non-positive pageSize (falls back to the default to avoid an unpaged query)")]
        public void Constructor_NonPositivePageSize_FallsBackToDefault(int pageSize)
        {
            // A page size of zero must not reach the server: an unpaged list query materialises
            // the whole table on both ends, which measures something else entirely.
            var scenario = new GetListScenario(Pool(), "GetList", "Customer", pageSize, 1);

            Assert.Equal("GetList", scenario.Name);
        }

        [Fact]
        [DisplayName("GetListScenario uses a custom name, so shallow and deep paging get separate rows in one run")]
        public void Constructor_CustomName_IsUsedAsScenarioName()
        {
            // Two instances sharing a name would have their samples merged into one row, which
            // is exactly what a shallow-vs-deep comparison must avoid.
            Assert.Equal("GetListDeep",
                new GetListScenario(Pool(), "GetListDeep", "Customer", 50, 1900).Name);
        }

        [Fact]
        [DisplayName("GetListScenario falls back to the default name when none is given")]
        public void Constructor_EmptyName_FallsBackToDefault()
        {
            Assert.Equal("GetList",
                new GetListScenario(Pool(), string.Empty, "Customer", 50, 1).Name);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [DisplayName("GetListScenario constructor accepts a non-positive startPage (falls back to the first page)")]
        public void Constructor_NonPositiveStartPage_FallsBackToFirstPage(int startPage)
        {
            // Page numbering is one-based; a zero or negative offset would be rejected by the
            // server rather than quietly measuring something else.
            var scenario = new GetListScenario(Pool(), "GetList", "Customer", 50, startPage);

            Assert.Equal("GetList", scenario.Name);
        }

        [Fact]
        [DisplayName("GetListScenario constructor throws ArgumentNullException for a null pool")]
        public void Constructor_NullPool_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new GetListScenario(null!, "GetList", "Customer", 50, 1));
        }
    }
}
