namespace Polhem.Cli.UnitTests
{
    /// <summary>
    /// Redirects <see cref="Console.Out"/> and <see cref="Console.Error"/> for the lifetime of the instance and
    /// restores the previous writers on dispose.
    /// </summary>
    /// <remarks>
    /// The console is process-wide state. This assembly disables test parallelization (see <c>AssemblyInfo.cs</c>),
    /// which is what makes the redirection safe.
    /// </remarks>
    internal sealed class ConsoleCapture : IDisposable
    {
        private readonly TextWriter _previousOut;
        private readonly TextWriter _previousError;
        private readonly StringWriter _out = new();
        private readonly StringWriter _error = new();

        public ConsoleCapture()
        {
            _previousOut = Console.Out;
            _previousError = Console.Error;
            Console.SetOut(_out);
            Console.SetError(_error);
        }

        public string Out => _out.ToString();

        public string Error => _error.ToString();

        /// <summary>
        /// Runs the CLI entry point with the given arguments and returns its exit code.
        /// </summary>
        public static (int ExitCode, string Out, string Error) Run(params string[] args)
        {
            using var capture = new ConsoleCapture();
            int exitCode = Program.Main(args);
            return (exitCode, capture.Out, capture.Error);
        }

        public void Dispose()
        {
            Console.SetOut(_previousOut);
            Console.SetError(_previousError);
            _out.Dispose();
            _error.Dispose();
        }
    }
}
