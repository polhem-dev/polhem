using ClosedXML.Excel;

namespace Polhem.Definition.Spreadsheet.UnitTests
{
    /// <summary>
    /// Lays out the form workbook template of a language from <see cref="FormWorkbookCatalog"/>. MiniExcel fills a
    /// template but cannot create drop-downs, so the templates are built here with ClosedXML and committed. The
    /// texts come from the catalog; this class only places them.
    /// </summary>
    internal static class FormWorkbookTemplateBuilder
    {
        private static readonly XLColor s_headerFill = XLColor.FromHtml("#DDEBF7");
        private static readonly XLColor s_advancedFill = XLColor.FromHtml("#EDEDED");

        public static XLWorkbook Build(FormWorkbookLanguage language)
        {
            var workbook = new XLWorkbook();
            var instructions = workbook.AddWorksheet(FormWorkbookCatalog.InstructionsSheet.Get(language));
            var form = workbook.AddWorksheet(FormWorkbookCatalog.FormSheet);
            var tabular = FormWorkbookCatalog.TabularSheets
                .Select(sheet => (Sheet: workbook.AddWorksheet(sheet.Sheet), sheet.Columns))
                .ToList();
            var lists = BuildLists(workbook.AddWorksheet(FormWorkbookCatalog.ListsSheet));

            BuildInstructions(instructions, language);
            BuildForm(form, lists, language);
            foreach (var (sheet, columns) in tabular)
                BuildTabular(sheet, columns, lists, language);

            form.SetTabActive();
            return workbook;
        }

        private static Dictionary<string, IXLRange> BuildLists(IXLWorksheet sheet)
        {
            var ranges = new Dictionary<string, IXLRange>(StringComparer.Ordinal);
            int column = 1;
            foreach (var (name, values) in FormWorkbookCatalog.Lists)
            {
                sheet.Cell(1, column).Value = name;
                for (int i = 0; i < values.Count; i++)
                    sheet.Cell(i + 2, column).Value = values[i];
                ranges[name] = sheet.Range(2, column, values.Count + 1, column);
                column++;
            }
            sheet.Hide();
            return ranges;
        }

        private static void BuildInstructions(IXLWorksheet sheet, FormWorkbookLanguage language)
        {
            var lines = FormWorkbookCatalog.InstructionLines;
            for (int i = 0; i < lines.Count; i++)
                sheet.Cell(i + 1, 1).Value = lines[i].Get(language);
            sheet.Cell(1, 1).Style.Font.Bold = true;
            sheet.Cell(1, 1).Style.Font.FontSize = 14;

            int row = lines.Count + 2;
            var header = FormWorkbookCatalog.InstructionHeaders;
            for (int i = 0; i < header.Count; i++)
                sheet.Cell(row, i + 1).Value = header[i].Get(language);
            sheet.Range(row, 1, row, header.Count).Style.Font.Bold = true;
            sheet.Range(row, 1, row, header.Count).Style.Fill.BackgroundColor = s_headerFill;

            foreach (var item in FormWorkbookCatalog.FormItems)
                AddInstructionRow(sheet, ++row, FormWorkbookCatalog.FormSheet, item, language);
            foreach (var (name, columns) in FormWorkbookCatalog.TabularSheets)
            {
                foreach (var column in columns)
                    AddInstructionRow(sheet, ++row, name, column, language);
            }
            sheet.Column(1).Width = 12;
            sheet.Column(2).Width = 22;
            sheet.Column(3).Width = 24;
            sheet.Column(4).Width = 100;
        }

        private static void AddInstructionRow(IXLWorksheet sheet, int row, string sheetName, ISheetColumn column, FormWorkbookLanguage language)
        {
            sheet.Cell(row, 1).Value = sheetName;
            sheet.Cell(row, 2).Value = column.Label.Get(language);
            sheet.Cell(row, 3).Value = column.Key;
            sheet.Cell(row, 4).Value = column.Hint.Get(language);
        }

        private static void BuildForm(IXLWorksheet sheet, Dictionary<string, IXLRange> lists, FormWorkbookLanguage language)
        {
            var (item, value, explanation) = FormWorkbookCatalog.FormHeaders;
            sheet.Cell(1, 1).Value = item.Get(language);
            sheet.Cell(1, 3).Value = value.Get(language);
            sheet.Cell(1, 4).Value = explanation.Get(language);
            var header = sheet.Range(1, 1, 1, 4);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = s_headerFill;

            int row = 2;
            foreach (var formItem in FormWorkbookCatalog.FormItems)
            {
                sheet.Cell(row, 1).Value = formItem.Label.Get(language);
                sheet.Cell(row, 2).Value = formItem.Key;
                sheet.Cell(row, 3).Value = $"{{{{{formItem.Key}}}}}";
                sheet.Cell(row, 4).Value = formItem.Hint.Get(language);
                if (formItem.Advanced)
                    sheet.Cell(row, 1).Style.Fill.BackgroundColor = s_advancedFill;
                if (formItem.ListName is not null)
                    sheet.Cell(row, 3).CreateDataValidation().List(lists[formItem.ListName], true);
                row++;
            }
            sheet.Column(1).Width = 24;
            sheet.Column(2).Hide();
            sheet.Column(3).Width = 36;
            sheet.Column(4).Width = 80;
            sheet.SheetView.FreezeRows(1);
        }

        private static void BuildTabular(
            IXLWorksheet sheet, IReadOnlyList<ISheetColumn> columns, Dictionary<string, IXLRange> lists, FormWorkbookLanguage language)
        {
            for (int i = 0; i < columns.Count; i++)
            {
                var column = columns[i];
                int index = i + 1;
                var label = sheet.Cell(FormWorkbookCatalog.LabelRow, index);
                label.Value = column.Label.Get(language);
                label.Style.Font.Bold = true;
                label.Style.Fill.BackgroundColor = column.Advanced ? s_advancedFill : s_headerFill;
                sheet.Cell(FormWorkbookCatalog.KeyRow, index).Value = column.Key;
                sheet.Cell(FormWorkbookCatalog.FirstDataRow, index).Value = $"{{{{{sheet.Name}.{column.Key}}}}}";
                sheet.Column(index).Width = column.Width;
                if (column.ListName is not null)
                {
                    sheet.Range(FormWorkbookCatalog.FirstDataRow, index, FormWorkbookCatalog.LastValidatedRow, index)
                        .CreateDataValidation().List(lists[column.ListName], true);
                }
            }
            sheet.Row(FormWorkbookCatalog.KeyRow).Hide();
            sheet.SheetView.FreezeRows(FormWorkbookCatalog.KeyRow);

            int firstAdvanced = columns.ToList().FindIndex(c => c.Advanced);
            if (firstAdvanced >= 0)
            {
                // `Group(true)` only marks the group collapsed; Excel and LibreOffice open it expanded unless the
                // columns themselves are hidden, which is what `Collapse` adds.
                var advanced = sheet.Columns(firstAdvanced + 1, columns.Count);
                advanced.Group();
                advanced.Collapse();
            }
        }
    }
}
