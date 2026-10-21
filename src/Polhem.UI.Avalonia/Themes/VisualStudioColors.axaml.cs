using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Polhem.UI.Avalonia.Themes
{
    /// <summary>The Semi palette keys of <see cref="VisualStudioTheme"/>, in its light and dark variants.</summary>
    internal sealed partial class VisualStudioColors : ResourceDictionary
    {
        /// <summary>Loads the XAML.</summary>
        public VisualStudioColors()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
