using System.ComponentModel;

namespace Polhem.Core.UnitTests
{
    public class AssemblyLoaderExtraTests
    {
        [Fact]
        [DisplayName("GetType supports separate assembly name and type name arguments")]
        public void GetType_WithSeparateAssemblyAndTypeName_ReturnsType()
        {
            var type = AssemblyLoader.GetType("Polhem.Core.UnitTests.dll", "Polhem.Core.UnitTests.AssemblyLoaderSample");
            Assert.Equal(typeof(AssemblyLoaderSample), type);
        }

        [Fact]
        [DisplayName("CreateInstance supports separate assembly name and type name arguments")]
        public void CreateInstance_SeparateAssemblyAndTypeName_ReturnsInstance()
        {
            var instance = AssemblyLoader.CreateInstance("Polhem.Core.UnitTests.dll", "Polhem.Core.UnitTests.AssemblyLoaderSample");
            Assert.IsType<AssemblyLoaderSample>(instance);
        }

        [Fact]
        [DisplayName("CreateInstance with separate names supports constructor arguments")]
        public void CreateInstance_SeparateParamsWithCtorArgs_UsesMatchingConstructor()
        {
            var instance = AssemblyLoader.CreateInstance(
                "Polhem.Core.UnitTests.dll", "Polhem.Core.UnitTests.AssemblyLoaderSample", "msg", true);
            var result = Assert.IsType<AssemblyLoaderSample>(instance);
            Assert.Equal("msg", result.Label);
            Assert.True(result.Flag);
        }
    }
}
