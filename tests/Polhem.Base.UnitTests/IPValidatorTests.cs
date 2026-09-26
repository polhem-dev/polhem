using System.ComponentModel;

namespace Polhem.Base.UnitTests
{
    public class IPValidatorTests
    {
        [Fact]
        [DisplayName("IsIpAllowed allows an IP on the whitelist and rejects one on the blacklist")]
        public void IsIpAllowed_WhitelistAndBlacklist_ReturnsExpectedResult()
        {
            var whitelist = new System.Collections.Generic.List<string>
            {
                "192.168.1.*",
                "10.0.*.*",
                "192.168.2.0/24"
            };

            var blacklist = new System.Collections.Generic.List<string>
            {
                "192.168.1.100",
                "10.0.0.5",
                "192.168.3.0/24"
            };

            var validator = new IPValidator(whitelist, blacklist);

            var allowed = validator.IsIpAllowed("192.168.2.50");
            Assert.True(allowed);
            var allowed2 = validator.IsIpAllowed("10.0.0.5");
            Assert.False(allowed2);
        }
    }
}
