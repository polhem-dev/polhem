using System.ComponentModel;
using ClosedXML.Excel;
using Polhem.Core.Data;

namespace Polhem.Definition.Spreadsheet.UnitTests
{
    /// <summary>
    /// What <see cref="FormWorkbookReader"/> makes of a workbook a person filled in, and where it reports what it
    /// cannot accept.
    /// </summary>
    public class FormWorkbookReaderTests
    {
        [Fact]
        [DisplayName("A misspelled data type is reported at its cell")]
        public void Read_UnknownDbType_ReportsCell()
        {
            using var workbook = new FilledWorkbook().Form("ProgId", "Quote");
            workbook.Row(FormWorkbookCatalog.FieldsSheet, [("FieldName", "quote_date"), ("DbType", "Dat")], out int row);

            var result = workbook.Read();

            var letter = XLHelper.GetColumnLetterFromNumber(
                FilledWorkbook.ColumnOf(workbook.Workbook.Worksheet(FormWorkbookCatalog.FieldsSheet), "DbType"));
            var issue = Assert.Single(result.Issues);
            Assert.Equal(IssueSeverity.Error, issue.Severity);
            Assert.Equal($"Fields!{letter}{row}", issue.Location);
            Assert.Contains("'Dat'", issue.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A number typed into an enum column is rejected rather than taken as an enum value")]
        public void Read_NumberInEnumColumn_IsRejected()
        {
            using var workbook = new FilledWorkbook().Form("ProgId", "Quote")
                .Field(("FieldName", "qty"), ("NumberKind", "1"));

            var result = workbook.Read();

            Assert.True(result.HasErrors);
        }

        [Fact]
        [DisplayName("Yes and no accept Y, N and their Chinese forms, and blank keeps the default")]
        public void Read_Booleans_AcceptCommonForms()
        {
            using var workbook = new FilledWorkbook().Form("ProgId", "Quote")
                .Field(("FieldName", "a"), ("Required", "是"), ("Visible", "否"))
                .Field(("FieldName", "b"), ("Required", "y"))
                .Field(("FieldName", "c"));

            var result = workbook.Read();

            Assert.Empty(result.Issues);
            var fields = result.Content!.Schema.Tables![0].Fields!;
            Assert.True(fields["a"].Required);
            Assert.False(fields["a"].Visible);
            Assert.True(fields["b"].Required);
            Assert.False(fields["c"].Required);
            Assert.True(fields["c"].Visible);
        }

        [Fact]
        [DisplayName("A field naming a table that is not listed is reported at its row")]
        public void Read_FieldOfUnknownTable_ReportsRow()
        {
            using var workbook = new FilledWorkbook().Form("ProgId", "Quote");
            workbook.Row(FormWorkbookCatalog.FieldsSheet, [("TableName", "QuoteLine"), ("FieldName", "qty")], out int row);

            var result = workbook.Read();

            var issue = Assert.Single(result.Issues);
            Assert.Equal($"Fields!{row}", issue.Location);
        }

        [Fact]
        [DisplayName("A field listed twice in the same table is reported and read once")]
        public void Read_DuplicateField_ReportsAndKeepsFirst()
        {
            using var workbook = new FilledWorkbook().Form("ProgId", "Quote")
                .Field(("FieldName", "qty"), ("Caption", "First"))
                .Field(("FieldName", "QTY"), ("Caption", "Second"));

            var result = workbook.Read();

            Assert.Single(result.Issues);
            Assert.Equal("First", result.Content!.Schema.Tables![0].Fields!["qty"].Caption);
        }

        [Fact]
        [DisplayName("Fields are read into the table they name, and an empty table name means the master")]
        public void Read_Fields_GoToTheirTables()
        {
            using var workbook = new FilledWorkbook().Form("ProgId", "Quote")
                .Table(("TableName", ""), ("DisplayName", "Quote"))
                .Table(("TableName", "QuoteLine"), ("DisplayName", "Quote Lines"), ("DisplayName_zh_TW", "報價明細"))
                .Field(("FieldName", "quote_date"), ("DbType", "Date"))
                .Field(("TableName", "QuoteLine"), ("FieldName", "qty"), ("DbType", "Integer"), ("Caption_zh_TW", "數量"));

            var result = workbook.Read();

            Assert.Empty(result.Issues);
            var schema = result.Content!.Schema;
            Assert.Equal(["Quote", "QuoteLine"], schema.Tables!.Select(t => t.TableName));
            Assert.Equal(FieldDbType.Date, schema.Tables!["Quote"].Fields!["quote_date"].DbType);
            Assert.Equal(FieldDbType.Integer, schema.Tables["QuoteLine"].Fields!["qty"].DbType);
            Assert.Equal("報價明細", result.Content.ZhTw["Table.QuoteLine.DisplayName"]);
            Assert.Equal("數量", result.Content.ZhTw["Field.qty.Caption"]);
        }

        [Fact]
        [DisplayName("A column a person inserts without a key is ignored and the keyed columns still read")]
        public void Read_InsertedColumn_IsIgnored()
        {
            using var workbook = new FilledWorkbook().Form("ProgId", "Quote")
                .Field(("FieldName", "qty"), ("DbType", "Integer"));
            var sheet = workbook.Workbook.Worksheet(FormWorkbookCatalog.FieldsSheet);
            sheet.Column(2).InsertColumnsBefore(1);
            sheet.Cell(1, 2).Value = "我的備註";
            sheet.Cell(FormWorkbookCatalog.FirstDataRow, 2).Value = "remember to ask finance";

            var result = workbook.Read();

            Assert.Empty(result.Issues);
            Assert.Equal(FieldDbType.Integer, result.Content!.Schema.Tables![0].Fields!["qty"].DbType);
        }

        [Fact]
        [DisplayName("List items are read from one cell, with the value standing in for a missing text")]
        public void Read_ListItems_ParsesCell()
        {
            using var workbook = new FilledWorkbook().Form("ProgId", "Quote")
                .Field(("FieldName", "status"), ("ListItems", "Draft=草稿; Sent"));

            var result = workbook.Read();

            Assert.Empty(result.Issues);
            var items = result.Content!.Schema.Tables![0].Fields!["status"].ListItems!;
            Assert.Equal(["Draft:草稿", "Sent:Sent"], items.Select(i => $"{i.Value}:{i.Text}"));
        }

        [Fact]
        [DisplayName("A workbook without the Fields sheet is refused with no content")]
        public void Read_MissingSheet_Refused()
        {
            using var workbook = new FilledWorkbook();
            workbook.Workbook.Worksheet(FormWorkbookCatalog.FieldsSheet).Delete();

            var result = workbook.Read();

            Assert.Null(result.Content);
            Assert.True(result.HasErrors);
        }
    }
}
