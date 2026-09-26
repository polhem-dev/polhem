using Avalonia.Controls;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.Controls.Editors;
using Avalonia.DemoCenter.Modules.DataEditors;
using Avalonia.DemoCenter.Modules.Views;

namespace Avalonia.DemoCenter.Modules.Grids
{
    /// <summary>
    /// Ambient grid binding: a <see cref="GridControl"/> with only <c>TableName</c> set binds
    /// itself through the ambient <see cref="FormScope"/> on attach and generates plain
    /// columns from the table.
    /// </summary>
    public sealed class AmbientGridModule : DemoModuleBase
    {
        /// <inheritdoc/>
        public override string Category => "Grid";

        /// <inheritdoc/>
        public override string Title => "Ambient binding";

        /// <inheritdoc/>
        public override string Description =>
            "Only TableName is set: the grid binds to the detail table through FormScope and its columns are generated from the table, with no Layout needed.";

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var data = SampleFormData.BuildMasterDetail(SampleFormData.BuildSchema());
            var grid = new GridControl { TableName = "Phones", MinHeight = 240 };

            var root = new ScrollViewer
            {
                Content = DataEditorParts.Section(
                    "Ambient binding (TableName only)",
                    "No Layout given; the columns are generated from the Phones table.",
                    grid),
            };
            FormScope.SetDataObject(root, data);
            return root;
        }
    }
}
