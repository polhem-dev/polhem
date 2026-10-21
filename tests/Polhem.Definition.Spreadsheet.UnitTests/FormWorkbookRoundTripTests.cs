using System.ComponentModel;
using ClosedXML.Excel;
using Polhem.Core.Serialization;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Tests.Shared;

namespace Polhem.Definition.Spreadsheet.UnitTests
{
    /// <summary>
    /// Exporting a form to a workbook and reading it back gives the same form: the schema, its zh-TW texts and
    /// its menu placement. Run over every FormSchema in the repository's definition folders, read-only.
    /// </summary>
    public class FormWorkbookRoundTripTests
    {
        private static readonly string[] s_defineFolders =
        [
            Path.Combine("tests", "Define"),
            Path.Combine("apps", "Polhem.Northwind", "Define"),
            Path.Combine("samples", "Define"),
        ];

        public static TheoryData<string, string> Forms()
        {
            var data = new TheoryData<string, string>();
            var root = RepoRoot.Find();
            foreach (var folder in s_defineFolders)
            {
                const string suffix = ".FormSchema.xml";
                foreach (var file in Directory.EnumerateFiles(Path.Combine(root, folder, "FormSchema"), "*" + suffix).Order(StringComparer.Ordinal))
                    data.Add(folder, Path.GetFileName(file)[..^suffix.Length]);
            }
            return data;
        }

        [Theory]
        [MemberData(nameof(Forms))]
        [DisplayName("A form exported to a workbook and read back is the same form")]
        public void ExportThenRead_GivesSameForm(string defineFolder, string progId)
        {
            var original = FormExporter.Load(Path.Combine(RepoRoot.Find(), defineFolder), progId);

            var result = RoundTrip(original);

            Assert.Empty(result.Issues);
            var read = result.Content!;
            Assert.Equal(XmlCodec.Serialize(original.Schema), XmlCodec.Serialize(read.Schema));
            Assert.Equal(original.ZhTw.Where(IsFormKey).OrderBy(p => p.Key, StringComparer.Ordinal),
                read.ZhTw.OrderBy(p => p.Key, StringComparer.Ordinal));
            Assert.Equal(original.Menu.FolderId, read.Menu.FolderId);
            Assert.Equal(original.Menu.EntryCaption, read.Menu.EntryCaption);
            Assert.Equal(original.Menu.EntryCaptionZhTw, read.Menu.EntryCaptionZhTw);
        }

        [Fact]
        [DisplayName("The Northwind order form carries its zh-TW captions and menu folder into the workbook")]
        public void Export_NorthwindOrder_FillsTranslationsAndMenu()
        {
            var content = FormExporter.Load(Path.Combine(RepoRoot.Find(), "apps", "Polhem.Northwind", "Define"), "Order");

            using var workbook = Workbook(content);

            Assert.Equal("訂單", Cell(workbook, FormWorkbookCatalog.FormSheet, "DisplayName_zh_TW"));
            Assert.Equal("transactions", Cell(workbook, FormWorkbookCatalog.FormSheet, "MenuFolderId"));
            var fields = workbook.Worksheet(FormWorkbookCatalog.FieldsSheet);
            var orderDate = fields.RowsUsed().First(r => r.Cell(2).GetString() == "order_date");
            Assert.Equal("訂單日期", orderDate.Cell(4).GetString());
        }

        [Fact]
        [DisplayName("An exported workbook keeps the drop-downs and the hidden key row of the template")]
        public void Export_KeepsTemplateFeatures()
        {
            var content = FormExporter.Load(Path.Combine(RepoRoot.Find(), "apps", "Polhem.Northwind", "Define"), "Order");

            using var workbook = Workbook(content);

            var fields = workbook.Worksheet(FormWorkbookCatalog.FieldsSheet);
            Assert.True(fields.Row(FormWorkbookCatalog.KeyRow).IsHidden);
            Assert.Contains(fields.DataValidations, v => v.Ranges.Any(r => r.RangeAddress.ToString()!.StartsWith("E3", StringComparison.Ordinal)));
            Assert.Equal(XLWorksheetVisibility.Hidden, workbook.Worksheet(FormWorkbookCatalog.ListsSheet).Visibility);
        }

        [Fact]
        [DisplayName("An exported workbook opens with the advanced field columns collapsed and the others shown")]
        public void Export_CollapsesAdvancedColumns()
        {
            var content = FormExporter.Load(Path.Combine(RepoRoot.Find(), "apps", "Polhem.Northwind", "Define"), "Order");

            using var workbook = Workbook(content);

            var fields = workbook.Worksheet(FormWorkbookCatalog.FieldsSheet);
            var columns = FormWorkbookCatalog.FieldColumns.ToList();
            Assert.Contains(columns, c => c.Advanced);
            for (int i = 0; i < columns.Count; i++)
            {
                var column = fields.Column(i + 1);
                Assert.Equal(columns[i].Advanced, column.IsHidden);
                Assert.Equal(columns[i].Advanced ? 1 : 0, column.OutlineLevel);
            }
        }

        [Fact]
        [DisplayName("An exported workbook writes Auto in the control column of a field that leaves the control to the data type")]
        public void Export_WritesAutoControl()
        {
            var content = FormExporter.Load(Path.Combine(RepoRoot.Find(), "apps", "Polhem.Northwind", "Define"), "Order");

            using var workbook = Workbook(content);

            var fields = workbook.Worksheet(FormWorkbookCatalog.FieldsSheet);
            int control = FormWorkbookCatalog.FieldColumns.ToList().FindIndex(c => c.Key == nameof(FormField.ControlType)) + 1;
            string ControlOf(string fieldName) => fields.RowsUsed().First(r => r.Cell(2).GetString() == fieldName).Cell(control).GetString();
            Assert.Equal(nameof(ControlType.Auto), ControlOf("order_date"));
            Assert.Equal(nameof(ControlType.DropDownEdit), ControlOf("status"));
        }

        [Fact]
        [DisplayName("A blank workbook reads back as an empty form with only a master table")]
        public void Blank_ReadsBackEmpty()
        {
            using var stream = new MemoryStream();
            FormWorkbookWriter.WriteBlank(stream);
            stream.Position = 0;

            var result = FormWorkbookReader.Read(stream);

            Assert.Empty(result.Issues);
            Assert.Single(result.Content!.Schema.Tables!);
            Assert.Empty(result.Content.Schema.Tables![0].Fields!);
            Assert.Empty(result.Content.Schema.Rules!);
        }

        internal static FormWorkbookReadResult RoundTrip(FormWorkbookContent content)
        {
            using var stream = new MemoryStream();
            FormWorkbookWriter.Write(content, stream);
            stream.Position = 0;
            return FormWorkbookReader.Read(stream);
        }

        private static XLWorkbook Workbook(FormWorkbookContent content)
        {
            var stream = new MemoryStream();
            FormWorkbookWriter.Write(content, stream);
            stream.Position = 0;
            return new XLWorkbook(stream);
        }

        private static string Cell(XLWorkbook workbook, string sheet, string key)
            => workbook.Worksheet(sheet).RowsUsed().First(r => r.Cell(2).GetString() == key).Cell(3).GetString();

        /// <summary>The language file may hold keys the workbook does not show, such as enum texts.</summary>
        private static bool IsFormKey(KeyValuePair<string, string> pair)
            => pair.Key.StartsWith("Schema.", StringComparison.Ordinal)
                || pair.Key.StartsWith("Table.", StringComparison.Ordinal)
                || pair.Key.StartsWith("Field.", StringComparison.Ordinal)
                || pair.Key.StartsWith("Rule.", StringComparison.Ordinal);
    }
}
