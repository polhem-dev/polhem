using System.ComponentModel;
using Microsoft.Extensions.AI;
using Polhem.Definition.Drafting.Documents;
using static Polhem.Definition.Drafting.UnitTests.DocxBuilder;

namespace Polhem.Definition.Drafting.UnitTests
{
    public sealed class DocumentSourceTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "polhem-drafting-" + Guid.NewGuid().ToString("N"));

        public DocumentSourceTests()
        {
            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        [Theory]
        [InlineData("form.pdf", "application/pdf")]
        [InlineData("front.png", "image/png")]
        [InlineData("back.JPG", "image/jpeg")]
        [InlineData("scan.jpeg", "image/jpeg")]
        [DisplayName("FromFile reads a PDF or an image as data content with its media type and file name")]
        public void FromFile_PdfOrImage_ReturnsDataContentWithMediaType(string fileName, string mediaType)
        {
            var path = WriteFile(fileName, [1, 2, 3]);

            var content = Assert.IsType<DataContent>(DocumentSource.FromFile(path));

            Assert.Equal(mediaType, content.MediaType);
            Assert.Equal(fileName, content.Name);
            Assert.Equal(new byte[] { 1, 2, 3 }, content.Data.ToArray());
        }

        [Fact]
        [DisplayName("FromFile reads a Word document as Markdown text")]
        public void FromFile_Docx_ReturnsMarkdownText()
        {
            using var docx = Build(Paragraph("Leave request", styleId: "Heading1"));
            var path = WriteFile("leave.docx", docx.ToArray());

            var content = Assert.IsType<TextContent>(DocumentSource.FromFile(path));

            Assert.Equal("# Leave request\n", content.Text);
        }

        [Theory]
        [InlineData("notes.txt")]
        [InlineData("sheet.xlsx")]
        [InlineData("old.doc")]
        [DisplayName("FromFile refuses a file type it does not read")]
        public void FromFile_UnsupportedType_Throws(string fileName)
        {
            var path = WriteFile(fileName, [1]);

            var exception = Assert.Throws<NotSupportedException>(() => DocumentSource.FromFile(path));

            Assert.Contains(fileName, exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("FromFile refuses an image over the image limit instead of cutting it short")]
        public void FromFile_ImageOverLimit_Throws()
        {
            var path = WriteFile("big.png", new byte[DocumentSource.MaxImageBytes + 1]);

            var exception = Assert.Throws<DocumentTooLargeException>(() => DocumentSource.FromFile(path));

            Assert.Equal("big.png", exception.FileName);
            Assert.Equal(DocumentSource.MaxImageBytes + 1, exception.Length);
            Assert.Equal(DocumentSource.MaxImageBytes, exception.MaxLength);
        }

        [Fact]
        [DisplayName("FromFile accepts an image exactly at the image limit")]
        public void FromFile_ImageAtLimit_IsAccepted()
        {
            var path = WriteFile("edge.png", new byte[DocumentSource.MaxImageBytes]);

            var content = Assert.IsType<DataContent>(DocumentSource.FromFile(path));

            Assert.Equal(DocumentSource.MaxImageBytes, content.Data.Length);
        }

        [Fact]
        [DisplayName("FromStream applies the PDF limit, which is larger than the image limit")]
        public void FromStream_PdfOverImageLimit_IsAccepted()
        {
            using var stream = new MemoryStream(new byte[DocumentSource.MaxImageBytes + 1]);

            var content = Assert.IsType<DataContent>(DocumentSource.FromStream(stream, "large.pdf"));

            Assert.Equal("application/pdf", content.MediaType);
        }

        [Fact]
        [DisplayName("FromStream refuses an oversized document from a stream that cannot seek")]
        public void FromStream_NonSeekableOverLimit_Throws()
        {
            using var stream = new NonSeekableStream(new byte[DocumentSource.MaxImageBytes + 1]);

            Assert.Throws<DocumentTooLargeException>(() => DocumentSource.FromStream(stream, "photo.jpg"));
        }

        [Fact]
        [DisplayName("FromStream reads a Word document from a stream that cannot seek")]
        public void FromStream_NonSeekableDocx_ReturnsMarkdown()
        {
            using var docx = Build(Paragraph("Applicant"));
            using var stream = new NonSeekableStream(docx.ToArray());

            var content = Assert.IsType<TextContent>(DocumentSource.FromStream(stream, "form.docx"));

            Assert.Equal("Applicant\n", content.Text);
        }

        private string WriteFile(string fileName, byte[] bytes)
        {
            var path = Path.Combine(_directory, fileName);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
        {
            public override bool CanSeek => false;

            public override long Length => throw new NotSupportedException();
        }
    }
}
