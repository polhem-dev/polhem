using System.ComponentModel;

namespace Polhem.Base.UnitTests
{
    /// <summary>
    /// Security tests for the SysInfo type name allowlist.
    /// </summary>
    [Collection(SysInfoStaticCollection.Name)]
    public class SysInfoSecurityTests
    {
        [Theory]
        [InlineData("Polhem.Base.SomeClass", true)]
        [InlineData("Polhem.Definition.Collections.Parameter", true)]
        [InlineData("Polhem.Api.Contracts.MyDto", true)]
        [InlineData("System.Byte[]", true)]
        [DisplayName("IsTypeNameAllowed allows types in the allowlist")]
        public void IsTypeNameAllowed_AllowedTypes_ReturnsTrue(string typeName, bool expected)
        {
            Assert.Equal(expected, SysInfo.IsTypeNameAllowed(typeName));
        }

        [Theory]
        [InlineData("System.Diagnostics.Process")]
        [InlineData("System.IO.FileInfo")]
        [InlineData("System.Runtime.Serialization.Formatters.Binary.BinaryFormatter")]
        [InlineData("Evil.Namespace.Exploit")]
        [InlineData("Polhem")]
        [InlineData("PolhemBase.SomeClass")]
        [DisplayName("IsTypeNameAllowed rejects types outside the allowlist")]
        public void IsTypeNameAllowed_DisallowedTypes_ReturnsFalse(string typeName)
        {
            Assert.False(SysInfo.IsTypeNameAllowed(typeName));
        }

        [Fact]
        [DisplayName("IsTypeNameAllowed matches a prefix only at a namespace boundary")]
        public void IsTypeNameAllowed_PrefixMustMatchNamespaceBoundary()
        {
            // "Polhem.Base" is allowed, but "Polhem.BaseExtra" should NOT match
            Assert.True(SysInfo.IsTypeNameAllowed("Polhem.Base.SomeClass"));
            Assert.False(SysInfo.IsTypeNameAllowed("Polhem.BaseExtra.SomeClass"));
        }

        [Fact]
        [DisplayName("AllowedTypeNamespaces is read-only and cannot be replaced from outside")]
        public void AllowedTypeNamespaces_IsReadOnly()
        {
            var list = SysInfo.AllowedTypeNamespaces;
            Assert.NotNull(list);
            Assert.IsType<IReadOnlyList<string>>(list, exactMatch: false);
        }
    }
}
