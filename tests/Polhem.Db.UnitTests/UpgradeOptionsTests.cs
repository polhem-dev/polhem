using System.ComponentModel;
using Polhem.Db.Schema;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    public class UpgradeOptionsTests : IClassFixture<SharedDbFixture>
    {
        public UpgradeOptionsTests(SharedDbFixture _) { }

        [Fact]
        [DisplayName("AllowColumnNarrowing defaults to false")]
        public void AllowColumnNarrowing_Default_IsFalse()
        {
            var options = new UpgradeOptions();

            Assert.False(options.AllowColumnNarrowing);
        }

        [Fact]
        [DisplayName("The static Default instance returns the default values")]
        public void Default_ReturnsInstanceWithDefaults()
        {
            var options = UpgradeOptions.Default;

            Assert.NotNull(options);
            Assert.False(options.AllowColumnNarrowing);
        }
    }
}
