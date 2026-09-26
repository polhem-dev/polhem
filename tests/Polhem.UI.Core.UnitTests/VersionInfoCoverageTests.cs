using System.ComponentModel;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// Covers the getter of each public property of <see cref="VersionInfo"/> and its fallback branch.
    /// These properties read entry assembly metadata and cannot be changed at runtime, so the assertions are "does not throw and returns a sensible shape".
    /// </summary>
    public class VersionInfoCoverageTests
    {
        [Fact]
        [DisplayName("VersionInfo.Product getter does not throw and returns non-null")]
        public void Product_Getter_ReturnsNonNull()
        {
            var value = VersionInfo.Product;

            Assert.NotNull(value);
        }

        [Fact]
        [DisplayName("VersionInfo.Company getter does not throw and returns non-null")]
        public void Company_Getter_ReturnsNonNull()
        {
            var value = VersionInfo.Company;

            Assert.NotNull(value);
        }

        [Fact]
        [DisplayName("VersionInfo.Description getter does not throw and returns non-null")]
        public void Description_Getter_ReturnsNonNull()
        {
            var value = VersionInfo.Description;

            Assert.NotNull(value);
        }

        [Fact]
        [DisplayName("VersionInfo.Version getter returns the version string with the Git hash stripped")]
        public void Version_Getter_HasNoGitHashSuffix()
        {
            var value = VersionInfo.Version;

            Assert.NotNull(value);
            Assert.DoesNotContain("+", value);
        }

        [Fact]
        [DisplayName("VersionInfo.FileVersion getter does not throw and returns non-null")]
        public void FileVersion_Getter_ReturnsNonNull()
        {
            var value = VersionInfo.FileVersion;

            Assert.NotNull(value);
        }

        [Fact]
        [DisplayName("VersionInfo.AssemblyVersion getter does not throw and returns non-null")]
        public void AssemblyVersion_Getter_ReturnsNonNull()
        {
            var value = VersionInfo.AssemblyVersion;

            Assert.NotNull(value);
        }

        [Fact]
        [DisplayName("VersionInfo.FullInformationalVersion getter does not throw and returns non-null")]
        public void FullInformationalVersion_Getter_ReturnsNonNull()
        {
            var value = VersionInfo.FullInformationalVersion;

            Assert.NotNull(value);
        }

        [Fact]
        [DisplayName("VersionInfo reads every public property in a row without throwing")]
        public void AllProperties_SequentialRead_DoesNotThrow()
        {
            var exception = Record.Exception(() =>
            {
                _ = VersionInfo.Product;
                _ = VersionInfo.Company;
                _ = VersionInfo.Description;
                _ = VersionInfo.Version;
                _ = VersionInfo.FileVersion;
                _ = VersionInfo.AssemblyVersion;
                _ = VersionInfo.FullInformationalVersion;
            });

            Assert.Null(exception);
        }
    }
}
