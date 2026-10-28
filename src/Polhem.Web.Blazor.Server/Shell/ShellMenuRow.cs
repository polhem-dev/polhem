namespace Polhem.Web.Blazor.Server.Shell
{
    /// <summary>
    /// One row of the shell's menu: a folder header when <see cref="ProgId"/> is <see langword="null"/>, otherwise a
    /// form to open.
    /// </summary>
    /// <param name="Caption">The text shown.</param>
    /// <param name="ProgId">The program the row opens, or <see langword="null"/> for a header.</param>
    internal sealed record ShellMenuRow(string Caption, string? ProgId);
}
