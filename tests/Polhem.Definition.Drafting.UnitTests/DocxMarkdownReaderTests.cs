using System.ComponentModel;
using DocumentFormat.OpenXml.Wordprocessing;
using Polhem.Definition.Drafting.Documents;
using static Polhem.Definition.Drafting.UnitTests.DocxBuilder;

namespace Polhem.Definition.Drafting.UnitTests
{
    public class DocxMarkdownReaderTests
    {
        [Fact]
        [DisplayName("Paragraphs become lines, headings get # marks and list items get dashes")]
        public void Read_Paragraphs_WritesHeadingsAndListItems()
        {
            using var docx = Build(
                Paragraph("Leave request", styleId: "Title"),
                Paragraph("Applicant", styleId: "Heading2"),
                Paragraph("Fill in every field."),
                Paragraph("Annual leave", listItem: true),
                Paragraph("   "));

            var markdown = DocxMarkdownReader.Read(docx);

            Assert.Equal("# Leave request\n\n## Applicant\n\nFill in every field.\n\n- Annual leave\n", markdown);
        }

        [Fact]
        [DisplayName("A table becomes a pipe table whose first row is the header")]
        public void Read_Table_WritesPipeTable()
        {
            using var docx = Build(Table(
                Row(Cell("Name"), Cell("Department")),
                Row(Cell("Ada"), Cell("R&D"))));

            var markdown = DocxMarkdownReader.Read(docx);

            Assert.Equal("| Name | Department |\n| --- | --- |\n| Ada | R&D |\n", markdown);
        }

        [Fact]
        [DisplayName("A horizontally merged cell keeps the columns aligned and writes its text once")]
        public void Read_GridSpan_PadsCoveredColumns()
        {
            using var docx = Build(Table(
                Row(Cell("Period", gridSpan: 2), Cell("Days")),
                Row(Cell("From"), Cell("To"), Cell(""))));

            var markdown = DocxMarkdownReader.Read(docx);

            Assert.Equal("| Period |  | Days |\n| --- | --- | --- |\n| From | To |  |\n", markdown);
        }

        [Fact]
        [DisplayName("A vertically merged cell writes its text in the first row and leaves the rows it covers empty")]
        public void Read_VerticalMerge_LeavesContinuationEmpty()
        {
            using var docx = Build(Table(
                Row(Cell("Type"), Cell("Option")),
                Row(Cell("Leave", verticalMerge: MergedCellValues.Restart), Cell("Annual")),
                Row(Cell("Leave", verticalMerge: MergedCellValues.Continue), Cell("Sick"))));

            var markdown = DocxMarkdownReader.Read(docx);

            Assert.Equal("| Type | Option |\n| --- | --- |\n| Leave | Annual |\n|  | Sick |\n", markdown);
        }

        [Fact]
        [DisplayName("Empty cells stay empty and a short row is padded to the widest row")]
        public void Read_EmptyAndMissingCells_ArePadded()
        {
            using var docx = Build(Table(
                Row(Cell("Signature"), Cell(""), Cell("Date")),
                Row(Cell("Manager"))));

            var markdown = DocxMarkdownReader.Read(docx);

            Assert.Equal("| Signature |  | Date |\n| --- | --- | --- |\n| Manager |  |  |\n", markdown);
        }

        [Fact]
        [DisplayName("A pipe inside a cell is escaped and the paragraphs of a cell are joined with a line break")]
        public void Read_CellWithPipeAndParagraphs_EscapesAndJoins()
        {
            var cell = Cell("Yes | No");
            cell.Append(Paragraph("(tick one)"));
            using var docx = Build(Table(Row(cell)));

            var markdown = DocxMarkdownReader.Read(docx);

            Assert.Equal("| Yes \\| No<br>(tick one) |\n| --- |\n", markdown);
        }

        [Fact]
        [DisplayName("Paragraphs and tables keep their document order")]
        public void Read_MixedBlocks_KeepsOrder()
        {
            using var docx = Build(
                Paragraph("Before"),
                Table(Row(Cell("A"))),
                Paragraph("After"));

            var markdown = DocxMarkdownReader.Read(docx);

            Assert.Equal("Before\n\n| A |\n| --- |\n\nAfter\n", markdown);
        }
    }
}
