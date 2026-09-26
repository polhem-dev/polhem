using System.ComponentModel;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Settings
{
    public class CacheNotifyOptionsTests
    {
        [Fact]
        [DisplayName("CacheNotifyOptions defaults match the specification (Enabled=true, IntervalSeconds=5, MarginSeconds=5, DatabaseId=common)")]
        public void DefaultValues_MatchExpectedDefaults()
        {
            var options = new CacheNotifyOptions();
            Assert.True(options.Enabled);
            Assert.Equal(5, options.IntervalSeconds);
            Assert.Equal(5, options.MarginSeconds);
            Assert.Equal("common", options.DatabaseId);
        }

        [Fact]
        [DisplayName("ToString returns the type name CacheNotifyOptions")]
        public void ToString_ReturnsTypeName()
        {
            var options = new CacheNotifyOptions();
            Assert.Equal("CacheNotifyOptions", options.ToString());
        }
    }
}
