using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Polhem.Web.Blazor.Shell;

namespace Polhem.Web.Blazor.Wasm.Shell
{
    /// <summary>
    /// Stores the viewer's language in the browser and reloads the page, which
    /// <see cref="DependencyInjection.PolhemShellServiceProviderExtensions.ApplyPolhemShellCulture"/> reads when it starts again. The reload
    /// is what loads the new language's satellite assemblies. Nobody is signed in yet, so it loses nothing.
    /// </summary>
    internal sealed class ShellLanguageSwitcher(IJSRuntime js, NavigationManager navigation) : IShellLanguageSwitcher
    {
        internal const string StorageKey = "Polhem.Shell.Culture";

        public void Switch(PolhemShellLanguage language)
        {
            ArgumentNullException.ThrowIfNull(language);
            ((IJSInProcessRuntime)js).InvokeVoid("localStorage.setItem", StorageKey, language.Culture);
            navigation.NavigateTo(navigation.Uri, forceLoad: true);
        }
    }
}
