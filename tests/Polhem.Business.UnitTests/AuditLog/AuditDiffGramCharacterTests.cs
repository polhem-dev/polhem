using System.ComponentModel;
using System.Data;
using System.Globalization;
using Polhem.Api.Contracts.AuditLog;
using Polhem.Business.AuditLog;
using Polhem.Definition.Logging;

namespace Polhem.Business.UnitTests.AuditLog
{
    /// <summary>
    /// The payload <c>AuditDiffGram</c> writes when a string value contains characters XML 1.0 forbids or a CR: writing must not throw,
    /// and the old and new values read back through <c>ChangeDiffGramReader</c> must match the originals exactly. The only exception is a lone surrogate:
    /// no XML form can express it, so it becomes U+FFFD.
    /// </summary>
    /// <remarks>
    /// Both form and deployment-level audits serialize after the database transaction commits. If the writer threw here, a call that had already saved
    /// would report failure, and the audit would not be written either. Characters are passed as code points and built into strings so that NUL and lone surrogates
    /// do not go through xUnit's theory data serialization.
    /// </remarks>
    public class AuditDiffGramCharacterTests
    {
        private const string TableName = "ft_note";
        private const string RowKey = "row-1";
        private const string Column = "memo";

        private static DataSet NewDataSet()
        {
            var table = new DataTable(TableName) { Locale = CultureInfo.InvariantCulture };
            table.Columns.Add("sys_rowid", typeof(string));
            table.Columns.Add(Column, typeof(string));
            var dataSet = new DataSet("form") { Locale = CultureInfo.InvariantCulture };
            dataSet.Tables.Add(table);
            return dataSet;
        }

        private static List<RecordFieldChange> RoundTrip(DataSet dataSet)
        {
            using var changes = dataSet.GetChanges()!;
            return ChangeDiffGramReader.Read(AuditDiffGram.Serialize(changes));
        }

        private static RecordFieldChange ModifiedMemo(string before, string after)
        {
            var dataSet = NewDataSet();
            var row = dataSet.Tables[TableName]!.Rows.Add(RowKey, before);
            dataSet.AcceptChanges();
            row[Column] = after;
            return Assert.Single(RoundTrip(dataSet));
        }

        [Theory]
        [InlineData(0x0000)]
        [InlineData(0x0001)]
        [InlineData(0x001F)]
        [InlineData(0xFFFE)]
        [InlineData(0xFFFF)]
        [DisplayName("Characters XML forbids can still be written into the payload, and the old and new values read back exactly")]
        public void Serialize_InvalidXmlCharacter_RoundTripsExactly(int codePoint)
        {
            char character = (char)codePoint;
            string before = "舊" + character + "值";
            string after = "新" + character + "值";

            var change = ModifiedMemo(before, after);

            Assert.Equal(before, change.OldValue);
            Assert.Equal(after, change.NewValue);
        }

        [Theory]
        [InlineData("a\r\nb")]
        [InlineData("a\rb")]
        [InlineData("行尾\r")]
        [DisplayName("CR and CRLF are not normalized to LF when read back")]
        public void Serialize_CarriageReturn_IsPreserved(string after)
        {
            var change = ModifiedMemo("原值", after);

            Assert.Equal(after, change.NewValue);
        }

        [Theory]
        [InlineData(0xD842)]
        [InlineData(0xDC00)]
        [DisplayName("A lone surrogate cannot be expressed in XML, so both old and new values get U+FFFD instead of throwing")]
        public void Serialize_LoneSurrogate_ReplacedWithReplacementCharacter(int codePoint)
        {
            char surrogate = (char)codePoint;

            var change = ModifiedMemo("舊" + surrogate + "值", "新值" + surrogate);

            Assert.Equal("舊\uFFFD值", change.OldValue);
            Assert.Equal("新值\uFFFD", change.NewValue);
        }

        [Fact]
        [DisplayName("A surrogate pair (emoji) is not affected by the replacement")]
        public void Serialize_SurrogatePair_IsPreserved()
        {
            string after = "讚😀，孤\uD842";

            var change = ModifiedMemo("原值", after);

            Assert.Equal("讚😀，孤\uFFFD", change.NewValue);
        }

        [Fact]
        [DisplayName("The before image of a deleted row with invalid characters can still be written and read back")]
        public void Serialize_DeletedRowWithInvalidCharacters_RestoresBeforeImage()
        {
            var dataSet = NewDataSet();
            var row = dataSet.Tables[TableName]!.Rows.Add(RowKey, "刪\u0001除\r\n前\uDC00");
            dataSet.AcceptChanges();
            row.Delete();

            var change = Assert.Single(RoundTrip(dataSet));

            Assert.Equal(ChangeKind.Delete, change.RowState);
            Assert.Equal("刪\u0001除\r\n前\uFFFD", change.OldValue);
        }

        [Fact]
        [DisplayName("The deployment-level ForInsert uses the same serialization, and a value with a control character reads back as well")]
        public void ForInsert_ValueWithControlCharacter_RoundTrips()
        {
            string payload = AuditDiffGram.ForInsert("st_api_key", [("sys_name", "App\u0001Name")]);

            var change = Assert.Single(ChangeDiffGramReader.Read(payload), c => c.FieldName == "sys_name");

            Assert.Equal("App\u0001Name", change.NewValue);
        }
    }
}
