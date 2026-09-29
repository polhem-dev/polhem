using System.ComponentModel;
using Polhem.Core.Attributes;

namespace Polhem.Core.UnitTests
{
    public class AssemblyLoaderExtraTests
    {
        [Fact]
        [DisplayName("GetType supports separate assembly name and type name arguments")]
        public void GetType_WithSeparateAssemblyAndTypeName_ReturnsType()
        {
            var type = AssemblyLoader.GetType("Polhem.Core.dll", "Polhem.Core.Attributes.TreeNodeAttribute");
            Assert.Equal(typeof(TreeNodeAttribute), type);
        }

        [Fact]
        [DisplayName("CreateInstance supports separate assembly name and type name arguments")]
        public void CreateInstance_SeparateAssemblyAndTypeName_ReturnsInstance()
        {
            var instance = AssemblyLoader.CreateInstance("Polhem.Core.dll", "Polhem.Core.Attributes.TreeNodeAttribute");
            Assert.IsType<TreeNodeAttribute>(instance);
        }

        [Fact]
        [DisplayName("CreateInstance with separate names supports constructor arguments")]
        public void CreateInstance_SeparateParamsWithCtorArgs_UsesMatchingConstructor()
        {
            var instance = AssemblyLoader.CreateInstance(
                "Polhem.Core.dll", "Polhem.Core.Attributes.TreeNodeAttribute", "msg", true);
            var result = Assert.IsType<TreeNodeAttribute>(instance);
            Assert.Equal("msg", result.DisplayFormat);
            Assert.True(result.CollectionFolder);
        }
    }
}
