namespace Polhem.Web.Blazor.Shell
{
    /// <summary>
    /// A link the shell shows at the foot of its sign-in screen, such as to another front end of the same site.
    /// </summary>
    /// <param name="Text">The text of the link.</param>
    /// <param name="Href">
    /// The address, as an <c>href</c>: absolute, rooted (<c>/other/</c>) or relative to the base address.
    /// </param>
    public sealed record PolhemShellLink(string Text, string Href);
}
