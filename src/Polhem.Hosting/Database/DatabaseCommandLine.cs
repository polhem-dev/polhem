using System.Data.Common;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Db.Manager;
using Polhem.Db.Schema;

namespace Polhem.Hosting.Database
{
    /// <summary>
    /// Runs the <c>db</c> commands of a host: <c>db create</c>, <c>db upgrade</c> and
    /// <c>db seed-admin</c>, backed by <see cref="IDatabaseInitializer"/>.
    /// </summary>
    /// <remarks>
    /// A host calls <see cref="TryRun(IServiceProvider, string[])"/> after building its service
    /// provider and before it starts serving, and exits with the returned code when there is one:
    /// <code>
    /// if (DatabaseCommandLine.TryRun(app.Services, args) is int exitCode) return exitCode;
    /// </code>
    /// The commands do not start the host, so its hosted services never run.
    /// <para>
    /// The administrator password is never taken from the command line, where it would stay in the
    /// shell history. It is read from the <see cref="PasswordVariable"/> environment variable, or
    /// typed at a prompt that does not echo it.
    /// </para>
    /// </remarks>
    public static class DatabaseCommandLine
    {
        /// <summary>The environment variable <c>db seed-admin</c> reads the password from.</summary>
        public const string PasswordVariable = "POLHEM_ADMIN_PASSWORD";

        /// <summary>The exit code for a command that completed.</summary>
        public const int Success = 0;

        /// <summary>The exit code for a command that ran and had at least one item fail.</summary>
        public const int Failure = 1;

        /// <summary>The exit code for a command line that could not be understood.</summary>
        public const int UsageError = 2;

        private const string Usage =
            "Usage:\n" +
            "  db create                     Create the databases named in DatabaseSettings that do not exist.\n" +
            "  db upgrade [--dry-run] [--allow-narrowing]\n" +
            "                                Build or upgrade every table registered in DbCategorySettings.\n" +
            "  db seed-admin --company <id> [--company-name <name>] [--company-database <id>]\n" +
            "                [--user <id>] [--name <name>] [--currency <code>] [--no-deployment-admin]\n" +
            "                                Create the first administrator, a company and the access between them.\n" +
            "                                The password is read from " + PasswordVariable + " or prompted for.\n";

        /// <summary>
        /// Runs a <c>db</c> command when the arguments start with <c>db</c>.
        /// </summary>
        /// <param name="services">The host's service provider.</param>
        /// <param name="args">The command-line arguments.</param>
        /// <returns>
        /// The exit code, or <c>null</c> when the arguments are not a <c>db</c> command and the host
        /// should start as usual.
        /// </returns>
        public static int? TryRun(IServiceProvider services, string[] args)
            => TryRun(services, args, Console.Out, ReadSecretFromConsole, Environment.GetEnvironmentVariable);

        /// <summary>
        /// Runs a <c>db</c> command with its input and output supplied by the caller.
        /// </summary>
        /// <param name="services">The service provider.</param>
        /// <param name="args">The command-line arguments.</param>
        /// <param name="output">Where the report is written.</param>
        /// <param name="readSecret">Prompts for a secret without echoing it; returns <c>null</c> when it cannot prompt.</param>
        /// <param name="getEnvironmentVariable">Reads an environment variable.</param>
        internal static int? TryRun(
            IServiceProvider services, string[] args, TextWriter output,
            Func<string, string?> readSecret, Func<string, string?> getEnvironmentVariable)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(args);
            if (args.Length == 0 || !string.Equals(args[0], "db", StringComparison.Ordinal)) return null;

            var command = args.Length > 1 ? args[1] : string.Empty;
            var options = args.Skip(2).ToArray();
            try
            {
                return command switch
                {
                    "create" => RunCreate(services, options, output),
                    "upgrade" => RunUpgrade(services, options, output),
                    "seed-admin" => RunSeedAdmin(services, options, output, readSecret, getEnvironmentVariable),
                    _ => WriteUsage(output, command.Length == 0 || command == "help"
                        ? null : $"Unknown command 'db {command}'."),
                };
            }
            catch (ArgumentException ex)
            {
                return WriteUsage(output, ex.Message);
            }
            catch (DbException ex)
            {
                return WriteFailure(output, ex);
            }
            catch (InvalidOperationException ex)
            {
                return WriteFailure(output, ex);
            }
            catch (KeyNotFoundException ex)
            {
                // A database id with no item, or an engine with no registered provider.
                return WriteFailure(output, ex);
            }
            catch (IOException ex)
            {
                // A definition file that is missing or unreadable, or a SQLite directory that cannot be made.
                return WriteFailure(output, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                return WriteFailure(output, ex);
            }
        }

