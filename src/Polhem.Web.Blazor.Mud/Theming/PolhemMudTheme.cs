using MudBlazor;

namespace Polhem.Web.Blazor.Mud.Theming
{
    /// <summary>
    /// The theme <see cref="PolhemMudProviders"/> applies unless it is given another, light and dark palettes included.
    /// </summary>
    public static class PolhemMudTheme
    {
        /// <summary>
        /// Creates the default theme.
        /// </summary>
        /// <returns>A new theme.</returns>
        public static MudTheme CreateDefault() => new()
        {
            LayoutProperties = new LayoutProperties { DefaultBorderRadius = "4px" },
        };
    }
}
