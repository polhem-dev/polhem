using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Polhem.UI.Avalonia.Controls.Editors;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// Behaviour checks for <see cref="ConfirmPanel"/>: which answer each button and key gives.
    /// </summary>
    public class ConfirmPanelTests
    {
        private static (ConfirmPanel Panel, List<string> Answers) Build()
        {
            var panel = new ConfirmPanel("Delete this record?", "Delete", "Cancel");
            var answers = new List<string>();
            panel.Confirmed += (_, _) => answers.Add("confirmed");
            panel.Cancelled += (_, _) => answers.Add("cancelled");
            return (panel, answers);
        }

        private static Button FindButton(ConfirmPanel panel, string caption)
        {
            var body = Assert.IsType<DockPanel>(panel.Content);
            var buttons = Assert.IsType<StackPanel>(body.Children[0]);
            return buttons.Children.OfType<Button>().Single(b => Equals(b.Content, caption));
        }

        [Fact]
        [DisplayName("The confirm button answers Confirmed")]
        public void ConfirmButton_Click_RaisesConfirmed()
        {
            var (panel, answers) = Build();

            FindButton(panel, "Delete").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal(["confirmed"], answers);
        }

        [Fact]
        [DisplayName("The cancel button answers Cancelled")]
        public void CancelButton_Click_RaisesCancelled()
        {
            var (panel, answers) = Build();

            FindButton(panel, "Cancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal(["cancelled"], answers);
        }

        [Fact]
        [DisplayName("Escape answers Cancelled")]
        public void Escape_RaisesCancelled()
        {
            var (panel, answers) = Build();

            panel.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });

            Assert.Equal(["cancelled"], answers);
        }
    }
}
