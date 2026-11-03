namespace Polhem.UI.Avalonia.Shell
{
    /// <summary>
    /// A link the shell shows at the foot of its sign-in screen, such as to another front end of the same site.
    /// </summary>
    /// <param name="Text">The text of the link.</param>
    /// <param name="Address">The absolute address the link opens.</param>
    /// <remarks>
    /// The shell opens the address through the platform's launcher: the default browser on a desktop or a phone, and
    /// a new browser tab in the browser head.
    /// </remarks>
    public sealed record ShellLink(string Text, Uri Address);
}
