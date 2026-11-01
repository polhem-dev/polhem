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
            // Captions are the definitions' and the language resources' text, shown as written.
            Typography = new Typography { Button = new ButtonTypography { TextTransform = "none" } },
        };
    }
}