        private static int WriteFailure(TextWriter output, Exception exception)
        {
            output.WriteLine($"Failed: {exception.Message}");
            return Failure;
        }

        private static int RunCreate(IServiceProvider services, string[] options, TextWriter output)
        {
            RejectUnknown(options);
            var entries = services.GetRequiredService<IDatabaseInitializer>().CreateDatabases();
            if (entries.Count == 0)
            {
                output.WriteLine("DatabaseSettings has no database items; nothing to create.");
                return Failure;
            }

            foreach (var entry in entries)
            {
                var status = entry.Result switch
                {
                    DatabaseCreateResult.Created => "created",
                    DatabaseCreateResult.AlreadyExists => "exists",
                    DatabaseCreateResult.NotApplicable => "skipped (nothing to create on this engine)",
                    _ => "FAILED: " + entry.Error,
                };
                var name = entry.DatabaseName.Length == 0 ? string.Empty : $" ({entry.DatabaseName})";
                output.WriteLine($"  {entry.DatabaseId}{name}: {status}");
            }

            var failed = entries.Count(e => e.Result is null);
            output.WriteLine(failed == 0 ? "Done." : $"{failed} database(s) failed.");
            return failed == 0 ? Success : Failure;
        }

        private static int RunUpgrade(IServiceProvider services, string[] options, TextWriter output)
        {
            var dryRun = TakeFlag(ref options, "--dry-run");
            var allowNarrowing = TakeFlag(ref options, "--allow-narrowing");
            RejectUnknown(options);

            var upgradeOptions = allowNarrowing ? new UpgradeOptions { AllowColumnNarrowing = true } : null;
            // Each table is written as it completes, so a caller showing the output live (DefineEditor
            // runs this command as a child process) sees progress rather than one block at the end.
            var report = services.GetRequiredService<IDatabaseInitializer>().UpgradeTables(
                upgradeOptions, dryRun, table => WriteTable(output, table));

            foreach (var problem in report.Problems)
                output.WriteLine($"  {problem}");

            var summary = new StringBuilder();
            summary.Append(report.Tables.Count(t => t.Outcome == TableUpgradeOutcome.Created)).Append(" created, ")
                .Append(report.Tables.Count(t => t.Outcome == TableUpgradeOutcome.Upgraded)).Append(" upgraded, ")
                .Append(report.Tables.Count(t => t.Outcome == TableUpgradeOutcome.Unchanged)).Append(" unchanged");
            if (dryRun)
                summary.Append(", ").Append(report.Tables.Count(t => t.Outcome == TableUpgradeOutcome.Planned)).Append(" planned");
            summary.Append(", ").Append(report.Tables.Count(t => t.Outcome == TableUpgradeOutcome.Failed)).Append(" failed.");
            output.WriteLine(summary.ToString());
            return report.Succeeded ? Success : Failure;
        }

        private static void WriteTable(TextWriter output, TableUpgradeEntry table)
        {
            var status = table.Outcome switch
            {
                TableUpgradeOutcome.Created => "created",
                TableUpgradeOutcome.Upgraded => "upgraded",
                TableUpgradeOutcome.Unchanged => "unchanged",
                TableUpgradeOutcome.Planned => "planned",
                _ => "FAILED: " + table.Error,
            };
            output.WriteLine($"  {table.DatabaseId}.{table.TableName}: {status}");
            if (table.Outcome == TableUpgradeOutcome.Planned)
                output.WriteLine(Indent(table.CommandText));
            output.Flush();
        }

