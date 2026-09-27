using System.ComponentModel;
using System.Xml.Linq;
using Polhem.Tests.Shared;

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
        [DisplayName("ToolCommandName is dotnet-polhem")]
        public void ToolCommandName_IsDotnetPolhem()
        {
            Assert.Equal(CommandName, ReadToolCommandName());
        }

        [Fact]
        [DisplayName("The Usage line of the help text uses the same command name as ToolCommandName")]
        public void HelpText_UsesInstalledCommandName()
        {
            // `dotnet-<name>` is invoked as `dotnet <name>`.
            var invocation = "dotnet " + ReadToolCommandName()["dotnet-".Length..];
            using var writer = new StringWriter();

            DefinesCommand.PrintHelp(writer);

            Assert.Contains("Usage: " + invocation + " defines", writer.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The Usage line of the root help and of the keys help use the same command name as ToolCommandName")]
        public void RootAndKeysHelp_UseInstalledCommandName()
        {
            var invocation = "dotnet " + ReadToolCommandName()["dotnet-".Length..];
            using var keysHelp = new StringWriter();
            KeysCommand.PrintHelp(keysHelp);

            var (_, rootHelp, _) = ConsoleCapture.Run();

            Assert.Contains("Usage: " + invocation + " <command>", rootHelp, StringComparison.Ordinal);
            Assert.Contains("Usage: " + invocation + " keys", keysHelp.ToString(), StringComparison.Ordinal);
        }

        private static string ReadToolCommandName()
        {
            var project = XDocument.Load(Path.Combine(RepoRoot.Find(), "tools", "Polhem.Cli", "Polhem.Cli.csproj"));
            var name = project.Descendants("ToolCommandName").Select(e => e.Value).SingleOrDefault();
            Assert.True(name != null, "Polhem.Cli.csproj declares no ToolCommandName.");
            return name!;
        }
    }
}
