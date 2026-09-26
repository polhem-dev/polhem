using Avalonia.Controls;
using Avalonia.Media;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls;
using Avalonia.DemoCenter.Modules.Views;

namespace Avalonia.DemoCenter.Modules.ReadOnlyRequired
{
    /// <summary>
    /// Required / read-only marking: a <see cref="GridControl"/> whose column headers are
    /// coloured by field state — read-only brown, required blue (read-only wins) — via the
    /// library's shared caption-colour convention.
    /// </summary>
    public sealed class RequiredReadOnlyMarkModule : DemoModuleBase
    {
        /// <inheritdoc/>
        public override string Category => "Read-only & Required";

        /// <inheritdoc/>
        public override string Title => "Required / read-only markers";

        /// <inheritdoc/>
        public override string Description =>
            "GridControl marks column state with the header text color: read-only = brown, required = blue (read-only wins). A glance at the headers shows each column's state.";

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var data = SampleFormData.BuildMasterDetail(SampleFormData.BuildSchema());

            var layout = new LayoutGrid("Phones", "Phones");
            layout.Columns!.Add(new LayoutColumn("phone", "Number (read-only)", ControlType.TextEdit) { ReadOnly = true });
            layout.Columns.Add(new LayoutColumn("type", "Type (required)", ControlType.DropDownEdit) { Required = true });
            layout.Columns.Add(new LayoutColumn("is_primary", "Primary", ControlType.CheckEdit));
            layout.Columns.Add(new LayoutColumn("valid_from", "Valid From", ControlType.DateEdit));

            var grid = new GridControl { MinHeight = 180 };
            grid.Bind(data, layout);

            var stack = new StackPanel { Spacing = 8, Margin = new Thickness(4) };
            stack.Children.Add(new TextBlock { Text = "Column state shown by header color", FontSize = 15, FontWeight = FontWeight.Bold });
            stack.Children.Add(new TextBlock
            {
                Text = "Number is read-only → brown header; Type is required → blue header; the others use the default color.",
                FontSize = 12,
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
            });
            stack.Children.Add(grid);
            return new ScrollViewer { Content = stack };
        }
    }
}
