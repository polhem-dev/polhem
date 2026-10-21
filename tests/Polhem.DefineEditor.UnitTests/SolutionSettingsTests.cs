using System.ComponentModel;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The settings file a solution keeps in its DefinePath, which a project template fills in.
    /// </summary>
    public sealed class SolutionSettingsTests : IDisposable
    {
        private readonly string _definePath = Path.Combine(Path.GetTempPath(), $"polhem-solution-{Guid.NewGuid():N}");

        public SolutionSettingsTests() => Directory.CreateDirectory(_definePath);

        public void Dispose() => Directory.Delete(_definePath, recursive: true);

        [Fact]
        [DisplayName("A solution without the file has no upgrade command")]
        public void Load_NoFile_ReturnsDefaults()
        {
            Assert.False(SolutionSettings.Load(_definePath).DatabaseUpgrade.IsConfigured);
        }

        [Fact]
        [DisplayName("The file a template writes by hand is read, with camel-case names")]
        public void Load_TemplateFile_ReadsCommand()
        {
            File.WriteAllText(SolutionSettings.GetPath(_definePath), """
                {
                  "databaseUpgrade": {
                    "command": "dotnet",
                    "arguments": [ "run", "--project", "../MyApp.Server", "--", "db", "upgrade" ],
                    "workingDirectory": ".."
                  }
                }
                """);

            var command = SolutionSettings.Load(_definePath).DatabaseUpgrade;

            Assert.Equal("dotnet", command.Command);
            Assert.Equal(["run", "--project", "../MyApp.Server", "--", "db", "upgrade"], command.Arguments);
            Assert.Equal("..", command.WorkingDirectory);
        }

        [Fact]
        [DisplayName("Saved settings load back unchanged")]
        public void Save_RoundTrips()
        {
            var settings = new SolutionSettings
            {
                DatabaseUpgrade = new ExternalCommand { Command = "dotnet", Arguments = ["db", "upgrade"], WorkingDirectory = ".." },
            };

            settings.Save(_definePath);
            var loaded = SolutionSettings.Load(_definePath).DatabaseUpgrade;

            Assert.Equal("dotnet", loaded.Command);
            Assert.Equal(["db", "upgrade"], loaded.Arguments);
            Assert.Equal("..", loaded.WorkingDirectory);
        }

        [Fact]
        [DisplayName("A section left out of the file reads as empty instead of null")]
        public void Load_NullSection_GivesEmptyCommand()
        {
            File.WriteAllText(SolutionSettings.GetPath(_definePath), """{ "databaseUpgrade": null }""");

            Assert.False(SolutionSettings.Load(_definePath).DatabaseUpgrade.IsConfigured);
        }

        [Fact]
        [DisplayName("A file that is not valid JSON is reported as invalid data")]
        public void Load_InvalidJson_ThrowsInvalidData()
        {
            File.WriteAllText(SolutionSettings.GetPath(_definePath), "{ not json");

            Assert.Throws<InvalidDataException>(() => SolutionSettings.Load(_definePath));
        }

        [Fact]
        [DisplayName("The dialog splits the typed arguments into the list it saves")]
        public void Dialog_Apply_SplitsArguments()
        {
            var settings = new SolutionSettings();
            var dialog = new SolutionSettingsDialogViewModel(settings)
            {
                Command = " dotnet ",
                ArgumentsText = "run --project \"../My App.Server\" -- db upgrade",
                WorkingDirectory = "..",
            };

            dialog.ApplyCommand.Execute(null);

            Assert.Equal("dotnet", settings.DatabaseUpgrade.Command);
            Assert.Equal(["run", "--project", "../My App.Server", "--", "db", "upgrade"], settings.DatabaseUpgrade.Arguments);
        }

        [Fact]
        [DisplayName("The dialog warns when the command line carries a password")]
        public void Dialog_PasswordInArguments_HasSecret()
        {
            var dialog = new SolutionSettingsDialogViewModel(new SolutionSettings()) { Command = "dotnet", ArgumentsText = "db upgrade" };
            Assert.False(dialog.HasSecret);

            dialog.ArgumentsText = "db upgrade --connection \"Server=x;Password=secret\"";

            Assert.True(dialog.HasSecret);
        }
    }
}
