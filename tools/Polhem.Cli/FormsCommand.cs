using Polhem.Definition.Spreadsheet;

namespace Polhem.Cli;

/// <summary>
/// Subcommand group for the <c>forms</c> command: describe a form in an Excel workbook and turn the workbook
/// into definition files. Thin shell over <see cref="FormWorkbookWriter"/>, <see cref="FormWorkbookReader"/>
/// and <see cref="FormImporter"/>.
/// </summary>
internal static class FormsCommand
{
    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp(Console.Out);
            return ExitCodes.Success;
        }

        return args[0] switch
        {
            "template" => Template(args.AsSpan(1).ToArray()),
            "export" => Export(args.AsSpan(1).ToArray()),
            "import" => Import(args.AsSpan(1).ToArray()),
            "--help" or "-h" or "help" => HelpSelf(),
            _ => throw new UsageException($"unknown 'forms' subcommand: '{args[0]}'"),
        };
    }

    private static int HelpSelf()
    {
        PrintHelp(Console.Out);
        return ExitCodes.Success;
    }

    public static void PrintHelp(TextWriter writer)
    {
        writer.WriteLine("dotnet polhem forms - Describe forms in Excel workbooks and import them as definition files");
        writer.WriteLine();
        writer.WriteLine("Usage: dotnet polhem forms <subcommand> [options]");
        writer.WriteLine();
        writer.WriteLine("Subcommands:");
        writer.WriteLine("  template      Write an empty workbook for describing a new form");
        writer.WriteLine("  export        Write the workbook of an existing form");
        writer.WriteLine("  import        Turn a workbook into FormSchema, TableSchema, FormLayout and registrations");
        writer.WriteLine();
        writer.WriteLine("Run 'dotnet polhem forms <subcommand> --help' for subcommand options.");
    }

    private static int Template(string[] args)
    {
        var options = Parse(args, "template", PrintTemplateHelp, valueOptions: ["--out"], flags: [], positionals: 0);
        if (options is null) return ExitCodes.Success;
        var output = Required(options, "--out", "<file.xlsx>");

        WriteFile(output, FormWorkbookWriter.WriteBlank);
        Console.WriteLine($"Wrote an empty form workbook to {Path.GetFullPath(output)}");
        return ExitCodes.Success;
    }

    private static int Export(string[] args)
    {
        var options = Parse(args, "export", PrintExportHelp, valueOptions: ["--define", "--out"], flags: [], positionals: 1);
        if (options is null) return ExitCodes.Success;
        var progId = options.Positionals.Count == 1
            ? options.Positionals[0]
            : throw new UsageException("missing required argument: <ProgId>");
        var define = Required(options, "--define", "<directory>");
        var output = options.Values.TryGetValue("--out", out var o) ? o : $"{progId}.xlsx";

        var content = FormExporter.Load(define, progId);
        WriteFile(output, stream => FormWorkbookWriter.Write(content, stream));
        Console.WriteLine($"Wrote the workbook of '{progId}' to {Path.GetFullPath(output)}");
        return ExitCodes.Success;
    }

    private static int Import(string[] args)
    {
        var options = Parse(args, "import", PrintImportHelp,
            valueOptions: ["--define"], flags: ["--regenerate-layout", "--dry-run"], positionals: 1);
        if (options is null) return ExitCodes.Success;
        var input = options.Positionals.Count == 1
            ? options.Positionals[0]
            : throw new UsageException("missing required argument: <file.xlsx>");
        var define = Required(options, "--define", "<directory>");
        bool dryRun = options.Flags.Contains("--dry-run");

        FormWorkbookReadResult read;
        using (var stream = File.OpenRead(input))
            read = FormWorkbookReader.Read(stream);
        PrintIssues(read.Issues);
        if (read.HasErrors)
        {
            Console.Error.WriteLine("error: the workbook has errors; nothing was written.");
            return ExitCodes.Error;
        }

        var plan = FormImporter.Plan(read.Content!, new FormImportOptions
        {
            DefinePath = define,
            RegenerateLayout = options.Flags.Contains("--regenerate-layout"),
        });
        PrintIssues(plan.Issues);
        if (plan.HasErrors)
        {
            Console.Error.WriteLine("error: the form has errors; nothing was written.");
            return ExitCodes.Error;
        }

        var root = Path.GetFullPath(define);
        foreach (var file in plan.Files)
            Console.WriteLine($"  {Symbol(file.Action)} {Path.GetRelativePath(root, file.Path)}  ({Describe(file)})");

        if (dryRun)
        {
            Console.WriteLine("Dry run: nothing was written.");
            return ExitCodes.Success;
        }
        FormImporter.Apply(plan);
        Console.WriteLine($"Imported '{read.Content!.Schema.ProgId}' into {root}: {plan.Files.Count(f => f.Writes)} file(s) written.");
        return ExitCodes.Success;
    }

    private static void PrintIssues(IEnumerable<DefinitionIssue> issues)
    {
        foreach (var issue in issues)
            Console.Error.WriteLine(issue.ToString());
    }

    private static string Symbol(PlannedFileAction action) => action switch
    {
        PlannedFileAction.Create => "+",
        PlannedFileAction.Overwrite => "!",
        PlannedFileAction.Update => "~",
        _ => "=",
    };

    private static string Describe(PlannedFile file) => file.Action switch
    {
        PlannedFileAction.Create => "create",
        PlannedFileAction.Overwrite => "overwrite",
        PlannedFileAction.Update => "add entries",
        PlannedFileAction.Unchanged => "unchanged",
        _ => file.Description,
    };

    private static void WriteFile(string path, Action<Stream> write)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        using var stream = File.Create(path);
        write(stream);
    }

    private static string Required(ParsedOptions options, string name, string placeholder)
        => options.Values.TryGetValue(name, out var value)
            ? value
            : throw new UsageException($"missing required option: {name} {placeholder}");

    /// <summary>
    /// Parses options and positional arguments. Returns <see langword="null"/> after printing help.
    /// </summary>
    private static ParsedOptions? Parse(
        string[] args, string subcommand, Action<TextWriter> printHelp,
        string[] valueOptions, string[] flags, int positionals)
    {
        var parsed = new ParsedOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "--help" or "-h")
            {
                printHelp(Console.Out);
                return null;
            }
            if (valueOptions.Contains(arg, StringComparer.Ordinal))
            {
                if (i + 1 >= args.Length) throw new UsageException($"{arg} requires a value");
                parsed.Values[arg] = args[++i];
            }
            else if (flags.Contains(arg, StringComparer.Ordinal))
            {
                parsed.Flags.Add(arg);
            }
            else if (!arg.StartsWith('-') && parsed.Positionals.Count < positionals)
            {
                parsed.Positionals.Add(arg);
            }
            else
            {
                throw new UsageException($"unknown option for '{subcommand}': '{arg}'");
            }
        }
        return parsed;
    }

    private static void PrintTemplateHelp(TextWriter writer)
    {
        writer.WriteLine("dotnet polhem forms template - Write an empty workbook for describing a new form");
        writer.WriteLine();
        writer.WriteLine("Usage: dotnet polhem forms template --out <file.xlsx>");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  --out <file>         The workbook to write. Required.");
        writer.WriteLine("  --help, -h           Print this help text.");
    }

    private static void PrintExportHelp(TextWriter writer)
    {
        writer.WriteLine("dotnet polhem forms export - Write the workbook of an existing form");
        writer.WriteLine();
        writer.WriteLine("Usage: dotnet polhem forms export <ProgId> --define <directory> [--out <file.xlsx>]");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  --define <dir>       The definition folder that holds FormSchema/. Required.");
        writer.WriteLine("  --out <file>         The workbook to write. Default: <ProgId>.xlsx");
        writer.WriteLine("  --help, -h           Print this help text.");
    }

    private static void PrintImportHelp(TextWriter writer)
    {
        writer.WriteLine("dotnet polhem forms import - Turn a workbook into definition files");
        writer.WriteLine();
        writer.WriteLine("Usage: dotnet polhem forms import <file.xlsx> --define <directory> [options]");
        writer.WriteLine();
        writer.WriteLine("Writes the FormSchema and a TableSchema per table, generates the FormLayout when there is none,");
        writer.WriteLine("and registers the form in DbCategorySettings, ProgramSettings, MenuSettings and the zh-TW");
        writer.WriteLine("language files. Nothing is written when the workbook or the form has an error.");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  --define <dir>       The definition folder to write to. Required.");
        writer.WriteLine("  --regenerate-layout  Replace an existing FormLayout. Default: keep it (it may be hand-tuned).");
        writer.WriteLine("  --dry-run            List the files that would change without writing them.");
        writer.WriteLine("  --help, -h           Print this help text.");
    }

    private sealed class ParsedOptions
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Flags { get; } = new(StringComparer.Ordinal);

        public List<string> Positionals { get; } = [];
    }
}
