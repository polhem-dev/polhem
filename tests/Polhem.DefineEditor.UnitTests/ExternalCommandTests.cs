using System.ComponentModel;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// A command DefineEditor runs as a child process: how it is shown and where it runs.
    /// </summary>
    public class ExternalCommandTests
    {
        [Fact]
        [DisplayName("The display string quotes an argument that contains spaces")]
        public void ToDisplayString_QuotesArgumentsWithSpaces()
        {
            var command = new ExternalCommand { Command = "dotnet", Arguments = ["tool", "exec", "--", "--define", "/My Project/Define"] };

            Assert.Equal("dotnet tool exec -- --define \"/My Project/Define\"", command.ToDisplayString());
        }

        [Fact]
        [DisplayName("A relative working directory is resolved against the base directory, and empty means that directory")]
        public void ResolveWorkingDirectory_RelativeAndEmpty()
        {
            var define = Path.Combine(Path.GetTempPath(), "project", "Define");

            Assert.Equal(Path.Combine(Path.GetTempPath(), "project"),
                new ExternalCommand { Command = "dotnet", WorkingDirectory = ".." }.ResolveWorkingDirectory(define));
            Assert.Equal(define, new ExternalCommand { Command = "dotnet" }.ResolveWorkingDirectory(define));
        }
    }
}
