namespace Polhem.Web.Blazor.Server.Shell
{
    /// <summary>
    /// A <see cref="ShellLayoutBase"/> drawn with HTML, for a Polhem Blazor Server site.
    /// </summary>
    /// <remarks>
    /// Set it as the router's default layout. It reads the <see cref="PolhemShellOptions"/> of
    /// <see cref="PolhemShellServiceCollectionExtensions.AddPolhemShell"/> and the circuit's
    /// <see cref="Polhem.Web.Blazor.Identity.BlazorUserContext"/>. The sign-in belongs to the circuit, so a reload or a
    /// new tab shows the sign-in screen again. Signing out returns to it as well. The narrow width is 600 CSS pixels,
    /// the default compact threshold of the form page. The look comes from the package's stylesheet; see the package
    /// README.
    /// </remarks>
    public sealed partial class PolhemShellLayout : ShellLayoutBase
    {
    }
}
