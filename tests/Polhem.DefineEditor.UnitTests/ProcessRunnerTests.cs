using System.Collections.Concurrent;
using System.ComponentModel;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// Running a solution command as a child process: its lines, its exit code, and the ways it can fail to run.
    /// </summary>
    /// <remarks>
    /// The commands are <c>dotnet</c>, which every machine that runs these tests has, and <c>/bin/sh</c> where the
    /// test needs a shell; the shell tests do nothing on Windows.
    /// </remarks>
    public class ProcessRunnerTests
    {
        private static readonly string s_baseDirectory = Path.GetTempPath();

        private static async Task<(ProcessRunResult Result, List<string> Lines)> RunAsync(
            ExternalCommand command, CancellationToken cancellationToken = default)
        {
            var lines = new ConcurrentQueue<string>();
            var result = await ProcessRunner.RunAsync(command, s_baseDirectory, lines.Enqueue, cancellationToken);
            return (result, lines.ToList());
        }

        private static ExternalCommand Shell(string script) => new() { Command = "/bin/sh", Arguments = ["-c", script] };

        [Fact]
        [DisplayName("A command that succeeds reports exit code 0 and the lines it wrote")]
        public async Task RunAsync_Success_ReportsExitCodeAndLines()
        {
            var (result, lines) = await RunAsync(new ExternalCommand { Command = "dotnet", Arguments = ["--version"] });

            Assert.True(result.Succeeded);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains(lines, line => line.Length > 0 && char.IsDigit(line[0]));
        }

        [Fact]
        [DisplayName("A failing command reports its exit code, and its standard error lines arrive too")]
        public async Task RunAsync_Failure_ReportsExitCodeAndErrorLines()
        {
            if (OperatingSystem.IsWindows()) return;

            var (result, lines) = await RunAsync(Shell("echo '  company.ft_order: FAILED: boom' 1>&2; exit 1"));

            Assert.False(result.Succeeded);
            Assert.Equal(1, result.ExitCode);
            Assert.Contains("  company.ft_order: FAILED: boom", lines);
        }

        [Fact]
        [DisplayName("Lines arrive in the order the command wrote them")]
        public async Task RunAsync_Lines_KeepTheirOrder()
        {
            if (OperatingSystem.IsWindows()) return;

            var (_, lines) = await RunAsync(Shell("for i in 1 2 3 4 5; do echo line$i; done"));

            Assert.Equal(["line1", "line2", "line3", "line4", "line5"], lines);
        }

        [Fact]
        [DisplayName("A password value the command writes is hidden before it is reported")]
        public async Task RunAsync_PasswordInOutput_IsMasked()
        {
            if (OperatingSystem.IsWindows()) return;

            var (_, lines) = await RunAsync(Shell("echo 'Failed: Server=db;Password=Secret1!'"));

            Assert.Equal(["Failed: Server=db;Password=********"], lines);
        }

        [Fact]
        [DisplayName("A command that waits for input reads end-of-file instead of hanging")]
        public async Task RunAsync_ReadsInput_GetsEndOfFile()
        {
            if (OperatingSystem.IsWindows()) return;

            var (result, lines) = await RunAsync(Shell("if read answer; then echo got; else echo eof; fi"));

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(["eof"], lines);
        }

        [Fact]
        [DisplayName("A program that does not exist is reported as unable to start")]
        public async Task RunAsync_MissingProgram_ReportsStartError()
        {
            var (result, _) = await RunAsync(new ExternalCommand { Command = $"polhem-no-such-program-{Guid.NewGuid():N}" });

            Assert.Null(result.ExitCode);
            Assert.NotNull(result.StartError);
            Assert.False(result.Succeeded);
        }

        [Fact]
        [DisplayName("A working directory that does not exist is reported as unable to start")]
        public async Task RunAsync_MissingWorkingDirectory_ReportsStartError()
        {
            var (result, _) = await RunAsync(new ExternalCommand
            {
                Command = "dotnet",
                WorkingDirectory = $"polhem-missing-{Guid.NewGuid():N}",
            });

            Assert.Null(result.ExitCode);
            Assert.Contains("working directory", result.StartError);
        }

        [Fact]
        [DisplayName("Cancelling kills the command and reports the run as cancelled")]
        public async Task RunAsync_Cancelled_KillsProcess()
        {
            if (OperatingSystem.IsWindows()) return;
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

            var started = DateTime.UtcNow;
            var (result, _) = await RunAsync(Shell("sleep 30"), cancellation.Token);

            Assert.True(result.Cancelled);
            Assert.Null(result.ExitCode);
            Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(20));
        }

        [Fact]
        [DisplayName("An upgrade run names each table with --table and ends a preview with --dry-run")]
        public void BuildRun_TablesAndDryRun_AppendOptions()
        {
            var command = new ExternalCommand { Command = "dotnet", Arguments = ["run", "--", "db", "upgrade"] };

            var run = DatabaseUpgradeViewModel.BuildRun(command, ["ft_order", "ft_order_detail"], dryRun: true);

            Assert.Equal(["run", "--", "db", "upgrade", "--table", "ft_order", "--table", "ft_order_detail", "--dry-run"], run.Arguments);
            Assert.Equal(["run", "--", "db", "upgrade"], DatabaseUpgradeViewModel.BuildRun(command, [], dryRun: false).Arguments);
        }

        [Theory]
        [DisplayName("The upgrade window's status follows the exit codes of the db commands")]
        [InlineData("exit 0", false, "Upgrade_StatusSucceeded")]
        [InlineData("exit 0", true, "Upgrade_StatusDryRunSucceeded")]
        [InlineData("exit 1", false, "Upgrade_StatusFailed")]
        [InlineData("exit 2", false, "Upgrade_StatusUsageError")]
        public async Task UpgradeViewModel_ExitCode_SetsStatus(string script, bool dryRun, string statusKey)
        {
            if (OperatingSystem.IsWindows()) return;
            using var viewModel = new DatabaseUpgradeViewModel(Shell("echo line; " + script), s_baseDirectory, dryRun);

            await viewModel.RunAsync();

            var expected = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                LocalizationService.Current[statusKey], viewModel.Result!.ExitCode);
            Assert.Equal(expected, viewModel.StatusText);
            Assert.Equal(viewModel.Result.ExitCode == 0, viewModel.Succeeded);
            Assert.Contains("line", viewModel.OutputText);
            Assert.False(viewModel.IsRunning);
        }
    }
}
