using System.ComponentModel;
using System.Globalization;
using ClosedXML.Excel;
using Polhem.Core.Serialization;
using Polhem.Tests.Shared;

namespace Polhem.Definition.Spreadsheet.UnitTests
{
    /// <summary>
    /// The workbook language changes the texts shown to people and nothing the reader depends on.
    /// </summary>
    public class FormWorkbookLanguageTests
    {
        public static TheoryData<FormWorkbookLanguage> Languages => FormWorkbookTemplateTests.Languages;

        [Fact]
        [DisplayName("Every column and Form item has a header in both languages, and an explanation in both or neither")]
        public void Catalog_EveryColumn_IsTranslated()
        {
            var columns = FormWorkbookCatalog.FormItems.Cast<ISheetColumn>()
                .Concat(FormWorkbookCatalog.TabularSheets.SelectMany(sheet => sheet.Columns))
                .ToList();
            Assert.NotEmpty(columns);

            foreach (var column in columns)
            {
                Assert.False(string.IsNullOrWhiteSpace(column.Label.En), $"'{column.Key}' has no English header.");
                Assert.False(string.IsNullOrWhiteSpace(column.Label.ZhTw), $"'{column.Key}' has no zh-TW header.");
                Assert.True(IsTranslated(column.Hint), $"The explanation of '{column.Key}' is in one language only.");
            }
        }

        [Fact]
        [DisplayName("Every line and header of the instructions sheet is in both languages or blank in both")]
        public void Catalog_Instructions_AreTranslated()
        {
            var (item, value, explanation) = FormWorkbookCatalog.FormHeaders;
            SheetText[] texts =
            [
                FormWorkbookCatalog.InstructionsSheet, item, value, explanation,
                .. FormWorkbookCatalog.InstructionLines, .. FormWorkbookCatalog.InstructionHeaders,
            ];

            Assert.All(texts, text => Assert.True(IsTranslated(text), $"'{text.En}{text.ZhTw}' is in one language only."));
        }

        [Theory]
        [MemberData(nameof(Languages))]
        [DisplayName("A written workbook names its instructions sheet in its language")]
        public void Write_NamesInstructionsSheetInLanguage(FormWorkbookLanguage language)
        {
            using var stream = new MemoryStream();
            FormWorkbookWriter.WriteBlank(stream, language);
            stream.Position = 0;
            using var workbook = new XLWorkbook(stream);

            Assert.Equal(FormWorkbookCatalog.InstructionsSheet.Get(language), workbook.Worksheets.First().Name);
            Assert.Equal(FormWorkbookCatalog.FieldColumns[0].Label.Get(language),
                workbook.Worksheet(FormWorkbookCatalog.FieldsSheet).Cell(FormWorkbookCatalog.LabelRow, 1).GetString());
        }

        [Fact]
        [DisplayName("A form written in English and in zh-TW reads back as the same form")]
        public void Write_EitherLanguage_ReadsBackTheSame()
        {
            var content = FormExporter.Load(Path.Combine(RepoRoot.Find(), "apps", "Polhem.Northwind", "Define"), "Order");

            var english = WriteAndRead(content, FormWorkbookLanguage.English);
            var chinese = WriteAndRead(content, FormWorkbookLanguage.TraditionalChinese);

            Assert.Empty(english.Issues);
            Assert.Empty(chinese.Issues);
            Assert.Equal(XmlCodec.Serialize(english.Content!.Schema), XmlCodec.Serialize(chinese.Content!.Schema));
            Assert.Equal(english.Content.ZhTw.OrderBy(p => p.Key, StringComparer.Ordinal),
                chinese.Content.ZhTw.OrderBy(p => p.Key, StringComparer.Ordinal));
            Assert.Equal(english.Content.Menu.EntryCaptionZhTw, chinese.Content.Menu.EntryCaptionZhTw);
        }

        [Theory]
        [MemberData(nameof(Languages))]
        [DisplayName("A blank workbook of either language filled in by hand reads back the fields")]
        public void FilledByHand_EitherLanguage_Reads(FormWorkbookLanguage language)
        {
            using var workbook = new FilledWorkbook(language)
                .Form("ProgId", "Quote")
                .Field(("FieldName", "quote_date"), ("Caption", "Quote Date"), ("Caption_zh_TW", "報價日期"), ("DbType", "Date"));

            var result = workbook.Read();

            Assert.Empty(result.Issues);
            Assert.Equal("quote_date", Assert.Single(result.Content!.Schema.Tables![0].Fields!).FieldName);
            Assert.Equal("報價日期", result.Content.ZhTw["Field.quote_date.Caption"]);
        }

        [Theory]
        [InlineData("en", FormWorkbookLanguage.English)]
        [InlineData("EN", FormWorkbookLanguage.English)]
        [InlineData("zh-TW", FormWorkbookLanguage.TraditionalChinese)]
        [InlineData("zh-tw", FormWorkbookLanguage.TraditionalChinese)]
        [DisplayName("TryParse accepts the language tags in any case")]
        public void TryParse_KnownTag_ReturnsLanguage(string tag, FormWorkbookLanguage expected)
        {
            Assert.True(FormWorkbookLanguages.TryParse(tag, out var language));
            Assert.Equal(expected, language);
            Assert.Equal(tag, FormWorkbookLanguages.ToTag(language), StringComparer.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("fr")]
        [InlineData("zh")]
        [InlineData("")]
        [InlineData(null)]
        [DisplayName("TryParse rejects any other tag")]
        public void TryParse_UnknownTag_ReturnsFalse(string? tag)
        {
            Assert.False(FormWorkbookLanguages.TryParse(tag, out _));
        }

        [Theory]
        [InlineData("zh-TW", FormWorkbookLanguage.TraditionalChinese)]
        [InlineData("zh-Hant", FormWorkbookLanguage.TraditionalChinese)]
        [InlineData("en-US", FormWorkbookLanguage.English)]
        [InlineData("ja-JP", FormWorkbookLanguage.English)]
        [DisplayName("FromCulture picks zh-TW for a Chinese culture and English for every other")]
        public void FromCulture_PicksLanguage(string culture, FormWorkbookLanguage expected)
        {
            Assert.Equal(expected, FormWorkbookLanguages.FromCulture(CultureInfo.GetCultureInfo(culture)));
        }

        private static bool IsTranslated(SheetText text)
            => string.IsNullOrWhiteSpace(text.En) == string.IsNullOrWhiteSpace(text.ZhTw);

        private static FormWorkbookReadResult WriteAndRead(FormWorkbookContent content, FormWorkbookLanguage language)
        {
            using var stream = new MemoryStream();
            FormWorkbookWriter.Write(content, stream, language);
            stream.Position = 0;
            return FormWorkbookReader.Read(stream);
        }
    }
}
