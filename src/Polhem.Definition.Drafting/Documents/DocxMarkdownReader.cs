using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Polhem.Definition.Drafting.Documents
{
    /// <summary>
    /// Reads the body of a Word document into Markdown: paragraphs as lines, headings with <c>#</c>, list items with
    /// <c>-</c>, and tables as pipe tables.
    /// </summary>
    /// <remarks>
    /// A merged cell keeps the table's columns aligned: its text appears once, in the cell where the merge starts, and
    /// every cell it covers (to the right through <c>gridSpan</c>, below through <c>vMerge</c>) is written empty. A
    /// table nested in a cell is flattened into that cell's text.
    /// </remarks>
    internal static class DocxMarkdownReader
    {
        public static string Read(Stream stream)
        {
            using var document = WordprocessingDocument.Open(stream, isEditable: false);
            var body = document.MainDocumentPart?.Document?.Body;
            if (body is null) return string.Empty;

            var builder = new StringBuilder();
            AppendBlocks(builder, body.ChildElements);
            return builder.ToString().TrimEnd() + "\n";
        }

        private static void AppendBlocks(StringBuilder builder, OpenXmlElementList elements)
        {
            foreach (var element in elements)
            {
                switch (element)
                {
                    case Paragraph paragraph:
                        AppendParagraph(builder, paragraph);
                        break;
                    case Table table:
                        AppendTable(builder, table);
                        break;
                    case SdtBlock block when block.SdtContentBlock is not null:
                        AppendBlocks(builder, block.SdtContentBlock.ChildElements);
                        break;
                }
            }
        }

        private static void AppendParagraph(StringBuilder builder, Paragraph paragraph)
        {
            var text = ParagraphText(paragraph);
            if (text.Length == 0) return;

            var properties = paragraph.ParagraphProperties;
            var level = HeadingLevel(properties?.ParagraphStyleId?.Val?.Value);
            if (level > 0)
                builder.Append('#', level).Append(' ');
            else if (properties?.NumberingProperties is not null)
                builder.Append("- ");
            builder.Append(text).Append("\n\n");
        }

        private static int HeadingLevel(string? styleId)
        {
            if (styleId is null) return 0;
            if (styleId.Equals("Title", StringComparison.OrdinalIgnoreCase)) return 1;
            const string prefix = "Heading";
            if (styleId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(styleId.AsSpan(prefix.Length), System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var level)
                && level is >= 1 and <= 6)
                return level;
            return 0;
        }

        private static string ParagraphText(OpenXmlElement paragraph)
        {
            var builder = new StringBuilder();
            foreach (var element in paragraph.Descendants())
            {
                switch (element)
                {
                    case Text text:
                        builder.Append(text.Text);
                        break;
                    case TabChar:
                    case Break:
                        builder.Append(' ');
                        break;
                }
            }
            return builder.ToString().Trim();
        }

        private static void AppendTable(StringBuilder builder, Table table)
        {
            var rows = new List<List<string>>();
            foreach (var row in table.Elements<TableRow>())
            {
                var cells = new List<string>();
                foreach (var cell in row.Elements<TableCell>())
                {
                    var properties = cell.TableCellProperties;
                    var continuesMerge = properties?.VerticalMerge is { } merge
                        && (merge.Val is null || merge.Val.Value == MergedCellValues.Continue);
                    cells.Add(continuesMerge ? string.Empty : CellText(cell));
                    var span = properties?.GridSpan?.Val?.Value ?? 1;
                    for (var covered = 1; covered < span; covered++)
                        cells.Add(string.Empty);
                }
                rows.Add(cells);
            }
            if (rows.Count == 0) return;

            var columns = rows.Max(row => row.Count);
            if (columns == 0) return;
            for (var index = 0; index < rows.Count; index++)
            {
                AppendRow(builder, rows[index], columns);
                if (index == 0)
                    builder.Append('|').Append(string.Concat(Enumerable.Repeat(" --- |", columns))).Append('\n');
            }
            builder.Append('\n');
        }

        private static void AppendRow(StringBuilder builder, List<string> cells, int columns)
        {
            builder.Append('|');
            for (var column = 0; column < columns; column++)
            {
                var text = column < cells.Count ? cells[column] : string.Empty;
                builder.Append(' ').Append(text).Append(" |");
            }
            builder.Append('\n');
        }

        private static string CellText(TableCell cell)
        {
            var parts = cell.Descendants<Paragraph>()
                .Select(ParagraphText)
                .Where(text => text.Length > 0);
            return string.Join("<br>", parts).Replace("|", "\\|", StringComparison.Ordinal);
        }
    }
}
