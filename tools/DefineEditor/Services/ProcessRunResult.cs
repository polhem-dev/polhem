namespace Polhem.DefineEditor.Services;

/// <summary>How a run of <see cref="ProcessRunner"/> ended.</summary>
/// <param name="ExitCode">The exit code, or <see langword="null"/> when the process did not start or was cancelled.</param>
/// <param name="StartError">Why the process could not start, or <see langword="null"/> when it started.</param>
/// <param name="Cancelled">True when the run was cancelled and the process was killed.</param>
public sealed record ProcessRunResult(int? ExitCode, string? StartError, bool Cancelled)
{
    /// <summary>True when the process ran to the end with exit code 0.</summary>
    public bool Succeeded => ExitCode == 0;
}
