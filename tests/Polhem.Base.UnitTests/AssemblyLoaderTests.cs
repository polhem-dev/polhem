using System.ComponentModel;
using Polhem.Base.Attributes;

namespace Polhem.Base.UnitTests
{
    public class AssemblyLoaderTests
    {
        private static readonly object[] s_treeNodeCtorArgs = { "ok", true };

        private const string BaseAssembly = "Polhem.Base.dll";

        [Fact]
        [DisplayName("FindAssembly finds an assembly already loaded in the AppDomain")]
        public void FindAssembly_AlreadyLoaded_ReturnsAssembly()
        {
            var assembly = AssemblyLoader.FindAssembly(BaseAssembly);
            Assert.NotNull(assembly);
            Assert.Equal("Polhem.Base", assembly!.GetName().Name);
        }

        [Fact]
        [DisplayName("Repeated FindAssembly calls hit the cache")]
        public void FindAssembly_RepeatedCalls_ReturnSameInstance()
        {
            var first = AssemblyLoader.FindAssembly(BaseAssembly);
            var second = AssemblyLoader.FindAssembly(BaseAssembly);

            Assert.NotNull(first);
            Assert.Same(first, second);
        }

        [Fact]
        [DisplayName("FindAssembly returns null for an unknown name")]
        public void FindAssembly_UnknownName_ReturnsNull()
        {
            Assert.Null(AssemblyLoader.FindAssembly("Does.Not.Exist.dll"));
        }

        [Fact]
        [DisplayName("LoadAssembly returns the cached instance for an already loaded assembly")]
        public void LoadAssembly_AlreadyLoaded_ReturnsCached()
        {
            var first = AssemblyLoader.LoadAssembly(BaseAssembly);
            var second = AssemblyLoader.LoadAssembly(BaseAssembly);

            Assert.NotNull(first);
            Assert.Same(first, second);
        }

        [Fact]
        [DisplayName("GetType supports the 'type, assembly' format")]
        public void GetType_WithAssemblyQualifiedName_ReturnsType()
        {
            var type = AssemblyLoader.GetType("Polhem.Base.Attributes.TreeNodeAttribute, Polhem.Base");
            Assert.Equal(typeof(TreeNodeAttribute), type);
        }

        [Fact]
        [DisplayName("GetType supports a bare full type name (the assembly is inferred from the namespace)")]
        public void GetType_WithFullTypeName_ReturnsType()
        {
            // The sample type must be in the root namespace `Polhem.Base`. The assembly name is inferred by dropping
            // the last segment, so `Polhem.Base.Attributes.X` would look for a nonexistent `Polhem.Base.Attributes.dll`.
            var type = AssemblyLoader.GetType("Polhem.Base.SysInfo");
            Assert.Equal(typeof(SysInfo), type);
        }

        [Fact]
        [DisplayName("CreateInstance creates a new object of the given type")]
        public void CreateInstance_ReturnsInstance()
        {
            var instance = AssemblyLoader.CreateInstance("Polhem.Base.Attributes.TreeNodeAttribute, Polhem.Base");
            Assert.IsType<TreeNodeAttribute>(instance);
        }

        [Fact]
        [DisplayName("CreateInstance supports constructor arguments")]
        public void CreateInstance_WithConstructorArgs_UsesMatchingConstructor()
        {
            // WARNING: The arguments must be passed explicitly as an `object[]`. Written as
            // `CreateInstance(aqn, "ok", true)`, the second argument is a string, so C# binds to the
            // `CreateInstance(assemblyName, typeName, params args)` overload, treats the AQN as an assembly name and
            // throws `FileLoadException`. The original test only avoided this because its first constructor argument
            // happened to be a bool.
            var instance = AssemblyLoader.CreateInstance(
                "Polhem.Base.Attributes.TreeNodeAttribute, Polhem.Base", s_treeNodeCtorArgs);
            var result = Assert.IsType<TreeNodeAttribute>(instance);
            Assert.Equal("ok", result.DisplayFormat);
            Assert.True(result.CollectionFolder);
        }
    }
}
