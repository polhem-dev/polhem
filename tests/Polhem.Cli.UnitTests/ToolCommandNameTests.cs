using System.ComponentModel;
using System.Xml.Linq;

namespace Polhem.Cli.UnitTests
{
    /// <summary>
    /// Pins the name the tool is installed under and checks that the help text teaches the same name.
    /// </summary>
    /// <remarks>
    /// <c>ToolCommandName</c> lives only in the project file, so nothing that compiles or runs reads
    /// it back. Users type the command from the help text and the README, which is where a mismatch
    /// would show.
    /// </remarks>
    public class ToolCommandNameTests
    {
        private const string CommandName = "dotnet-polhem";

        [Fact]
        [DisplayName("ToolCommandName 為 dotnet-polhem")]
        public void ToolCommandName_IsDotnetPolhem()
        {
            Assert.Equal(CommandName, ReadToolCommandName());
        }

        [Fact]
        [DisplayName("說明文字的 Usage 使用與 ToolCommandName 相同的指令名")]
        public void HelpText_UsesInstalledCommandName()
        {
            // `dotnet-<name>` is invoked as `dotnet <name>`.
            var invocation = "dotnet " + ReadToolCommandName()["dotnet-".Length..];
            using var writer = new StringWriter();

            DefinesCommand.PrintHelp(writer);

            Assert.Contains("Usage: " + invocation + " defines", writer.ToString(), StringComparison.Ordinal);
        }

        private static string ReadToolCommandName()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && dir.GetDirectories(".git").Length == 0)
            {
                dir = dir.Parent;
            }
            Assert.True(dir != null, "No repository root (.git) above the test output directory.");

            var project = XDocument.Load(Path.Combine(dir!.FullName, "tools", "Polhem.Cli", "Polhem.Cli.csproj"));
            var name = project.Descendants("ToolCommandName").Select(e => e.Value).SingleOrDefault();
            Assert.True(name != null, "Polhem.Cli.csproj declares no ToolCommandName.");
            return name!;
        }
    }
}
