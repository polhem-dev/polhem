using System.ComponentModel;

namespace Polhem.Core.UnitTests
{
    /// <summary>
    /// Security tests for the SysInfo type name allowlist.
    /// </summary>
    [Collection(SysInfoStaticCollection.Name)]
    public class SysInfoSecurityTests
    {
        [Theory]
        [InlineData("Polhem.Core.SomeClass", true)]
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
        [InlineData("PolhemCore.SomeClass")]
        [InlineData("Polhem.Base.SomeClass")]
        [DisplayName("IsTypeNameAllowed rejects types outside the allowlist")]
        public void IsTypeNameAllowed_DisallowedTypes_ReturnsFalse(string typeName)
        {
            Assert.False(SysInfo.IsTypeNameAllowed(typeName));
        }

        [Fact]
        [DisplayName("IsTypeNameAllowed matches a prefix only at a namespace boundary")]
        public void IsTypeNameAllowed_PrefixMustMatchNamespaceBoundary()
        {
            // "Polhem.Core" is allowed, but "Polhem.CoreExtra" should NOT match
            Assert.True(SysInfo.IsTypeNameAllowed("Polhem.Core.SomeClass"));
            Assert.False(SysInfo.IsTypeNameAllowed("Polhem.CoreExtra.SomeClass"));
        }

        [Fact]
        [DisplayName("BuildAllowedTypeNamespaces without custom namespaces returns exactly the built-in defaults")]
        public void BuildAllowedTypeNamespaces_NoCustomNamespaces_ReturnsBuiltInDefaults()
        {
            var expected = new[] { "Polhem.Api.Contracts", "Polhem.Api.Core", "Polhem.Business", "Polhem.Core", "Polhem.Definition" };

            var actual = SysInfo.BuildAllowedTypeNamespaces(string.Empty);

            Assert.Equal(expected, actual.Order(StringComparer.Ordinal));
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
