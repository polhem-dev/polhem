using ClosedXML.Excel;

namespace Polhem.Definition.Spreadsheet.UnitTests
{
    /// <summary>
    /// Lays out the form workbook template from <see cref="FormWorkbookCatalog"/>. MiniExcel fills a template
    /// but cannot create drop-downs, so the template is built here with ClosedXML and committed.
    /// </summary>
    internal static class FormWorkbookTemplateBuilder
    {
        private static readonly XLColor s_headerFill = XLColor.FromHtml("#DDEBF7");
        private static readonly XLColor s_advancedFill = XLColor.FromHtml("#EDEDED");

        public static XLWorkbook Build()
        {
            var workbook = new XLWorkbook();
            var instructions = workbook.AddWorksheet(FormWorkbookCatalog.InstructionsSheet);
            var form = workbook.AddWorksheet(FormWorkbookCatalog.FormSheet);
            var tabular = FormWorkbookCatalog.TabularSheets
                .Select(sheet => (Sheet: workbook.AddWorksheet(sheet.Sheet), sheet.Columns))
                .ToList();
            var lists = BuildLists(workbook.AddWorksheet(FormWorkbookCatalog.ListsSheet));

            BuildInstructions(instructions);
            BuildForm(form, lists);
            foreach (var (sheet, columns) in tabular)
                BuildTabular(sheet, columns, lists);

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

        private static void BuildInstructions(IXLWorksheet sheet)
        {
            string[] lines =
            [
                "表單定義活頁簿",
                "",
                "1. 在 Form 工作表填寫表單代號、名稱與選單位置。",
                "2. 在 Tables 工作表列出資料表：第一列是主檔，其餘是明細表。只有主檔時可以不填。",
                "3. 在 Fields 工作表逐列描述欄位。系統欄位與關聯帶回的 ref_ 欄位不必填，匯入時自動補上。",
                "4. 需要存檔前檢查時，在 Rules 工作表填寫規則。",
                "5. 執行 dotnet polhem forms import <這個檔案> --define <Define 資料夾> 產生定義檔。",
                "",
                "第 2 列是隱藏的欄位鍵，請勿刪除。可以調整欄順序、修改標題，或插入自己的備註欄（匯入時略過）。",
                "灰色標題的欄位是進階設定，預設折疊，點欄位上方的 + 展開。",
            ];
            for (int i = 0; i < lines.Length; i++)
                sheet.Cell(i + 1, 1).Value = lines[i];
            sheet.Cell(1, 1).Style.Font.Bold = true;
            sheet.Cell(1, 1).Style.Font.FontSize = 14;

            int row = lines.Length + 2;
            string[] header = ["工作表", "欄位", "鍵", "說明"];
            for (int i = 0; i < header.Length; i++)
                sheet.Cell(row, i + 1).Value = header[i];
            sheet.Range(row, 1, row, header.Length).Style.Font.Bold = true;
            sheet.Range(row, 1, row, header.Length).Style.Fill.BackgroundColor = s_headerFill;

            foreach (var item in FormWorkbookCatalog.FormItems)
                AddInstructionRow(sheet, ++row, FormWorkbookCatalog.FormSheet, item);
            foreach (var (name, columns) in FormWorkbookCatalog.TabularSheets)
            {
                foreach (var column in columns)
                    AddInstructionRow(sheet, ++row, name, column);
            }
            sheet.Column(1).Width = 12;
            sheet.Column(2).Width = 22;
            sheet.Column(3).Width = 24;
            sheet.Column(4).Width = 100;
        }

        private static void AddInstructionRow(IXLWorksheet sheet, int row, string sheetName, ISheetColumn column)
        {
            sheet.Cell(row, 1).Value = sheetName;
            sheet.Cell(row, 2).Value = column.Label;
            sheet.Cell(row, 3).Value = column.Key;
            sheet.Cell(row, 4).Value = column.Hint;
        }

        private static void BuildForm(IXLWorksheet sheet, Dictionary<string, IXLRange> lists)
        {
            sheet.Cell(1, 1).Value = "項目";
            sheet.Cell(1, 3).Value = "內容";
            sheet.Cell(1, 4).Value = "說明";
            var header = sheet.Range(1, 1, 1, 4);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = s_headerFill;

            int row = 2;
            foreach (var item in FormWorkbookCatalog.FormItems)
            {
                sheet.Cell(row, 1).Value = item.Label;
                sheet.Cell(row, 2).Value = item.Key;
                sheet.Cell(row, 3).Value = $"{{{{{item.Key}}}}}";
                sheet.Cell(row, 4).Value = item.Hint;
                if (item.Advanced)
                    sheet.Cell(row, 1).Style.Fill.BackgroundColor = s_advancedFill;
                if (item.ListName is not null)
                    sheet.Cell(row, 3).CreateDataValidation().List(lists[item.ListName], true);
                row++;
            }
            sheet.Column(1).Width = 24;
            sheet.Column(2).Hide();
            sheet.Column(3).Width = 36;
            sheet.Column(4).Width = 80;
            sheet.SheetView.FreezeRows(1);
        }

        private static void BuildTabular(IXLWorksheet sheet, IReadOnlyList<ISheetColumn> columns, Dictionary<string, IXLRange> lists)
        {
            for (int i = 0; i < columns.Count; i++)
            {
                var column = columns[i];
                int index = i + 1;
                var label = sheet.Cell(FormWorkbookCatalog.LabelRow, index);
                label.Value = column.Label;
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
                sheet.Columns(firstAdvanced + 1, columns.Count).Group(true);
            }
        }
    }
}
