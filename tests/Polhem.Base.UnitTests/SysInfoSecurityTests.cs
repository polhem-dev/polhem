using System.ComponentModel;

namespace Polhem.Base.UnitTests
{
    /// <summary>
    /// SysInfo 型別白名單安全性測試
    /// </summary>
    [Collection("SysInfoStatic")]
    public class SysInfoSecurityTests
    {
        [Theory]
        [InlineData("Polhem.Base.SomeClass", true)]
        [InlineData("Polhem.Definition.Collections.Parameter", true)]
        [InlineData("Polhem.Api.Contracts.MyDto", true)]
        [InlineData("System.Byte[]", true)]
        [DisplayName("IsTypeNameAllowed 應允許白名單內的型別")]
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
        [DisplayName("IsTypeNameAllowed 應拒絕白名單外的型別")]
        public void IsTypeNameAllowed_DisallowedTypes_ReturnsFalse(string typeName)
        {
            Assert.False(SysInfo.IsTypeNameAllowed(typeName));
        }

        [Fact]
        [DisplayName("IsTypeNameAllowed 前綴比對應精確匹配命名空間分隔符")]
        public void IsTypeNameAllowed_PrefixMustMatchNamespaceBoundary()
        {
            // "Polhem.Base" is allowed, but "Polhem.BaseExtra" should NOT match
            Assert.True(SysInfo.IsTypeNameAllowed("Polhem.Base.SomeClass"));
            Assert.False(SysInfo.IsTypeNameAllowed("Polhem.BaseExtra.SomeClass"));
        }

        [Fact]
        [DisplayName("AllowedTypeNamespaces 應為唯讀，不可被外部替換")]
        public void AllowedTypeNamespaces_IsReadOnly()
        {
            var list = SysInfo.AllowedTypeNamespaces;
            Assert.NotNull(list);
            Assert.IsType<IReadOnlyList<string>>(list, exactMatch: false);
        }
    }
}
