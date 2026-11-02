namespace Polhem.Web.Blazor.Shell
{
    /// <summary>
    /// Changes the language of the site, the part of the shell's sign-in screen that depends on the host.
    /// </summary>
    /// <remarks>
    /// Blazor Server fixes a circuit's culture when it connects, so its implementation reloads the page through an
    /// endpoint. WebAssembly stores the choice in the browser and reloads the page. A host that registers none gets no
    /// language switch.
    /// </remarks>
    public interface IShellLanguageSwitcher
    {
        /// <summary>Switches to <paramref name="language"/>.</summary>
        /// <param name="language">One of <see cref="PolhemShellOptions.Languages"/>.</param>
        void Switch(PolhemShellLanguage language);
    }
}
