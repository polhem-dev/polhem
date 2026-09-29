using Avalonia.Controls;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;
using Avalonia.DemoCenter.Modules.DataEditors;

namespace Avalonia.DemoCenter.Modules.ControlTypes
{
    /// <summary>
    /// A one-of-each gallery: every field-editor <c>ControlType</c> bound to a field through
    /// the ambient <see cref="FormScope"/>, with a live value readout. The quickest way to
    /// see the inherited controls and what they write back.
    /// </summary>
    public sealed class ControlGalleryModule : DemoModuleBase
    {
        private static readonly string[] s_fields =
            ["name", "notes", "code", "hire_date", "pay_month", "dept", "active"];

        /// <inheritdoc/>
        public override string Category => "Control Types";

        /// <inheritdoc/>
        public override string Title => "Control gallery";

        /// <inheritdoc/>
        public override string Description =>
            "One derived control per ControlType, bound through the FormScope ambient scope. Text controls update the values below when you leave them (or press Enter); "
            + "drop-downs, check boxes and date pickers update them on selection.";

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var data = BuildDataObject();

            var editors = DataEditorParts.Section(
                "Derived controls",
                "Each control is bound to one field (editable in the default Edit mode). For switching FormMode, see the FormMode States theme.",
                DataEditorParts.LabeledRow("TextEdit", new TextEdit { FieldName = "name" }),
                DataEditorParts.LabeledRow("MemoEdit", new MemoEdit { FieldName = "notes", MinHeight = 56 }),
                DataEditorParts.LabeledRow("ButtonEdit", new ButtonEdit { FieldName = "code" }),
                DataEditorParts.LabeledRow("DateEdit", new DateEdit { FieldName = "hire_date" }),
                DataEditorParts.LabeledRow("YearMonthEdit", new YearMonthEdit { FieldName = "pay_month" }),
                DataEditorParts.LabeledRow("DropDownEdit", new DropDownEdit { FieldName = "dept" }),
                DataEditorParts.LabeledRow("CheckEdit", new CheckEdit { FieldName = "active", Content = "Active" }));

            var values = DataEditorParts.Section(
                "FormDataObject live field values",
                null,
                DataEditorParts.LiveValue(data, s_fields));

            return DataEditorParts.Compose(data, editors, values);
        }

        private static FormDataObject BuildDataObject()
        {
            var schema = new FormSchema("Gallery", "Gallery");
            var master = schema.Tables!.Add("Gallery", "Gallery");
            var name = master.Fields!.Add("name", "Name", FieldDbType.String);
            name.MaxLength = 20;
            master.Fields.Add("notes", "Notes", FieldDbType.String);
            master.Fields.Add("code", "Code", FieldDbType.String);
            master.Fields.Add("hire_date", "Hire Date", FieldDbType.Date);
            master.Fields.Add("pay_month", "Pay Month", FieldDbType.String);
            var dept = master.Fields.Add("dept", "Department", FieldDbType.String);
            dept.ListItems!.Add("HR", "Human Resources");
            dept.ListItems.Add("IT", "Information Technology");
            dept.ListItems.Add("FIN", "Finance");
            master.Fields.Add("active", "Active", FieldDbType.Boolean);

            var data = new FormDataObject(schema);
            data.InitializeNewMaster();
            data.SetField("name", "Alice Chen");
            data.SetField("notes", "Memo");
            data.SetField("code", "EMP-001");
            data.SetField("hire_date", "2026-06-11");
            data.SetField("pay_month", "2026-06");
            data.SetField("dept", "IT");
            data.SetField("active", bool.TrueString);
            return data;
        }
    }
}
