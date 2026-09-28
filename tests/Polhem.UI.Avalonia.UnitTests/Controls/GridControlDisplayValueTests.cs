using System.ComponentModel;
using System.Data;
using System.Globalization;
using Polhem.Base.Data;
using Polhem.Definition;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Tests.Shared;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests.Controls
{
    /// <summary>
    /// The text <see cref="GridControl"/> shows for values whose stored form is not what a person reads:
    /// a drop-down's code, a Boolean outside a check-box column and an instant stored at midnight. The
    /// compact card list of the list view reads the same method, so these cover both surfaces.
    /// </summary>
    public class GridControlDisplayValueTests
    {
        private static FormTable BuildModeTable()
        {
            var table = new FormTable("AuditRule", "Audit Rule");
            var mode = table.Fields!.Add("change_mode", "Change Log", FieldDbType.Integer);
            mode.ControlType = ControlType.DropDownEdit;
            mode.ListItems!.Add("0", "Inherit");
            mode.ListItems.Add("1", "On");
            mode.ListItems.Add("2", "Off");
            return table;
        }

        private static DataTable BuildRows(Type type, object value)
        {
            var rows = new DataTable("AuditRule");
            rows.Columns.Add("value", type);
            rows.Rows.Add(value);
            return rows;
        }

        private static LayoutColumn Column(string fieldName, ControlType controlType)
            => new(fieldName, fieldName, controlType);

        [Fact]
        [DisplayName("A list-mode drop-down column shows the list item text of the schema table it was bound with")]
        public void FormatColumnText_ListModeDropDown_ShowsListItemText()
        {
            var rows = new DataTable("AuditRule");
            rows.Columns.Add("change_mode", typeof(int));
            rows.Rows.Add(2);
            var layout = new LayoutGrid("AuditRule", "Audit Rule");
            var column = Column("change_mode", ControlType.DropDownEdit);
            layout.Columns!.Add(column);
            var grid = new GridControl();

            grid.Bind(layout, rows, BuildModeTable());

            Assert.Equal("Off", grid.FormatColumnText(rows.DefaultView[0], column));
        }

        [Fact]
        [DisplayName("A list-mode drop-down column bound without a schema table still shows the stored value")]
        public void FormatColumnText_ListModeWithoutFormTable_ShowsStoredValue()
        {
            var rows = new DataTable("AuditRule");
            rows.Columns.Add("change_mode", typeof(int));
            rows.Rows.Add(2);
            var layout = new LayoutGrid("AuditRule", "Audit Rule");
            var column = Column("change_mode", ControlType.DropDownEdit);
            layout.Columns!.Add(column);
            var grid = new GridControl();

            grid.Bind(layout, rows);

            Assert.Equal("2", grid.FormatColumnText(rows.DefaultView[0], column));
        }

        [Fact]
        [DisplayName("A drop-down value no list item declares shows the stored value rather than nothing")]
        public void FormatColumnText_UndeclaredValue_ShowsStoredValue()
        {
            var rows = new DataTable("AuditRule");
            rows.Columns.Add("change_mode", typeof(int));
            rows.Rows.Add(9);
            var layout = new LayoutGrid("AuditRule", "Audit Rule");
            var column = Column("change_mode", ControlType.DropDownEdit);
            layout.Columns!.Add(column);
            var grid = new GridControl();

            grid.Bind(layout, rows, BuildModeTable());

            Assert.Equal("9", grid.FormatColumnText(rows.DefaultView[0], column));
        }

        [Fact]
        [DisplayName("A detail-grid drop-down column shows the list item text through the bound data object's schema")]
        public void FormatColumnText_DetailDropDown_ShowsListItemText()
        {
            var schema = new FormSchema("Rule", "Rule");
            var master = schema.Tables!.Add("Rule", "Rule");
            master.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            var detail = schema.Tables.Add("RuleItem", "Rule Item");
            detail.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            detail.Fields.Add(SysFields.MasterRowId, "Master", FieldDbType.Guid);
            var mode = detail.Fields.Add("mode", "Mode", FieldDbType.Integer);
            mode.ControlType = ControlType.DropDownEdit;
            mode.ListItems!.Add("1", "On");
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            var detailTable = dataObject.DataSet.Tables["RuleItem"]!;
            var row = detailTable.NewRow();
            row["mode"] = 1;
            detailTable.Rows.Add(row);

            var layout = new LayoutGrid("RuleItem", "Rule Item");
            var column = Column("mode", ControlType.DropDownEdit);
            layout.Columns!.Add(column);
            var grid = new GridControl();
            grid.Bind(dataObject, layout);

            Assert.Equal("On", grid.FormatColumnText(detailTable.DefaultView[0], column));
        }

        [Fact]
        [DisplayName("A Boolean outside a check-box column reads as the localized Yes or No, not True or False")]
        public void FormatColumnText_BooleanInTextColumn_ShowsLocalizedYesNo()
        {
            using var culture = new CultureScope("en-US");
            var rows = BuildRows(typeof(bool), true);
            var layout = new LayoutGrid("AuditRule", "Audit Rule");
            var column = Column("value", ControlType.TextEdit);
            layout.Columns!.Add(column);
            var grid = new GridControl();
            grid.Bind(layout, rows);

            Assert.Equal(UIText.Get(PolhemUIText.True), grid.FormatColumnText(rows.DefaultView[0], column));
            Assert.NotEqual(bool.TrueString, grid.FormatColumnText(rows.DefaultView[0], column));
        }

        [Fact]
        [DisplayName("A date-time column shows the time of day even for an instant stored at midnight")]
        public void FormatColumnText_DateTimeColumnAtMidnight_ShowsTime()
        {
            using var culture = new CultureScope("en-US");
            var midnight = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Unspecified);
            var rows = BuildRows(typeof(DateTime), midnight);
            var layout = new LayoutGrid("AuditRule", "Audit Rule");
            var column = Column("value", ControlType.DateTimeEdit);
            layout.Columns!.Add(column);
            var grid = new GridControl();
            grid.Bind(layout, rows);

            Assert.Equal(midnight.ToString("G", CultureInfo.CurrentCulture), grid.FormatColumnText(rows.DefaultView[0], column));
        }
    }
}
