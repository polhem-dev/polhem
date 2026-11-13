using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Core.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// The <c>FieldValue</c> surface of the drop-down, check and date editors, their row-scoped binding and unbinding,
    /// and the compact layout of the collection edit panel.
    /// </summary>
    public class EditorFieldValueTests
    {
        private static (FormDataObject DataObject, FormSchema Schema) CreateDataObject()
        {
            var schema = new FormSchema("Task", "Task");
            var master = schema.Tables!.Add("Task", "Task");
            master.Fields!.Add(new FormField(SysFields.RowId, "Row", FieldDbType.Guid));
            var detail = schema.Tables.Add("Step", "Steps");
            detail.Fields!.Add(new FormField(SysFields.RowId, "Row", FieldDbType.Guid));
            detail.Fields.Add(new FormField(SysFields.MasterRowId, "Task", FieldDbType.Guid));
            var status = detail.Fields.Add("status", "Status", FieldDbType.String);
            status.ListItems!.Add("O", "Open");
            status.ListItems.Add("C", "Closed");
            detail.Fields.Add(new FormField("done", "Done", FieldDbType.Boolean));
            detail.Fields.Add(new FormField("due", "Due", FieldDbType.Date));
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return (dataObject, schema);
        }

        private static (FormDataObject DataObject, LayoutGrid Layout, System.Data.DataRow Row) CreateStep()
        {
            var (dataObject, schema) = CreateDataObject();
            var steps = dataObject.DataSet.Tables["Step"]!;
            var row = steps.NewRow();
            row[SysFields.RowId] = Guid.NewGuid();
            row["status"] = "C";
            row["done"] = true;
            row["due"] = new DateTime(2026, 7, 8, 0, 0, 0, DateTimeKind.Unspecified);
            steps.Rows.Add(row);
            return (dataObject, FormLayoutGenerator.Generate(schema, "default").Details![0], row);
        }

        private static LayoutColumn ColumnOf(LayoutGrid layout, string fieldName)
            => layout.Columns!.First(c => c.FieldName == fieldName);

        [Fact]
        [DisplayName("A drop-down bound to a detail row shows that row's value and reports it as its field value")]
        public void DropDownEdit_BindRow_ReportsRowValue()
        {
            var (dataObject, layout, row) = CreateStep();
            var editor = new DropDownEdit();

            editor.Bind(dataObject, ColumnOf(layout, "status"), row);

            Assert.Equal("C", editor.FieldValue);
        }

        [Fact]
        [DisplayName("Setting a drop-down's field value selects the matching item, and an unknown value selects nothing")]
        public void DropDownEdit_SetFieldValue_SelectsMatchingItem()
        {
            var (dataObject, layout, row) = CreateStep();
            var editor = new DropDownEdit();
            editor.Bind(dataObject, ColumnOf(layout, "status"), row);
            editor.Unbind();

            editor.FieldValue = "O";
            Assert.Equal("O", editor.FieldValue);
            editor.FieldValue = "missing";

            Assert.Null(editor.FieldValue);
            Assert.Equal("C", row["status"]);
        }

        [Fact]
        [DisplayName("A check editor bound to a detail row reports the row's flag as its field value")]
        public void CheckEdit_BindRow_ReportsRowValue()
        {
            var (dataObject, layout, row) = CreateStep();
            var editor = new CheckEdit();

            editor.Bind(dataObject, ColumnOf(layout, "done"), row);

            Assert.Equal(true, editor.FieldValue);
        }

        [Fact]
        [DisplayName("An unchecked check editor reports false, never null")]
        public void CheckEdit_Unset_ReportsFalse()
        {
            var editor = new CheckEdit { IsChecked = null };

            Assert.Equal(false, editor.FieldValue);
        }

        [Fact]
        [DisplayName("A date editor bound to a detail row reports the row's date, and after unbinding no longer follows it")]
        public void DateEdit_BindRowThenUnbind_StopsFollowing()
        {
            var (dataObject, layout, row) = CreateStep();
            var editor = new DateEdit();
            editor.Bind(dataObject, ColumnOf(layout, "due"), row);
            Assert.Equal(new DateTime(2026, 7, 8, 0, 0, 0, DateTimeKind.Unspecified), ((DateTimeOffset)editor.FieldValue!).Date);

            editor.Unbind();
            row["due"] = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

            Assert.Equal(new DateTime(2026, 7, 8, 0, 0, 0, DateTimeKind.Unspecified), ((DateTimeOffset)editor.FieldValue!).Date);
        }

        [Fact]
        [DisplayName("A compact collection edit panel stacks the item list above the item grid")]
        public void CollectionEditPanel_Compact_StacksListAboveGrid()
        {
            var items = new ListItemCollection { { "A", "Active" } };

            var panel = new CollectionEditPanel(new CollectionEditSession(items, typeof(ListItem)), null, compact: true);

            Assert.Equal(1, global::Avalonia.Controls.Grid.GetRow(panel.ItemGrid));
            Assert.False(double.IsNaN(panel.ItemGrid.Height));
        }
    }
}
