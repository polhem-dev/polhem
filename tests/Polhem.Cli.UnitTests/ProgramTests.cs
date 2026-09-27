using System.ComponentModel;

namespace Polhem.Cli.UnitTests
{
    /// <summary>
    /// Tests for the CLI entry point: routing, the root help, <c>--version</c>, and the mapping of failures onto
    /// the exit codes scripts rely on.
    /// </summary>
    public class ProgramTests
    {
        [Fact]
        [DisplayName("Main with no arguments prints the root help and exits with Success")]
        public void Main_NoArguments_PrintsRootHelp()
        {
            var (exitCode, output, error) = ConsoleCapture.Run();

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Contains("Usage: dotnet polhem <command> [options]", output, StringComparison.Ordinal);
            Assert.Contains("defines", output, StringComparison.Ordinal);
            Assert.Contains("keys", output, StringComparison.Ordinal);
            Assert.Equal(string.Empty, error);
        }

        [Theory]
        [InlineData("--version")]
        [InlineData("-v")]
        [DisplayName("Main with a version flag prints the tool name and version and exits with Success")]
        public void Main_VersionFlag_PrintsVersion(string flag)
        {
            var (exitCode, output, _) = ConsoleCapture.Run(flag);

            Assert.Equal(ExitCodes.Success, exitCode);
            var firstLine = output.Split(Environment.NewLine)[0];
            Assert.StartsWith("dotnet-polhem ", firstLine, StringComparison.Ordinal);
            Assert.True(firstLine.Length > "dotnet-polhem ".Length, "The version line carries no version.");
        }

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        [InlineData("help")]
        [DisplayName("Main with a help flag and no command prints the root help and exits with Success")]
        public void Main_HelpFlag_PrintsRootHelp(string flag)
        {
            var (exitCode, output, _) = ConsoleCapture.Run(flag);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Contains("Usage: dotnet polhem <command> [options]", output, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("defines", "Usage: dotnet polhem defines <subcommand>")]
        [InlineData("keys", "Usage: dotnet polhem keys <subcommand>")]
        [DisplayName("Main help with a command name prints that command's help")]
        public void Main_HelpForCommand_PrintsCommandHelp(string command, string expectedUsage)
        {
            var (exitCode, output, _) = ConsoleCapture.Run("help", command);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Contains(expectedUsage, output, StringComparison.Ordinal);
            Assert.DoesNotContain("Usage: dotnet polhem <command>", output, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Main with an unknown command exits with Usage and prints the error and the root help to stderr")]
        public void Main_UnknownCommand_ExitsWithUsage()
        {
            var (exitCode, output, error) = ConsoleCapture.Run("bogus");

            Assert.Equal(ExitCodes.Usage, exitCode);
            Assert.Contains("error: unknown command: 'bogus'", error, StringComparison.Ordinal);
            Assert.Contains("Usage: dotnet polhem <command> [options]", error, StringComparison.Ordinal);
            Assert.Equal(string.Empty, output);
        }

        [Fact]
        [DisplayName("Main maps a failure that is not a usage error to the Error exit code without printing the root help")]
        public void Main_OperationFails_ExitsWithError()
        {
            // A file where the target directory should be makes `materialize` fail with an I/O error, which is an
            // operational failure rather than a mistake in the command line.
            string blocker = Path.Combine(Path.GetTempPath(), $"polhem-cli-blocker-{Guid.NewGuid():N}");
            File.WriteAllText(blocker, "not a directory");
            try
            {
                var (exitCode, _, error) = ConsoleCapture.Run("defines", "materialize", "--path", blocker);

                Assert.Equal(ExitCodes.Error, exitCode);
                Assert.StartsWith("error: ", error, StringComparison.Ordinal);
                Assert.DoesNotContain("Usage: dotnet polhem <command>", error, StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(blocker);
            }
        }
    }
}
