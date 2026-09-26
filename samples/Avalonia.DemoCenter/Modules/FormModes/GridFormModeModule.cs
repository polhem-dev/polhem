using Avalonia.Controls;
using Avalonia.Media;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.Controls.Editors;
using Avalonia.DemoCenter.Modules.Views;

namespace Avalonia.DemoCenter.Modules.FormModes
{
    /// <summary>
    /// Grid × FormMode: the same detail <see cref="GridControl"/> shown under View / Add /
    /// Edit (each pinned via <see cref="FormScope.SetFormMode"/>). In View the grid is
    /// read-only and its add/delete toolbar is hidden; in Add / Edit it is editable with the
    /// toolbar shown.
    /// </summary>
    public sealed class GridFormModeModule : DemoModuleBase
    {
        /// <inheritdoc/>
        public override string Category => "FormMode States";

        /// <inheritdoc/>
        public override string Title => "Grid × FormMode";

        /// <inheritdoc/>
        public override string Description =>
            "The same detail GridControl in the three modes: View is read-only with the toolbar hidden; Add / Edit are editable and show the add/delete toolbar"
            + " (AllowEdit combined with AllowEditModes). Each section pins its mode through FormScope.";

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var stack = new StackPanel { Spacing = 16, Margin = new Thickness(4) };
            stack.Children.Add(Section(SingleFormMode.View, "View (read-only, toolbar hidden)"));
            stack.Children.Add(Section(SingleFormMode.Add, "Add (editable, toolbar shown)"));
            stack.Children.Add(Section(SingleFormMode.Edit, "Edit (editable, toolbar shown)"));
            return new ScrollViewer { Content = stack };
        }

        private static Border Section(SingleFormMode mode, string title)
        {
            var data = SampleFormData.BuildMasterDetail(SampleFormData.BuildSchema());

            var layout = new LayoutGrid("Phones", "Phones");
            layout.Columns!.Add(new LayoutColumn("phone", "Number", ControlType.TextEdit));
            layout.Columns.Add(new LayoutColumn("type", "Type", ControlType.DropDownEdit));
            layout.Columns.Add(new LayoutColumn("is_primary", "Primary", ControlType.CheckEdit));
            layout.Columns.Add(new LayoutColumn("valid_from", "Valid From", ControlType.DateEdit));

            var grid = new GridControl { MinHeight = 130 };
            grid.Bind(data, layout);

            var stack = new StackPanel { Spacing = 8 };
            stack.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeight.Bold });
            stack.Children.Add(grid);

            var card = new Border
            {
                Padding = new Thickness(12),
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gray,
                CornerRadius = new CornerRadius(4),
                Child = stack,
            };
            // Pin this section to its mode; the grid inherits it and ignores the toolbar.
            FormScope.SetFormMode(card, mode);
            return card;
        }
    }
}
