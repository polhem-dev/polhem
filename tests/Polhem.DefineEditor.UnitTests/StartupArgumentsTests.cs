using System.ComponentModel;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// How the command line names the DefinePath a project task opens.
    /// </summary>
    public class StartupArgumentsTests
    {
        private static readonly string s_current = Path.Combine(Path.GetTempPath(), "project");

        [Fact]
        [DisplayName("No argument names no solution")]
        public void GetSolutionPath_NoArguments_ReturnsNull()
        {
            Assert.Null(StartupArguments.GetSolutionPath([], s_current));
            Assert.Null(StartupArguments.GetSolutionPath(null, s_current));
        }

        [Fact]
        [DisplayName("A relative path is resolved against the current directory")]
        public void GetSolutionPath_RelativePath_ResolvesAgainstCurrentDirectory()
        {
            Assert.Equal(Path.Combine(s_current, "Define"), StartupArguments.GetSolutionPath(["Define"], s_current));
        }

        [Fact]
        [DisplayName("A trailing separator is dropped, so the path matches the one Open Folder records")]
        public void GetSolutionPath_TrailingSeparator_IsTrimmed()
        {
            var path = Path.Combine(s_current, "Define") + Path.DirectorySeparatorChar;

            Assert.Equal(Path.Combine(s_current, "Define"), StartupArguments.GetSolutionPath([path], s_current));
        }

        [Fact]
        [DisplayName("Options before the path are skipped, such as the process serial number macOS passes")]
        public void GetSolutionPath_OptionsFirst_TakesFirstNonOption()
        {
            var path = StartupArguments.GetSolutionPath(["-psn_0_12345", "--verbose", "Define"], s_current);

            Assert.Equal(Path.Combine(s_current, "Define"), path);
        }
    }
}
