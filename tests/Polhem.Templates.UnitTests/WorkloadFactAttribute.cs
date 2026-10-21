using System.Diagnostics;

namespace Polhem.Templates.UnitTests;

/// <summary>
/// A fact that runs only where the .NET workload a client head builds with is installed, and skips otherwise.
/// </summary>
/// <remarks>
/// CI installs no workload, so these builds run on a developer's machine only; CI still checks what the template
/// generates for those heads. The installed workloads are read once per test run with <c>dotnet workload list</c>.
/// </remarks>
public sealed class WorkloadFactAttribute : FactAttribute
{
    private static readonly Lazy<HashSet<string>> s_installed = new(ReadInstalledWorkloads);

    /// <param name="workloads">The workload ids, any one of which provides what the build needs.</param>
    public WorkloadFactAttribute(params string[] workloads)
    {
        if (!workloads.Any(s_installed.Value.Contains))
        {
            Skip = $"None of the workloads {string.Join(", ", workloads)} is installed.";
        }
        else if (workloads.Contains("ios") && !OperatingSystem.IsMacOS())
        {
            Skip = "The iOS head builds on macOS only.";
        }
    }

    private static HashSet<string> ReadInstalledWorkloads()
    {
        var startInfo = new ProcessStartInfo("dotnet", "workload list")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        // The test host runs under MSBuild's environment; the child command must resolve its own SDK.
        foreach (var name in startInfo.Environment.Keys
                     .Where(k => k.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            startInfo.Environment.Remove(name);
        }
        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        // Each installed workload is a line that starts with its id, followed by the manifest version.
        return output.Split('\n')
            .Select(line => line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length == 2 && char.IsAsciiDigit(parts[1].TrimStart()[0]))
            .Select(parts => parts[0])
            .ToHashSet(StringComparer.Ordinal);
    }
}
