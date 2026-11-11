using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Tests.Shared;
using Polhem.UI.Core.Fields;

namespace Polhem.UI.Core.UnitTests.Fields
{
    /// <summary>
    /// Tests the cell text rules of <see cref="GridCellFormatter"/> and the caption rules of
    /// <see cref="FieldCaptionRules"/>, which the grids of both heads share.
    /// </summary>
    public class GridCellFormatterTests
    {
        private static readonly Func<bool, string> s_booleanText = flag => flag ? "Yes" : "No";

        private static DataRow BuildRow()
        {
            var table = new DataTable("Line");
            table.Columns.Add("status", typeof(string));
            table.Columns.Add("active", typeof(bool));
            table.Columns.Add("amount", typeof(decimal));
            table.Columns.Add("ref_id", typeof(string));
            table.Columns.Add("ref_name", typeof(string));
            return table.Rows.Add("A", true, 1234.5m, "D01", "Engineering");
        }

        [Fact]
        [DisplayName("A drop-down column shows its list item's text")]
        public void FormatCell_DropDown_ShowsItemText()
        {
            var field = new FormField("status", "Status", FieldDbType.String);
            field.ListItems!.Add("A", "Active");

            var text = GridCellFormatter.FormatCell(BuildRow(), new LayoutColumn("status", "Status", ControlType.DropDownEdit),
                field, null, string.Empty, null, s_booleanText);

            Assert.Equal("Active", text);
        }

        [Fact]
        [DisplayName("A Boolean outside a check-box column reads as the supplied text")]
        public void FormatCell_Boolean_UsesBooleanText()
        {
            var text = GridCellFormatter.FormatCell(BuildRow(), new LayoutColumn("active", "Active", ControlType.TextEdit),
                null, null, string.Empty, null, s_booleanText);

            Assert.Equal("Yes", text);
        }

        [Fact]
        [DisplayName("A number uses the column's format in the user's culture")]
        public void FormatCell_Number_UsesColumnFormat()
        {
            using var _ = new CultureScope("en-US");
            var column = new LayoutColumn("amount", "Amount", ControlType.NumericEdit) { NumberFormat = "N2" };

            Assert.Equal("1,234.50", GridCellFormatter.FormatCell(BuildRow(), column, null, null, string.Empty, null, s_booleanText));
        }

        [Fact]
        [DisplayName("A number whose column has no format uses the schema field's company-baked format, as its editor does")]
        public void FormatCell_ColumnWithoutFormat_UsesFormFieldFormat()
        {
            using var _ = new CultureScope("en-US");
            var column = new LayoutColumn("amount", "Amount", ControlType.NumericEdit);
            var field = new FormField("amount", "Amount", FieldDbType.Decimal) { NumberKind = NumberKind.UnitPrice, NumberFormat = "N4" };

            Assert.Equal("1,234.5000", GridCellFormatter.FormatCell(BuildRow(), column, field, null, string.Empty, null, s_booleanText));
        }

        [Fact]
        [DisplayName("A column's own format wins over the schema field's")]
        public void FormatCell_ColumnFormat_WinsOverFormFieldFormat()
        {
            using var _ = new CultureScope("en-US");
            var column = new LayoutColumn("amount", "Amount", ControlType.NumericEdit) { NumberFormat = "N2" };
            var field = new FormField("amount", "Amount", FieldDbType.Decimal) { NumberKind = NumberKind.UnitPrice, NumberFormat = "N4" };

            Assert.Equal("1,234.50", GridCellFormatter.FormatCell(BuildRow(), column, field, null, string.Empty, null, s_booleanText));
        }

        [Fact]
        [DisplayName("A column with display fields shows them joined instead of its own value")]
        public void FormatColumnText_DisplayFields_Composes()
        {
            var column = new LayoutColumn("dept_rowid", "Department", ControlType.ButtonEdit) { DisplayFields = "ref_id,ref_name" };

            Assert.Equal("D01 - Engineering",
                GridCellFormatter.FormatColumnText(BuildRow(), column, null, null, string.Empty, null, s_booleanText));
        }

