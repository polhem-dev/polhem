namespace Polhem.Cli;

/// <summary>
/// Thrown for argument-parsing / usage errors. Caught at the top of <c>Main</c>
/// and translated to a usage message + exit code 2.
/// </summary>
internal sealed class UsageException : Exception
{
    public UsageException(string message) : base(message) { }
}
