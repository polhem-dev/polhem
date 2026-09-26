using System.ComponentModel;
using Polhem.Definition.Settings;
using Polhem.Definition.Security;

namespace Polhem.Definition.UnitTests.Settings
{
    /// <summary>
    /// Tests for MasterKeySource.
    /// </summary>
    public class MasterKeySourceTests
    {
        [Fact]
        [DisplayName("The default constructor initializes Type=Environment and an empty Value")]
        public void DefaultConstructor_InitializesDefaults()
        {
            var source = new MasterKeySource();

            Assert.Equal(MasterKeySourceType.Environment, source.Type);
            Assert.Equal(string.Empty, source.Value);
        }

        [Fact]
        [DisplayName("Properties can be set and read back")]
        public void Properties_AreSettable()
        {
            var source = new MasterKeySource
            {
                Type = MasterKeySourceType.Environment,
                Value = "POLHEM_MASTER_KEY"
            };

            Assert.Equal(MasterKeySourceType.Environment, source.Type);
            Assert.Equal("POLHEM_MASTER_KEY", source.Value);
        }

        [Theory]
        [InlineData(MasterKeySourceType.File, "File")]
        [InlineData(MasterKeySourceType.Environment, "Environment")]
        [DisplayName("ToString returns the string form of Type")]
        public void ToString_ReturnsTypeName(MasterKeySourceType type, string expected)
        {
            var source = new MasterKeySource { Type = type };

            Assert.Equal(expected, source.ToString());
        }
    }
}
