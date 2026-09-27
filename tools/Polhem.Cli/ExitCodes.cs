namespace Polhem.Cli;

/// <summary>
/// Centralised exit codes so callers (CI scripts) can rely on stable values.
/// </summary>
internal static class ExitCodes
{
    public const int Success = 0;
    public const int Error = 1;
    public const int Usage = 2;
}
