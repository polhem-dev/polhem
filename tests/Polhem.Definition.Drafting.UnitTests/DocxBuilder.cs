using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Polhem.Definition.Drafting.UnitTests
{
    /// <summary>
    /// Builds small Word documents in memory, so the reader is tested against real OpenXml packages.
    /// </summary>
    internal static class DocxBuilder
    {
        public static MemoryStream Build(params OpenXmlElement[] blocks)
        {
            var stream = new MemoryStream();
            using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
            {
                var main = document.AddMainDocumentPart();
                main.Document = new Document(new Body(blocks));
            }
            stream.Position = 0;
            return stream;
        }

        public static Paragraph Paragraph(string text, string? styleId = null, bool listItem = false)
        {
            var paragraph = new Paragraph();
            if (styleId is not null || listItem)
            {
                var properties = new ParagraphProperties();
                if (styleId is not null)
                    properties.ParagraphStyleId = new ParagraphStyleId { Val = styleId };
                if (listItem)
                    properties.NumberingProperties = new NumberingProperties(
                        new NumberingLevelReference { Val = 0 }, new NumberingId { Val = 1 });
                paragraph.Append(properties);
            }
            paragraph.Append(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
            return paragraph;
        }

        public static Table Table(params TableRow[] rows) => new(rows);

        public static TableRow Row(params TableCell[] cells) => new(cells);

        public static TableCell Cell(string text, int gridSpan = 1, MergedCellValues? verticalMerge = null)
        {
            var cell = new TableCell();
            var properties = new TableCellProperties();
            if (gridSpan > 1)
                properties.GridSpan = new GridSpan { Val = gridSpan };
            if (verticalMerge is { } merge)
                properties.VerticalMerge = new VerticalMerge { Val = merge };
            cell.Append(properties);
            cell.Append(text.Length == 0 ? new Paragraph() : Paragraph(text));
            return cell;
        }
    }
}
