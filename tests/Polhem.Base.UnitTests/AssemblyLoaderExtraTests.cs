using System.ComponentModel;
using Polhem.Base.Attributes;

namespace Polhem.Base.UnitTests
{
    public class AssemblyLoaderExtraTests
    {
        [Fact]
        [DisplayName("GetType supports separate assembly name and type name arguments")]
        public void GetType_WithSeparateAssemblyAndTypeName_ReturnsType()
        {
            var type = AssemblyLoader.GetType("Polhem.Base.dll", "Polhem.Base.Attributes.TreeNodeAttribute");
            Assert.Equal(typeof(TreeNodeAttribute), type);
        }

        [Fact]
        [DisplayName("CreateInstance supports separate assembly name and type name arguments")]
        public void CreateInstance_SeparateAssemblyAndTypeName_ReturnsInstance()
        {
            var instance = AssemblyLoader.CreateInstance("Polhem.Base.dll", "Polhem.Base.Attributes.TreeNodeAttribute");
            Assert.IsType<TreeNodeAttribute>(instance);
        }

        [Fact]
        [DisplayName("CreateInstance with separate names supports constructor arguments")]
        public void CreateInstance_SeparateParamsWithCtorArgs_UsesMatchingConstructor()
        {
            var instance = AssemblyLoader.CreateInstance(
                "Polhem.Base.dll", "Polhem.Base.Attributes.TreeNodeAttribute", "msg", true);
            var result = Assert.IsType<TreeNodeAttribute>(instance);
            Assert.Equal("msg", result.DisplayFormat);
            Assert.True(result.CollectionFolder);
        }
    }
}
