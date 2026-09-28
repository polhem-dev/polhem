using System.Reflection;

namespace Polhem.Cli;

/// <summary>
/// Entry point for the <c>dotnet polhem</c> CLI. Routes to subcommand groups
/// (<c>defines</c>, <c>keys</c>) plus a few top-level helpers (<c>--version</c>, <c>--help</c>).
/// </summary>
internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            return Dispatch(args);
        }
        catch (UsageException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            Console.Error.WriteLine();
            PrintRootHelp(Console.Error);
            return ExitCodes.Usage;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return ExitCodes.Error;
        }
    }

    private static int Dispatch(string[] args)
    {
        if (args.Length == 0)
        {
            PrintRootHelp(Console.Out);
            return ExitCodes.Success;
        }

        return args[0] switch
        {
            "--version" or "-v" => PrintVersion(),
            "--help" or "-h" or "help" => Help(args),
            "defines" => DefinesCommand.Run(args.AsSpan(1).ToArray()),
            "keys" => KeysCommand.Run(args.AsSpan(1).ToArray()),
            _ => throw new UsageException($"unknown command: '{args[0]}'"),
        };
    }

    private static int Help(string[] args)
    {
        if (args.Length >= 2 && args[1] == "defines")
        {
            DefinesCommand.PrintHelp(Console.Out);
        }
        else if (args.Length >= 2 && args[1] == "keys")
        {
            KeysCommand.PrintHelp(Console.Out);
        }
        else
        {
            PrintRootHelp(Console.Out);
        }
        return ExitCodes.Success;
    }

    private static int PrintVersion()
    {
        var asm = typeof(Program).Assembly;
        var version = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? asm.GetName().Version?.ToString()
                      ?? "unknown";
        Console.WriteLine($"dotnet-polhem {version}");
        Console.WriteLine($"Polhem framework CLI");
        return ExitCodes.Success;
    }

    private static void PrintRootHelp(TextWriter writer)
    {
        writer.WriteLine("dotnet polhem - Polhem framework CLI");
        writer.WriteLine();
        writer.WriteLine("Usage: dotnet polhem <command> [options]");
        writer.WriteLine();
        writer.WriteLine("Commands:");
        writer.WriteLine("  defines       Manage define files (materialize / list)");
        writer.WriteLine("  keys          Produce encrypted key values for SystemSettings.xml (protect)");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  --version, -v Print the CLI version and exit");
        writer.WriteLine("  --help, -h    Print this help text");
        writer.WriteLine();
        writer.WriteLine("Run 'dotnet polhem help <command>' for command-specific help.");
    }
}
