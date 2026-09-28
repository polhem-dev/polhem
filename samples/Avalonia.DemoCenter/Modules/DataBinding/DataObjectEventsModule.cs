using Avalonia.Controls;
using Avalonia.Layout;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.Controls.Editors;
using Avalonia.DemoCenter.Modules.DataEditors;
using Avalonia.DemoCenter.Modules.Views;

namespace Avalonia.DemoCenter.Modules.DataBinding
{
    /// <summary>
    /// The <c>FormDataObject</c> events, all surfaced in one live log:
    /// <list type="bullet">
    /// <item><c>FieldValueChanged</c> — a master or detail field value changed (detail edits
    /// flow through the DataTable event bridge); args carry TableName / FieldName / Value / Row.</item>
    /// <item><c>RowAdded</c> / <c>RowDeleted</c> — a row was added / deleted (e.g. the detail
    /// grid's add / delete toolbar); args carry TableName / Row.</item>
    /// <item><c>IsDirtyChanged</c> — IsDirty transitioned (clean ↔ dirty).</item>
    /// <item><c>DataSetReplaced</c> — the dataset was replaced or its content reset (e.g.
    /// <c>InitializeNewMaster()</c>); bound editors re-pull.</item>
    /// </list>
    /// </summary>
    public sealed class DataObjectEventsModule : DemoModuleBase
    {
        private const string EmptyLog = "(No events yet — edit a field or a detail cell, or press the button below)";

        /// <inheritdoc/>
        public override string Category => "Data Binding";

        /// <inheritdoc/>
        public override string Title => "DataObject events";

        /// <inheritdoc/>
        public override string Description =>
            "FormDataObject events: FieldValueChanged (a field changes), RowAdded / RowDeleted (a detail row is added or deleted), "
            + "IsDirtyChanged (dirty/clean flips), DataSetReplaced (the DataSet is replaced or reset). The log below shows them live.";

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var data = SampleFormData.BuildMasterDetail(SampleFormData.BuildSchema());

            var log = new TextBlock { Text = EmptyLog, Opacity = 0.85 };
            var lines = new List<string>();
            var seq = 0;
            void Append(string entry)
            {
                seq++;
                lines.Insert(0, $"{seq:D2}  {entry}");
                if (lines.Count > 15)
                    lines.RemoveAt(lines.Count - 1);
                log.Text = string.Join(Environment.NewLine, lines);
            }

            // Subscribe after the seed writes in BuildMasterDetail so the log starts empty.
            data.FieldValueChanged += (_, e) => Append($"FieldValueChanged   {e.TableName}.{e.FieldName} = {e.Value}");
            data.RowAdded += (_, e) => Append($"RowAdded            {e.TableName}");
            data.RowDeleted += (_, e) => Append($"RowDeleted          {e.TableName}");
            data.IsDirtyChanged += (_, _) => Append($"IsDirtyChanged      IsDirty = {data.IsDirty}");
            data.DataSetReplaced += (_, _) => Append("DataSetReplaced     (DataSet replaced / contents reset)");

            var grid = new GridControl { MinHeight = 150, EditMode = GridEditMode.InCell };
            grid.Bind(data, SampleFormData.BuildPhonesLayout());

            var reinitButton = new Button { Content = "Reinitialize the master — InitializeNewMaster()", HorizontalAlignment = HorizontalAlignment.Left };
            reinitButton.Click += (_, _) => data.InitializeNewMaster();

            return DataEditorParts.Compose(
                data,
                DataEditorParts.Section(
                    "Master (Staff)",
                    "Edit the fields below → FieldValueChanged.",
                    DataEditorParts.LabeledRow("emp_name", new TextEdit { FieldName = "emp_name" }),
                    DataEditorParts.LabeledRow("dept", new DropDownEdit { FieldName = "dept" }),
                    DataEditorParts.LabeledRow("is_active", new CheckEdit { FieldName = "is_active", Content = "Active" })),
                DataEditorParts.Section(
                    "Detail (Phones)",
                    "Double-click a cell to edit → FieldValueChanged (bridged through the DataTable); grid toolbar add / delete row → RowAdded / RowDeleted.",
                    grid),
                DataEditorParts.Section(
                    "Triggering DataSetReplaced",
                    "InitializeNewMaster() resets the master and raises DataSetReplaced; the master fields above re-pull and clear.",
                    reinitButton),
                DataEditorParts.Section(
                    "Event log (newest first)",
                    "All FormDataObject events are shown here.",
                    log));
        }
    }
}
