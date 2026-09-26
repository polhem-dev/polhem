using Avalonia.Controls;
using Polhem.Base.Data;
using Polhem.UI.Avalonia.Controls.Editors;
using Avalonia.DemoCenter.Modules.DataEditors;

namespace Avalonia.DemoCenter.Modules.DataBinding
{
    /// <summary>
    /// Two-way sync: two editors bound to the same field. Editing either one writes through
    /// the data object and the other refreshes via <c>FieldValueChanged</c> — the data
    /// object is the single source of truth.
    /// </summary>
    public sealed class TwoWaySyncModule : DemoModuleBase
    {
        /// <inheritdoc/>
        public override string Category => "Data Binding";

        /// <inheritdoc/>
        public override string Title => "Two-way sync";

        /// <inheritdoc/>
        public override string Description =>
            "Two controls bound to the same field: type in one and leave it (or press Enter) to commit, and the other one and the values below update with it (FormDataObject is the single source of truth).";

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var data = DataEditorParts.SingleField("name", "Name", FieldDbType.String, initialValue: "Alice Chen");

            return DataEditorParts.Compose(
                data,
                DataEditorParts.Section(
                    "Two controls bound to the same field, name",
                    "Type in either box above and leave it (or press Enter); the other one updates.",
                    DataEditorParts.LabeledRow("Editor A", new TextEdit { FieldName = "name" }),
                    DataEditorParts.LabeledRow("Editor B", new TextEdit { FieldName = "name" })),
                DataEditorParts.Section(
                    "Live values",
                    null,
                    DataEditorParts.LiveValue(data, "name")));
        }
    }
}
