using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Polhem.UI.Avalonia.Controls.Editors;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// <see cref="ReadOnlyFieldVisual.Build"/>: the underlined display that follows its text, and the hidden parts it
    /// registers so the native control's template code finds them.
    /// </summary>
    public class ReadOnlyFieldVisualTests
    {
        private static IObservable<string?> Text(string? value) => new TextBlock { Text = value }.GetObservable(TextBlock.TextProperty);

        [Fact]
        [DisplayName("Build returns a label over a single bottom line drawn with the shared underline brush")]
        public void Build_Plain_ReturnsUnderlinedLabel()
        {
            var root = Assert.IsType<Border>(ReadOnlyFieldVisual.Build(Text("first"), new NameScope(), ReadOnlyFieldVisual.HostKind.Plain));

            Assert.IsType<TextBlock>(root.Child);
            Assert.Same(ReadOnlyFieldVisual.UnderlineBrush, root.BorderBrush);
            Assert.Equal(new Thickness(0, 0, 0, 1), root.BorderThickness);
        }

        [Fact]
        [DisplayName("A plain host registers no parts")]
        public void Build_Plain_RegistersNothing()
        {
            var scope = new NameScope();

            ReadOnlyFieldVisual.Build(Text(null), scope, ReadOnlyFieldVisual.HostKind.Plain);

            Assert.Null(scope.Find("PART_Popup"));
        }

        [Fact]
        [DisplayName("A combo box host registers the popup part its template code requires")]
        public void Build_ComboBox_RegistersPopup()
        {
            var scope = new NameScope();

            ReadOnlyFieldVisual.Build(Text("x"), scope, ReadOnlyFieldVisual.HostKind.ComboBox);

            Assert.IsType<Popup>(scope.Find("PART_Popup"));
        }

        [Fact]
        [DisplayName("A date picker host registers every part its template code dereferences, outside the visual tree")]
        public void Build_DatePicker_RegistersDatePickerParts()
        {
            var scope = new NameScope();

            var root = ReadOnlyFieldVisual.Build(Text("x"), scope, ReadOnlyFieldVisual.HostKind.DatePicker);

            var grid = Assert.IsType<Grid>(scope.Find("PART_ButtonContentGrid"));
            Assert.Same(grid, Assert.IsType<TextBlock>(scope.Find("PART_DayTextBlock")).Parent);
            Assert.IsType<TextBlock>(scope.Find("PART_MonthTextBlock"));
            Assert.IsType<TextBlock>(scope.Find("PART_YearTextBlock"));
            Assert.IsType<Rectangle>(scope.Find("PART_FirstSpacer"));
            Assert.IsType<Rectangle>(scope.Find("PART_SecondSpacer"));
            Assert.IsType<Button>(scope.Find("PART_FlyoutButton"));
            Assert.IsType<Popup>(scope.Find("PART_Popup"));
            Assert.IsType<TextBlock>(Assert.IsType<Border>(root).Child);
        }
    }
}
