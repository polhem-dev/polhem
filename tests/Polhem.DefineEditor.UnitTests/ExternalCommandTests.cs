using System.ComponentModel;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The command a solution keeps in its settings: how its arguments are typed on one line, where it runs, and what
    /// identifies it for the user's approval.
    /// </summary>
    public class ExternalCommandTests
    {
        private static ExternalCommand Upgrade() => new()
        {
            Command = "dotnet",
            Arguments = ["run", "--project", "../My App.Server", "--", "db", "upgrade"],
            WorkingDirectory = "..",
        };

        [Fact]
        [DisplayName("Spaces separate arguments and double quotes keep spaces inside one")]
        public void SplitArguments_QuotedArgument_KeepsSpaces()
        {
            var arguments = ExternalCommand.SplitArguments("run  --project \"../My App.Server\" -- db upgrade");

            Assert.Equal(["run", "--project", "../My App.Server", "--", "db", "upgrade"], arguments);
        }

        [Fact]
        [DisplayName("An empty quoted argument is kept as an empty argument")]
        public void SplitArguments_EmptyQuotes_GiveEmptyArgument()
        {
            Assert.Equal(["--name", ""], ExternalCommand.SplitArguments("--name \"\""));
        }

        [Fact]
        [DisplayName("Joined arguments split back to the same list")]
        public void JoinArguments_RoundTrips()
        {
            var arguments = Upgrade().Arguments;

            Assert.Equal(arguments, ExternalCommand.SplitArguments(ExternalCommand.JoinArguments(arguments)));
        }

        [Fact]
        [DisplayName("The display string quotes an argument that contains spaces")]
        public void ToDisplayString_QuotesArgumentsWithSpaces()
        {
            Assert.Equal("dotnet run --project \"../My App.Server\" -- db upgrade", Upgrade().ToDisplayString());
        }

        [Fact]
        [DisplayName("A relative working directory is resolved against the settings folder, and empty means that folder")]
        public void ResolveWorkingDirectory_RelativeAndEmpty()
        {
            var define = Path.Combine(Path.GetTempPath(), "project", "Define");

            Assert.Equal(Path.Combine(Path.GetTempPath(), "project"), Upgrade().ResolveWorkingDirectory(define));
            Assert.Equal(define, new ExternalCommand { Command = "dotnet" }.ResolveWorkingDirectory(define));
        }

        [Fact]
        [DisplayName("WithArgument appends to a copy and leaves the original unchanged")]
        public void WithArgument_AppendsToCopy()
        {
            var original = Upgrade();

            var dryRun = original.WithArgument("--dry-run");

            Assert.Equal("--dry-run", dryRun.Arguments[^1]);
            Assert.Equal("upgrade", original.Arguments[^1]);
        }

        [Fact]
        [DisplayName("The fingerprint changes with the solution, the program, an argument or the working directory")]
        public void Fingerprint_ChangesWithEveryPart()
        {
            var solution = Path.Combine(Path.GetTempPath(), "project", "Define");
            var baseline = Upgrade().Fingerprint(solution);

            Assert.Equal(baseline, Upgrade().Fingerprint(solution));
            Assert.NotEqual(baseline, Upgrade().Fingerprint(solution + "2"));
            var program = Upgrade();
            program.Command = "sh";
            Assert.NotEqual(baseline, program.Fingerprint(solution));
            Assert.NotEqual(baseline, Upgrade().WithArgument("--allow-narrowing").Fingerprint(solution));
            var folder = Upgrade();
            folder.WorkingDirectory = ".";
            Assert.NotEqual(baseline, folder.Fingerprint(solution));
        }

        [Fact]
        [DisplayName("Arguments that join to the same text still give different fingerprints")]
        public void Fingerprint_ArgumentBoundariesMatter()
        {
            var solution = Path.Combine(Path.GetTempPath(), "project");
            var one = new ExternalCommand { Command = "dotnet", Arguments = ["db upgrade"] };
            var two = new ExternalCommand { Command = "dotnet", Arguments = ["db", "upgrade"] };

            Assert.NotEqual(one.Fingerprint(solution), two.Fingerprint(solution));
        }

        [Fact]
        [DisplayName("An approved fingerprint is trusted, and approving it twice records it once")]
        public void UserSettings_TrustCommand_RecordsOnce()
        {
            var settings = new UserSettings();

            settings.TrustCommand("A1");
            settings.TrustCommand("A1");

            Assert.True(settings.IsCommandTrusted("A1"));
            Assert.False(settings.IsCommandTrusted("B2"));
            Assert.Single(settings.TrustedCommands);
        }
    }
}
