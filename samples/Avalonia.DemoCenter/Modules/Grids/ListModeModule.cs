using Avalonia.Controls;
using Polhem.UI.Avalonia.Controls;
using Avalonia.DemoCenter.Modules.DataEditors;
using Avalonia.DemoCenter.Modules.Views;

namespace Avalonia.DemoCenter.Modules.Grids
{
    /// <summary>
    /// List mode: a <see cref="GridControl"/> bound to a standalone <c>DataTable</c> (outside
    /// any data object), so the grid is read-only and the edit toolbar stays hidden.
    /// </summary>
    public sealed class ListModeModule : DemoModuleBase
    {
        /// <inheritdoc/>
        public override string Category => "Grid";

        /// <inheritdoc/>
        public override string Title => "List mode (read-only list)";

        /// <inheritdoc/>
        public override string Description =>
            "GridControl in list mode, bound to a standalone DataTable (not part of any FormDataObject): read-only, no add/delete toolbar, columns defined by a LayoutGrid.";

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var grid = new GridControl { MinHeight = 240 };
            grid.Bind(SampleFormData.BuildEmployeeListLayout(), SampleFormData.BuildEmployeeListTable());

            return new ScrollViewer
            {
                Content = DataEditorParts.Section(
                    "Employee list (list mode)",
                    "A list-mode grid is not editable and has no toolbar. The production ListView adds back-end reload and row events (see Avalonia.Demo).",
                    grid),
            };
        }
    }
}
