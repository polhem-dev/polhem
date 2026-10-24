using System.ComponentModel;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Polhem.UI.Avalonia.Themes;

namespace Polhem.UI.Avalonia.UnitTests.Themes
{
    public class VisualStudioThemeTests
    {
        [Fact]
        [DisplayName("Apply repaints a theme's palette brush in place with the VS Code colour")]
        public void Apply_RepaintsPaletteBrushInPlace()
        {
            // Stands in for Semi: a mutable palette brush that the theme's control keys would alias.
            var palette = new SolidColorBrush(Colors.Red);
            var semi = new Styles();
            semi.Resources["SemiColorPrimary"] = palette;
            var app = new Application();
            app.Styles.Add(semi);

            VisualStudioTheme.Apply(app);

            Assert.Equal(Color.Parse("#007ACC"), palette.Color);
        }

        [Fact]
        [DisplayName("Apply makes the VS Code colours resolvable from the application resources in each variant")]
        public void Apply_AddsColoursToApplicationResources()
        {
            var app = new Application();

            VisualStudioTheme.Apply(app);

            Assert.True(app.Resources.TryGetResource("SemiColorBackground0", ThemeVariant.Dark, out var dark));
            Assert.True(app.Resources.TryGetResource("SemiColorBackground0", ThemeVariant.Light, out var light));
            Assert.Equal(Color.Parse("#1E1E1E"), Assert.IsType<ISolidColorBrush>(dark, exactMatch: false).Color);
            Assert.Equal(Color.Parse("#FFFFFF"), Assert.IsType<ISolidColorBrush>(light, exactMatch: false).Color);
        }

        [Fact]
        [DisplayName("Apply leaves an application without Semi's palette unchanged apart from its resources")]
        public void Apply_WithoutPalette_DoesNotThrow()
        {
            var app = new Application();

            var exception = Record.Exception(() => VisualStudioTheme.Apply(app));

            Assert.Null(exception);
            Assert.Empty(app.Styles);
        }
    }
}