        private static int RunSeedAdmin(
            IServiceProvider services, string[] options, TextWriter output,
            Func<string, string?> readSecret, Func<string, string?> getEnvironmentVariable)
        {
            var seed = new AdministratorSeed
            {
                DeploymentAdmin = !TakeFlag(ref options, "--no-deployment-admin"),
            };
            seed.UserId = TakeValue(ref options, "--user") ?? seed.UserId;
            seed.UserName = TakeValue(ref options, "--name") ?? string.Empty;
            seed.CompanyId = TakeValue(ref options, "--company") ?? string.Empty;
            seed.CompanyName = TakeValue(ref options, "--company-name") ?? string.Empty;
            seed.CompanyDatabaseId = TakeValue(ref options, "--company-database") ?? string.Empty;
            seed.DefaultCurrency = TakeValue(ref options, "--currency") ?? seed.DefaultCurrency;
            RejectUnknown(options);
            if (string.IsNullOrWhiteSpace(seed.CompanyId))
                throw new ArgumentException("--company is required.");

            seed.Password = ReadPassword(output, readSecret, getEnvironmentVariable);

            var result = services.GetRequiredService<IDatabaseInitializer>().SeedAdministrator(seed);
            output.WriteLine($"  user {seed.UserId}: " + (result.UserCreated
                ? "created"
                : "exists, left unchanged (the password and the deployment administrator option were not applied)"));
            output.WriteLine($"  company {seed.CompanyId} (database {result.CompanyDatabaseId}): " +
                             (result.CompanyCreated ? "created" : "exists, left unchanged"));
            output.WriteLine($"  access {seed.UserId} -> {seed.CompanyId}: {(result.AccessGranted ? "granted" : "exists")}");
            output.WriteLine("Done.");
            return Success;
        }

        private static string ReadPassword(
            TextWriter output, Func<string, string?> readSecret, Func<string, string?> getEnvironmentVariable)
        {
            var fromEnvironment = getEnvironmentVariable(PasswordVariable);
            if (!string.IsNullOrEmpty(fromEnvironment)) return fromEnvironment;

            var first = readSecret("Administrator password: ")
                ?? throw new ArgumentException(
                    $"No password: set {PasswordVariable}, or run the command in an interactive terminal.");
            var second = readSecret("Repeat the password: ") ?? string.Empty;
            if (!string.Equals(first, second, StringComparison.Ordinal))
                throw new ArgumentException("The two passwords do not match.");
            output.WriteLine();
            return first;
        }

        /// <summary>
        /// Reads a line from the console without echoing it, or returns <c>null</c> when input is
        /// redirected and nobody can be prompted.
        /// </summary>
        private static string? ReadSecretFromConsole(string prompt)
        {
            if (Console.IsInputRedirected) return null;

            Console.Write(prompt);
            var secret = new StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter) break;
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (secret.Length > 0) secret.Length--;
                    continue;
                }
                if (!char.IsControl(key.KeyChar)) secret.Append(key.KeyChar);
            }
            Console.WriteLine();
            return secret.ToString();
        }

        private static bool TakeFlag(ref string[] options, string name)
        {
            var index = Array.IndexOf(options, name);
            if (index < 0) return false;
            options = options.Where((_, i) => i != index).ToArray();
            return true;
        }

        private static string? TakeValue(ref string[] options, string name)
        {
            var index = Array.IndexOf(options, name);
            if (index < 0) return null;
            if (index + 1 >= options.Length || options[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"{name} needs a value.");
            var value = options[index + 1];
            options = options.Where((_, i) => i != index && i != index + 1).ToArray();
            return value;
        }

        private static void RejectUnknown(string[] options)
        {
            if (options.Length == 0) return;
            // Only an option name is echoed, never a value: a password typed on the command line by
            // mistake (`--password=x`, or a bare argument) must not reach the terminal or a CI log.
            var first = options[0];
            if (first.StartsWith("--password", StringComparison.Ordinal))
                throw new ArgumentException($"There is no --password option; set {PasswordVariable} or answer the prompt.");
            if (!first.StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Unexpected argument; values follow the option they belong to.");
            var equals = first.IndexOf('=', StringComparison.Ordinal);
            throw new ArgumentException($"Unknown option '{(equals < 0 ? first : first[..equals])}'.");
        }

        private static int WriteUsage(TextWriter output, string? error)
        {
            if (error is not null) output.WriteLine(error);
            output.Write(Usage);
            return error is null ? Success : UsageError;
        }

        private static string Indent(string text)
            => string.Join(Environment.NewLine, text.Split('\n').Select(line => "      " + line.TrimEnd('\r')));
    }
}
