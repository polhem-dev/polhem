using Avalonia.Controls;
using Polhem.UI.Avalonia.Controls;
using Avalonia.DemoCenter.Modules.DataEditors;
using Avalonia.DemoCenter.Modules.Views;

namespace Avalonia.DemoCenter.Modules.Grids
{
    /// <summary>
    /// In-cell editing: a <see cref="GridControl"/> bound to the Phones detail in
    /// <see cref="GridEditMode.InCell"/> mode — double-click a cell to edit it in place.
    /// </summary>
    public sealed class InCellEditModule : DemoModuleBase
    {
        /// <inheritdoc/>
        public override string Category => "Grid";

        /// <inheritdoc/>
        public override string Title => "In-cell editing";

        /// <inheritdoc/>
        public override string Description =>
            "GridControl InCell mode: double-click a cell (or press F2) to edit in place; drop-down, date and check cells swap in their editor on a single click. Built-in add / delete toolbar.";

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var data = SampleFormData.BuildMasterDetail(SampleFormData.BuildSchema());
            var grid = new GridControl { MinHeight = 240, EditMode = GridEditMode.InCell };
            grid.Bind(data, SampleFormData.BuildPhonesLayout());

            return new ScrollViewer
            {
                Content = DataEditorParts.Section(
                    "In-cell editing",
                    "Double-click a cell (or press F2) to edit; Enter or clicking elsewhere commits, Esc cancels.",
                    grid),
            };
        }
    }
}
