using System.ComponentModel;
using Polhem.Hosting.Database;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// The default folder <see cref="DataDirectory"/> gives a definition folder. <see cref="DataDirectory.EnsureDefault"/>
    /// writes the process-wide application domain value, so it is exercised end to end by the polhem-db tests, which run
    /// the tool in a child process, rather than here.
    /// </summary>
    public class DataDirectoryTests
    {
        [Fact]
        [DisplayName("DefaultFor returns the folder that holds the definition folder, with or without a trailing separator")]
        public void DefaultFor_DefinitionFolder_ReturnsParent()
        {
            var root = Path.Combine(Path.GetTempPath(), "app");
            var define = Path.Combine(root, "Define");

            Assert.Equal(root, DataDirectory.DefaultFor(define));
            Assert.Equal(root, DataDirectory.DefaultFor(define + Path.DirectorySeparatorChar));
        }

        [Fact]
        [DisplayName("DefaultFor resolves a relative definition folder against the working directory")]
        public void DefaultFor_RelativeFolder_ReturnsFullPath()
        {
            Assert.Equal(Path.GetFullPath("."), DataDirectory.DefaultFor("Define"));
        }

        [Fact]
        [DisplayName("DefaultFor returns a root definition folder itself")]
        public void DefaultFor_Root_ReturnsRoot()
        {
            var root = Path.GetPathRoot(Path.GetTempPath())!;

            Assert.Equal(root, DataDirectory.DefaultFor(root));
        }
    }
}
