namespace Polhem.Web.Blazor.Server.Shell
{
    /// <summary>
    /// A language the shell offers on its sign-in screen.
    /// </summary>
    /// <param name="Culture">The culture name, such as <c>en-US</c> or <c>zh-TW</c>.</param>
    /// <param name="DisplayName">The name shown on the switch, written in the language itself.</param>
    public sealed record PolhemShellLanguage(string Culture, string DisplayName);
}
