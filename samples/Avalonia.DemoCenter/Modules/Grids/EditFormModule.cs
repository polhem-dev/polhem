using Avalonia.Controls;
using Polhem.UI.Avalonia.Controls;
using Avalonia.DemoCenter.Modules.DataEditors;
using Avalonia.DemoCenter.Modules.Views;

namespace Avalonia.DemoCenter.Modules.Grids
{
    /// <summary>
    /// EditForm mode: a <see cref="GridControl"/> in <see cref="GridEditMode.EditForm"/> —
    /// the grid stays read-only and editing a row opens a popup form (editing strategy in
    /// <c>ADR-021</c>).
    /// </summary>
    public sealed class EditFormModule : DemoModuleBase
    {
        /// <inheritdoc/>
        public override string Category => "Grid";

        /// <inheritdoc/>
        public override string Title => "EditForm dialog";

        /// <inheritdoc/>
        public override string Description =>
            "GridControl EditForm mode: the grid is read-only; double-click a row or the toolbar Edit icon to edit the whole row in a dialog. Add opens the dialog for a new row, and cancelling removes the empty row.";

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var data = SampleFormData.BuildMasterDetail(SampleFormData.BuildSchema());
            var grid = new GridControl { MinHeight = 240, EditMode = GridEditMode.EditForm };
            grid.Bind(data, SampleFormData.BuildPhonesLayout());

            return new ScrollViewer
            {
                Content = DataEditorParts.Section(
                    "Editing in an EditForm dialog",
                    "Double-click a row or the Edit icon to open the dialog. Cancel fully restores the row; OK commits it and scrolls back to it.",
                    grid),
            };
        }
    }
}
