using System.ComponentModel;
using ClosedXML.Excel;
using Polhem.Tests.Shared;

namespace Polhem.Definition.Spreadsheet.UnitTests
{
    /// <summary>
    /// Keeps the committed workbook template in step with <see cref="FormWorkbookCatalog"/>. The template is a
    /// binary file nobody reviews in a diff, so this test compares what it contains (every cell, hidden row and
    /// column, drop-down and column group) with what the catalog lays out.
    /// </summary>
    public class FormWorkbookTemplateTests
    {
        /// <summary>Set to <c>1</c> to rewrite the committed template from the catalog instead of comparing.</summary>
        private const string RegenerateVariable = "POLHEM_REGENERATE_FORM_TEMPLATE";

        private static string TemplatePath => Path.Combine(RepoRoot.Find(),
            "tools", "Polhem.Definition.Spreadsheet", "Templates", "FormSchema.xlsx");

        [Fact]
        [DisplayName("The committed workbook template matches the layout the catalog describes")]
        public void Template_MatchesCatalog()
        {
            using var expected = FormWorkbookTemplateBuilder.Build();
            if (Environment.GetEnvironmentVariable(RegenerateVariable) == "1")
                expected.SaveAs(TemplatePath);

            using var committed = new XLWorkbook(TemplatePath);
            var expectedLines = Describe(expected);
            var committedLines = Describe(committed);
            Assert.True(expectedLines.SequenceEqual(committedLines),
                $"The template is out of date. Run this test with {RegenerateVariable}=1 to regenerate it. First difference: "
                + FirstDifference(expectedLines, committedLines));
        }

        [Fact]
        [DisplayName("The embedded template is the committed template file")]
        public void EmbeddedTemplate_IsCommittedFile()
        {
            Assert.Equal(File.ReadAllBytes(TemplatePath), FormWorkbookTemplate.GetBytes());
        }

        /// <summary>
        /// Flattens what matters in a workbook into comparable lines: sheet order and visibility, cell values,
        /// hidden rows and columns, column outline levels and drop-down ranges with their sources.
        /// </summary>
        internal static List<string> Describe(XLWorkbook workbook)
        {
            var lines = new List<string>();
            foreach (var sheet in workbook.Worksheets)
            {
                lines.Add($"sheet {sheet.Name} {sheet.Visibility}");
                foreach (var cell in sheet.CellsUsed())
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
