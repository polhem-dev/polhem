using System.ComponentModel;

namespace Polhem.Base.UnitTests
{
    public class AssemblyLoaderFallbackTests
    {
        [Fact]
        [DisplayName("LoadAssembly with an unknown assembly name (no directory) takes the FileNotFoundException fallback and throws")]
        public void LoadAssembly_UnknownNameNoDirectory_FallbackThrowsException()
        {
            var exception = Record.Exception(() => AssemblyLoader.LoadAssembly("PolhemXyzNotExistFallback.dll"));
            Assert.NotNull(exception);
        }

        [Fact]
        [DisplayName("LoadAssembly with a nonexistent path including a directory uses the path as assemblyFile and throws")]
        public void LoadAssembly_PathWithDirectoryNotFound_FallbackThrowsException()
        {
            string fakeAssemblyPath = Path.Combine(
                Path.GetTempPath(), "polhem_fake_dir_xyz", "PolhemXyzNotExistFallback2.dll");
            var exception = Record.Exception(() => AssemblyLoader.LoadAssembly(fakeAssemblyPath));
            Assert.NotNull(exception);
        }
    }
}
