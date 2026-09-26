using System.ComponentModel;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// <see cref="VersionInfo"/> only reads entry assembly metadata. These smoke tests make sure it does not throw.
    /// </summary>
    public class VersionInfoTests
    {
        [Fact]
        [DisplayName("VersionInfo.Product returns a non-empty string")]
        public void Product_ReturnsNonEmptyString()
        {
            Assert.False(string.IsNullOrEmpty(VersionInfo.Product));
        }

        [Fact]
        [DisplayName("VersionInfo.Version returns a non-empty string")]
        public void Version_ReturnsNonEmptyString()
        {
            Assert.False(string.IsNullOrEmpty(VersionInfo.Version));
        }

        [Fact]
        [DisplayName("VersionInfo.AssemblyVersion returns a non-empty string")]
        public void AssemblyVersion_ReturnsNonEmptyString()
        {
            Assert.False(string.IsNullOrEmpty(VersionInfo.AssemblyVersion));
        }
    }
}
