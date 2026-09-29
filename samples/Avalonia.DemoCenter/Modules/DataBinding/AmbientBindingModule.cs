using Avalonia.Controls;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;
using Avalonia.DemoCenter.Modules.DataEditors;

namespace Avalonia.DemoCenter.Modules.DataBinding
{
    /// <summary>
    /// Ambient binding: the container sets the <see cref="FormScope"/> data object once and
    /// every descendant editor with a <c>FieldName</c> binds itself on attach.
    /// </summary>
    public sealed class AmbientBindingModule : DemoModuleBase
    {
        /// <inheritdoc/>
        public override string Category => "Data Binding";

        /// <inheritdoc/>
        public override string Title => "Ambient binding";

        /// <inheritdoc/>
        public override string Description =>
            "The container sets the DataObject once with FormScope.SetDataObject. Child editors only set FieldName and bind automatically on attach, with no per-editor wiring.";

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var data = BuildData();
            return DataEditorParts.Compose(
                data,
                DataEditorParts.Section(
                    "Set once on the container, child controls bind automatically",
                    "The root of this view calls FormScope.SetDataObject(data); the editors below only set FieldName.",
                    DataEditorParts.LabeledRow("name (TextEdit)", new TextEdit { FieldName = "name" }),
                    DataEditorParts.LabeledRow("dept (DropDownEdit)", new DropDownEdit { FieldName = "dept" })),
                DataEditorParts.Section(
                    "Live values",
                    null,
                    DataEditorParts.LiveValue(data, "name", "dept")));
        }

        private static FormDataObject BuildData()
        {
            var schema = new FormSchema("Demo", "Demo");
            var master = schema.Tables!.Add("Demo", "Demo");
            master.Fields!.Add("name", "Name", FieldDbType.String);
            var dept = master.Fields.Add("dept", "Department", FieldDbType.String);
            dept.ListItems!.Add("HR", "Human Resources");
            dept.ListItems.Add("IT", "Information Technology");
            dept.ListItems.Add("FIN", "Finance");

            var data = new FormDataObject(schema);
            data.InitializeNewMaster();
            data.SetField("name", "Alice Chen");
            data.SetField("dept", "IT");
            return data;
        }
    }
}
