using ClosedXML.Excel;

namespace Polhem.Definition.Spreadsheet.UnitTests
{
    /// <summary>
    /// A blank form workbook filled in the way a person would, cell by cell, for reader and importer tests.
    /// </summary>
    internal sealed class FilledWorkbook : IDisposable
    {
        private readonly XLWorkbook _workbook;

        public FilledWorkbook(FormWorkbookLanguage language = FormWorkbookLanguage.English)
        {
            var stream = new MemoryStream();
            FormWorkbookWriter.WriteBlank(stream, language);
            stream.Position = 0;
            _workbook = new XLWorkbook(stream);
        }

        public XLWorkbook Workbook => _workbook;

        public FilledWorkbook Form(string key, string value)
        {
            var sheet = _workbook.Worksheet(FormWorkbookCatalog.FormSheet);
            var row = sheet.RowsUsed().First(r => r.Cell(2).GetString() == key);
            row.Cell(3).Value = value;
            return this;
        }

        public FilledWorkbook Table(params (string Key, string Value)[] cells) => Row(FormWorkbookCatalog.TablesSheet, cells);

        public FilledWorkbook Field(params (string Key, string Value)[] cells) => Row(FormWorkbookCatalog.FieldsSheet, cells);

        public FilledWorkbook Rule(params (string Key, string Value)[] cells) => Row(FormWorkbookCatalog.RulesSheet, cells);

        /// <summary>Writes a row after the last used one; returns its row number through <paramref name="rowNumber"/>.</summary>
        public FilledWorkbook Row(string sheetName, (string Key, string Value)[] cells, out int rowNumber)
        {
            var sheet = _workbook.Worksheet(sheetName);
            rowNumber = Math.Max(FormWorkbookCatalog.FirstDataRow, (sheet.LastRowUsed()?.RowNumber() ?? 0) + 1);
            foreach (var (key, value) in cells)
                sheet.Cell(rowNumber, ColumnOf(sheet, key)).Value = value;
            return this;
        }

        public static int ColumnOf(IXLWorksheet sheet, string key)
            => sheet.Row(FormWorkbookCatalog.KeyRow).CellsUsed().First(c => c.GetString() == key).Address.ColumnNumber;

        public FormWorkbookReadResult Read()
        {
            using var stream = new MemoryStream();
            _workbook.SaveAs(stream);
            stream.Position = 0;
            return FormWorkbookReader.Read(stream);
        }

        public void Dispose() => _workbook.Dispose();

        private FilledWorkbook Row(string sheetName, (string Key, string Value)[] cells) => Row(sheetName, cells, out _);
    }
}
