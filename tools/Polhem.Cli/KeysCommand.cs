using Polhem.Definition.Security;
using Polhem.Definition.Settings;

namespace Polhem.Cli;

/// <summary>
/// Subcommand group for the <c>keys</c> command: produces the encrypted key values that
/// <see cref="SecurityKeySettings"/> stores. Thin shell over <see cref="EncryptionKeyProtector"/>.
/// </summary>
internal static class KeysCommand
{
    private const string DefaultMasterKeyVariable = "POLHEM_MASTER_KEY";

    public static int Run(string[] args) => Run(args, Console.Out);

    public static int Run(string[] args, TextWriter output)
    {
        if (args.Length == 0)
        {
            PrintHelp(output);
            return ExitCodes.Success;
        }

        return args[0] switch
        {
            "protect" => Protect(args.AsSpan(1).ToArray(), output),
            "--help" or "-h" or "help" => HelpSelf(output),
            _ => throw new UsageException($"unknown 'keys' subcommand: '{args[0]}'"),
        };
    }

    private static int HelpSelf(TextWriter output)
    {
        PrintHelp(output);
        return ExitCodes.Success;
    }

    public static void PrintHelp(TextWriter writer)
    {
        writer.WriteLine("dotnet polhem keys - Produce encrypted key values for SystemSettings.xml");
        writer.WriteLine();
        writer.WriteLine("Usage: dotnet polhem keys <subcommand> [options]");
        writer.WriteLine();
        writer.WriteLine("Subcommands:");
        writer.WriteLine("  protect       Generate a new key and print it encrypted with the master key");
        writer.WriteLine();
        writer.WriteLine("Run 'dotnet polhem keys <subcommand> --help' for subcommand options.");
    }

    private static int Protect(string[] args, TextWriter output)
    {
        string? masterKeyFile = null;
        string? masterKeyVariable = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--master-key-file":
                    if (i + 1 >= args.Length) throw new UsageException("--master-key-file requires a value");
                    masterKeyFile = args[++i];
                    break;
                case "--master-key-env":
                    if (i + 1 >= args.Length) throw new UsageException("--master-key-env requires a value");
                    masterKeyVariable = args[++i];
                    break;
                case "--help":
                case "-h":
                    PrintProtectHelp(output);
                    return ExitCodes.Success;
                default:
                    throw new UsageException($"unknown option for 'protect': '{args[i]}'");
            }
        }

        if (masterKeyFile != null && masterKeyVariable != null)
        {
            throw new UsageException("pass either --master-key-file or --master-key-env, not both");
        }

        var source = masterKeyFile != null
            ? new MasterKeySource { Type = MasterKeySourceType.File, Value = Path.GetFullPath(masterKeyFile) }
            : new MasterKeySource { Type = MasterKeySourceType.Environment, Value = masterKeyVariable ?? DefaultMasterKeyVariable };

        // autoCreate stays off: a master key generated here would exist only in this process, and
        // the value printed would be unreadable by the host that is meant to decrypt it.
        byte[] masterKey = MasterKeyProvider.GetMasterKey(source, Directory.GetCurrentDirectory(), autoCreate: false);
        output.WriteLine(EncryptionKeyProtector.GenerateEncryptedKey(masterKey));
        return ExitCodes.Success;
    }

    private static void PrintProtectHelp(TextWriter writer)
    {
        writer.WriteLine("dotnet polhem keys protect - Generate a key and print it encrypted with the master key");
        writer.WriteLine();
        writer.WriteLine("Usage: dotnet polhem keys protect [options]");
        writer.WriteLine();
        writer.WriteLine("Prints a Base64 value for SecurityKeySettings in SystemSettings.xml, such as");
        writer.WriteLine("ApiEncryptionKey or ConfigEncryptionKey. Use the master key the host reads, or");
        writer.WriteLine("the host cannot decrypt the value.");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  --master-key-env <name>   Read the master key from this environment variable.");
        writer.WriteLine("                            Default: POLHEM_MASTER_KEY.");
        writer.WriteLine("  --master-key-file <path>  Read the master key from this file instead.");
        writer.WriteLine("  --help, -h                Print this help text.");
    }
}
