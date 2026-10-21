using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Polhem.UI.Avalonia.Themes
{
    /// <summary>The text brightness and weights of <see cref="VisualStudioTheme"/>.</summary>
    internal sealed partial class VisualStudioText : ResourceDictionary
    {
        /// <summary>Loads the XAML.</summary>
        public VisualStudioText()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
