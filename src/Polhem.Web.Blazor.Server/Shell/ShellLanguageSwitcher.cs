using Microsoft.AspNetCore.Components;

namespace Polhem.Web.Blazor.Server.Shell
{
    /// <summary>
    /// Switches language through the endpoint of
    /// <see cref="PolhemShellEndpointRouteBuilderExtensions.MapPolhemShellCulture"/>, which reloads the page because a
    /// circuit's culture is fixed when it connects. Nobody is signed in yet, so the reload loses nothing.
    /// </summary>
    internal sealed class ShellLanguageSwitcher(NavigationManager navigation) : IShellLanguageSwitcher
    {
        public void Switch(PolhemShellLanguage language)
        {
            string returnPath = "/" + navigation.ToBaseRelativePath(navigation.Uri);
            navigation.NavigateTo(PolhemShellCulture.BuildSwitchUri(language.Culture, returnPath), forceLoad: true);
        }
    }
}
