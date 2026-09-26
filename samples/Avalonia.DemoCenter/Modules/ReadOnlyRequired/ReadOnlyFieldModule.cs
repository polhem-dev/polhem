using Avalonia.Controls;
using Polhem.Base.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;
using Avalonia.DemoCenter.Modules.DataEditors;

namespace Avalonia.DemoCenter.Modules.ReadOnlyRequired
{
    /// <summary>
    /// Per-field read-only via <c>LayoutField.ReadOnly</c>: a read-only field renders with
    /// the "de-framed, underline-only" appearance (CheckEdit greys the box but keeps the
    /// label readable), independent of the form's FormMode.
    /// </summary>
    public sealed class ReadOnlyFieldModule : DemoModuleBase
    {
        /// <inheritdoc/>
        public override string Category => "Read-only & Required";

        /// <inheritdoc/>
        public override string Title => "LayoutField.ReadOnly";

        /// <inheritdoc/>
        public override string Description =>
            "LayoutField.ReadOnly=true makes a field permanently read-only: the border is removed and an underline remains, CheckEdit gets a grey box and keeps its text; it stays read-only whatever the FormMode.";

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var data = BuildData();

            return DataEditorParts.Compose(
                data,
                DataEditorParts.Section(
                    "Normal (editable according to FormMode)",
                    "Ambient binding, editable in the default Edit mode (for read-only driven by FormMode, see the FormMode States theme).",
                    DataEditorParts.LabeledRow("name", new TextEdit { FieldName = "name" }),
                    DataEditorParts.LabeledRow("hire_date", new DateEdit { FieldName = "hire_date" }),
                    DataEditorParts.LabeledRow("dept", new DropDownEdit { FieldName = "dept" }),
                    DataEditorParts.LabeledRow("active", new CheckEdit { FieldName = "active", Content = "Active" })),
                DataEditorParts.Section(
                    "Read-only (LayoutField.ReadOnly=true)",
                    "Permanently read-only: text, date and drop-down editors lose their border and keep an underline; CheckEdit gets a grey box and keeps its text.",
                    DataEditorParts.LabeledRow("name", ReadOnly(new TextEdit(), data, "name")),
                    DataEditorParts.LabeledRow("hire_date", ReadOnly(new DateEdit(), data, "hire_date")),
                    DataEditorParts.LabeledRow("dept", ReadOnly(new DropDownEdit(), data, "dept")),
                    DataEditorParts.LabeledRow("active", ReadOnlyCheck(data, "active"))));
        }

        private static T ReadOnly<T>(T editor, FormDataObject data, string field)
            where T : Control, IFieldEditor
        {
            editor.Bind(data, new LayoutField { FieldName = field, ReadOnly = true });
            return editor;
        }

        private static CheckEdit ReadOnlyCheck(FormDataObject data, string field)
        {
            var editor = new CheckEdit { Content = "Active" };
            editor.Bind(data, new LayoutField { FieldName = field, ReadOnly = true });
            return editor;
        }

        private static FormDataObject BuildData()
        {
            var schema = new FormSchema("Demo", "Demo");
            var master = schema.Tables!.Add("Demo", "Demo");
            master.Fields!.Add("name", "Name", FieldDbType.String);
            master.Fields.Add("hire_date", "Hire Date", FieldDbType.Date);
            var dept = master.Fields.Add("dept", "Department", FieldDbType.String);
            dept.ListItems!.Add("HR", "Human Resources");
            dept.ListItems.Add("IT", "Information Technology");
            dept.ListItems.Add("FIN", "Finance");
            master.Fields.Add("active", "Active", FieldDbType.Boolean);

            var data = new FormDataObject(schema);
            data.InitializeNewMaster();
            data.SetField("name", "Alice Chen");
            data.SetField("hire_date", "2026-06-11");
            data.SetField("dept", "IT");
            data.SetField("active", bool.TrueString);
            return data;
        }
    }
}
