using System.ComponentModel;
using ClosedXML.Excel;
using Polhem.Tests.Shared;

namespace Polhem.Definition.Spreadsheet.UnitTests
{
    /// <summary>
    /// Keeps the committed workbook templates, one per <see cref="FormWorkbookLanguage"/>, in step with
    /// <see cref="FormWorkbookCatalog"/>. A template is a binary file nobody reviews in a diff, so this test compares
    /// what it contains (every cell, hidden row and column, drop-down and column group) with what the catalog lays out.
    /// </summary>
    public class FormWorkbookTemplateTests
    {
        /// <summary>Set to <c>1</c> to rewrite the committed templates from the catalog instead of comparing.</summary>
        private const string RegenerateVariable = "POLHEM_REGENERATE_FORM_TEMPLATE";

        public static TheoryData<FormWorkbookLanguage> Languages => [.. Enum.GetValues<FormWorkbookLanguage>()];

        internal static string TemplatePath(FormWorkbookLanguage language) => Path.Combine(RepoRoot.Find(),
            "tools", "Polhem.Definition.Spreadsheet", "Templates", FormWorkbookTemplate.FileName(language));

        [Theory]
        [MemberData(nameof(Languages))]
        [DisplayName("The committed workbook template of each language matches the layout the catalog describes")]
        public void Template_MatchesCatalog(FormWorkbookLanguage language)
        {
            using var expected = FormWorkbookTemplateBuilder.Build(language);
            if (Environment.GetEnvironmentVariable(RegenerateVariable) == "1")
                expected.SaveAs(TemplatePath(language));

            using var committed = new XLWorkbook(TemplatePath(language));
            var expectedLines = Describe(expected);
            var committedLines = Describe(committed);
            Assert.True(expectedLines.SequenceEqual(committedLines),
                $"The {language} template is out of date. Run this test with {RegenerateVariable}=1 to regenerate it. First difference: "
                + FirstDifference(expectedLines, committedLines));
        }

        [Theory]
        [MemberData(nameof(Languages))]
        [DisplayName("The embedded template of each language is the committed template file")]
        public void EmbeddedTemplate_IsCommittedFile(FormWorkbookLanguage language)
        {
            Assert.Equal(File.ReadAllBytes(TemplatePath(language)), FormWorkbookTemplate.GetBytes(language));
        }

        [Fact]
        [DisplayName("The templates of the two languages differ only in the texts shown to people")]
        public void Templates_DifferOnlyInShownTexts()
        {
            using var english = FormWorkbookTemplateBuilder.Build(FormWorkbookLanguage.English);
            using var chinese = FormWorkbookTemplateBuilder.Build(FormWorkbookLanguage.TraditionalChinese);

            Assert.Equal(Structure(english), Structure(chinese));
        }

        /// <summary>
        /// What a reader or a drop-down depends on: every sheet but the instructions, without the header row of a
        /// sheet and the label and explanation columns of the Form sheet.
        /// </summary>
        private static List<string> Structure(XLWorkbook workbook)
            => Describe(workbook,
                sheet => sheet.Name != FormWorkbookCatalog.InstructionsSheet.En
                    && sheet.Name != FormWorkbookCatalog.InstructionsSheet.ZhTw,
                cell => cell.Address.RowNumber != FormWorkbookCatalog.LabelRow
                    && (cell.Worksheet.Name != FormWorkbookCatalog.FormSheet || cell.Address.ColumnNumber is 2 or 3));

        /// <summary>
        /// Flattens what matters in a workbook into comparable lines: sheet order and visibility, cell values,
        /// hidden rows and columns, column outline levels and drop-down ranges with their sources.
        /// </summary>
        internal static List<string> Describe(
            XLWorkbook workbook, Func<IXLWorksheet, bool>? includeSheet = null, Func<IXLCell, bool>? includeCell = null)
        {
            var lines = new List<string>();
            foreach (var sheet in workbook.Worksheets.Where(includeSheet ?? (_ => true)))
            {
                lines.Add($"sheet {sheet.Name} {sheet.Visibility}");
                foreach (var cell in sheet.CellsUsed().Where(includeCell ?? (_ => true)))
                    lines.Add($"{sheet.Name}!{cell.Address} = {cell.GetString()}");
                foreach (var row in sheet.RowsUsed().Where(r => r.IsHidden))
                    lines.Add($"{sheet.Name} hidden row {row.RowNumber()}");
                foreach (var column in sheet.ColumnsUsed())
                {
                    if (column.IsHidden)
                        lines.Add($"{sheet.Name} hidden column {column.ColumnLetter()}");
                    if (column.OutlineLevel > 0)
                        lines.Add($"{sheet.Name} column {column.ColumnLetter()} outline {column.OutlineLevel}");
                }
                // Saving merges identical drop-downs of adjacent columns into one range, so compare per column.
                var validations = new List<string>();
                foreach (var validation in sheet.DataValidations)
                {
                    foreach (var range in validation.Ranges)
                    {
                        var address = range.RangeAddress;
                        for (int column = address.FirstAddress.ColumnNumber; column <= address.LastAddress.ColumnNumber; column++)
                        {
                            var letter = XLHelper.GetColumnLetterFromNumber(column);
                            validations.Add($"{sheet.Name} validation {letter}{address.FirstAddress.RowNumber}:{letter}{address.LastAddress.RowNumber} "
                                + $"{validation.AllowedValues} {validation.Value}");
                        }
                    }
                }
                lines.AddRange(validations.Order(StringComparer.Ordinal));
            }
            return lines;
        }

        private static string FirstDifference(List<string> expected, List<string> actual)
        {
            for (int i = 0; i < Math.Max(expected.Count, actual.Count); i++)
            {
                var e = i < expected.Count ? expected[i] : "(nothing)";
                var a = i < actual.Count ? actual[i] : "(nothing)";
                if (e != a) return $"expected '{e}', template has '{a}'";
            }
            return "none";
        }
    }
}