        [Fact]
        [DisplayName("A missing row or column gives an empty cell")]
        public void FormatCell_MissingRowOrColumn_IsEmpty()
        {
            var column = new LayoutColumn("missing", "Missing", ControlType.TextEdit);

            Assert.Equal(string.Empty, GridCellFormatter.FormatCell(null, column, null, null, string.Empty, null, s_booleanText));
            Assert.Equal(string.Empty, GridCellFormatter.FormatCell(BuildRow(), column, null, null, string.Empty, null, s_booleanText));
        }

        [Fact]
        [DisplayName("A read-only caption is parenthesised and suppresses the required cue")]
        public void FieldCaptionRules_ReadOnlyWins()
        {
            Assert.Equal("(Amount)", FieldCaptionRules.FormatCaption("Amount", readOnly: true));
            Assert.Equal("Amount", FieldCaptionRules.FormatCaption("Amount", readOnly: false));
            Assert.True(FieldCaptionRules.ShowsRequiredCue(readOnly: false, required: true));
            Assert.False(FieldCaptionRules.ShowsRequiredCue(readOnly: true, required: true));
        }

        [Fact]
        [DisplayName("ComposeDisplayText joins the display-field values of the row")]
        public void ComposeDisplayText_WithDisplayFields_JoinsValues()
        {
            Assert.Equal("D01 - Engineering",
                GridCellFormatter.ComposeDisplayText(BuildRow(), ["ref_id", "ref_name"], string.Empty, string.Empty));
        }

        [Fact]
        [DisplayName("ComposeDisplayText leaves out a display field the row does not have")]
        public void ComposeDisplayText_MissingField_IsLeftOut()
        {
            Assert.Equal("D01",
                GridCellFormatter.ComposeDisplayText(BuildRow(), ["ref_id", "no_such_field"], string.Empty, string.Empty));
        }

        [Fact]
        [DisplayName("ComposeDisplayText returns an empty string without a row or display fields")]
        public void ComposeDisplayText_NoRowOrFields_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, GridCellFormatter.ComposeDisplayText(null, ["ref_id"], string.Empty, string.Empty));
            Assert.Equal(string.Empty, GridCellFormatter.ComposeDisplayText(BuildRow(), [], string.Empty, string.Empty));
        }

        [Fact]
        [DisplayName("IsChecked is true for a true Boolean or the text True in any case, and false otherwise")]
        public void IsChecked_ReadsTrueValues()
        {
            var table = new DataTable("Line");
            table.Columns.Add("flag", typeof(bool));
            table.Columns.Add("text", typeof(string));
            var row = table.Rows.Add(true, "TRUE");
            var unset = table.Rows.Add(DBNull.Value, "1");

            Assert.True(GridCellFormatter.IsChecked(row, "flag"));
            Assert.True(GridCellFormatter.IsChecked(row, "text"));
            Assert.False(GridCellFormatter.IsChecked(unset, "flag"));
            Assert.False(GridCellFormatter.IsChecked(unset, "text"));
            Assert.False(GridCellFormatter.IsChecked(row, "missing"));
            Assert.False(GridCellFormatter.IsChecked(null, "flag"));
        }

        [Fact]
        [DisplayName("SortMark is an arrow after a space for a sorted column and empty for an unsorted one")]
        public void SortMark_FollowsTheDirection()
        {
            Assert.Equal(" \u25B2", GridCellFormatter.SortMark(Polhem.Definition.Sorting.SortDirection.Asc));
            Assert.Equal(" \u25BC", GridCellFormatter.SortMark(Polhem.Definition.Sorting.SortDirection.Desc));
            Assert.Equal(string.Empty, GridCellFormatter.SortMark(null));
        }
    }
}
