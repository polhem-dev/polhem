using System.ComponentModel;
using Polhem.Api.Contracts.AuditLog;
using Polhem.Business.AuditLog;
using Polhem.Definition.Logging;

namespace Polhem.Business.UnitTests.AuditLog
{
    /// <summary>
    /// Pure parsing coverage for the assembly-internal <c>ChangeDiffGramReader.Read</c> (called directly through InternalsVisibleTo
    /// with DataSet DiffGram strings): null / blank, malformed (XmlException swallowed, returns empty), an inserted row,
    /// a modified row paired with its before image emitting only the differences, an unmatched before row (delete), no sys_rowid (rowKey=null),
    /// and a before row without diffgr:id not becoming a delete.
    /// </summary>
    public class ChangeDiffGramReaderCoverageTests
    {
        private const string Diff = "urn:schemas-microsoft-com:xml-diffgram-v1";

        private static List<RecordFieldChange> Read(string? changesXml)
            => ChangeDiffGramReader.Read(changesXml);

        // ---- blank / malformed inputs ----

        [Fact]
        [DisplayName("A null input returns an empty list")]
        public void Read_Null_ReturnsEmpty()
        {
            var result = Read(null);

            Assert.Empty(result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t\n")]
        [DisplayName("An empty or whitespace-only string returns an empty list")]
        public void Read_BlankOrWhitespace_ReturnsEmpty(string input)
        {
            var result = Read(input);

            Assert.Empty(result);
        }

        [Theory]
        [InlineData("<broken><unclosed>")]
        [InlineData("<diffgr:diffgram></diffgr:diffgram>")] // undeclared prefix → XmlException
        [InlineData("<?xml version=\"1.0\"?>")]             // no root element
        [DisplayName("Malformed or non-DiffGram XML is swallowed as XmlException and returns an empty list")]
        public void Read_MalformedXml_ReturnsEmpty(string input)
        {
            var result = Read(input);

            Assert.Empty(result);
        }

        // ---- inserted rows ----

        [Fact]
        [DisplayName("An inserted row emits an Insert change for every non-row-key column (sys_rowid skipped, rowKey carried)")]
        public void Read_InsertedRow_EmitsInsertPerColumn()
        {
            var rowId = Guid.NewGuid().ToString();
            var xml =
                $"<diffgr:diffgram xmlns:diffgr=\"{Diff}\">" +
                "<NewDataSet>" +
                "<ft_customer diffgr:id=\"ft_customer1\" diffgr:hasChanges=\"inserted\">" +
                $"<sys_rowid>{rowId}</sys_rowid>" +
                "<cust_name>ACME</cust_name>" +
                "<city>Taipei</city>" +
                "</ft_customer>" +
                "</NewDataSet>" +
                "</diffgr:diffgram>";

            var result = Read(xml);

            Assert.Equal(2, result.Count);
            Assert.All(result, c => Assert.Equal(ChangeKind.Insert, c.RowState));
            Assert.All(result, c => Assert.Equal("ft_customer", c.TableName));
            Assert.All(result, c => Assert.Equal(rowId, c.RowKey));
            Assert.All(result, c => Assert.Null(c.OldValue));
            Assert.DoesNotContain(result, c => c.FieldName == "sys_rowid");
            var name = Assert.Single(result, c => c.FieldName == "cust_name");
            Assert.Equal("ACME", name.NewValue);
        }

        [Fact]
        [DisplayName("An inserted row without sys_rowid has a null rowKey")]
        public void Read_InsertedRowWithoutRowKey_RowKeyNull()
        {
            var xml =
                $"<diffgr:diffgram xmlns:diffgr=\"{Diff}\">" +
                "<NewDataSet>" +
                "<ft_note diffgr:id=\"ft_note1\" diffgr:hasChanges=\"inserted\">" +
                "<memo>Hello</memo>" +
                "</ft_note>" +
                "</NewDataSet>" +
                "</diffgr:diffgram>";

            var result = Read(xml);

            var change = Assert.Single(result);
            Assert.Null(change.RowKey);
            Assert.Equal("memo", change.FieldName);
            Assert.Equal(ChangeKind.Insert, change.RowState);
        }

        // ---- modified rows paired with before image ----

        [Fact]
        [DisplayName("A modified row paired with its before image emits Update only for the changed columns")]
        public void Read_ModifiedRow_EmitsOnlyChangedColumns()
        {
            var rowId = Guid.NewGuid().ToString();
            var xml =
                $"<diffgr:diffgram xmlns:diffgr=\"{Diff}\">" +
                "<NewDataSet>" +
                "<ft_customer diffgr:id=\"ft_customer1\">" +
                $"<sys_rowid>{rowId}</sys_rowid>" +
                "<cust_name>NEW</cust_name>" +
                "<city>Taipei</city>" +
                "</ft_customer>" +
                "</NewDataSet>" +
                $"<diffgr:before xmlns:diffgr=\"{Diff}\">" +
                "<ft_customer diffgr:id=\"ft_customer1\">" +
                $"<sys_rowid>{rowId}</sys_rowid>" +
                "<cust_name>OLD</cust_name>" +
                "<city>Taipei</city>" +
                "</ft_customer>" +
                "</diffgr:before>" +
                "</diffgr:diffgram>";

            var result = Read(xml);

            var change = Assert.Single(result);
            Assert.Equal(ChangeKind.Update, change.RowState);
            Assert.Equal("cust_name", change.FieldName);
            Assert.Equal("OLD", change.OldValue);
            Assert.Equal("NEW", change.NewValue);
            Assert.Equal(rowId, change.RowKey);
        }

        [Fact]
        [DisplayName("A before row matched by a modified row is not treated as a delete as well")]
        public void Read_ModifiedRow_MatchedBeforeNotTreatedAsDelete()
        {
            var rowId = Guid.NewGuid().ToString();
            var xml =
                $"<diffgr:diffgram xmlns:diffgr=\"{Diff}\">" +
                "<NewDataSet>" +
                "<ft_customer diffgr:id=\"ft_customer1\">" +
                $"<sys_rowid>{rowId}</sys_rowid>" +
                "<cust_name>NEW</cust_name>" +
                "</ft_customer>" +
                "</NewDataSet>" +
                $"<diffgr:before xmlns:diffgr=\"{Diff}\">" +
                "<ft_customer diffgr:id=\"ft_customer1\">" +
                $"<sys_rowid>{rowId}</sys_rowid>" +
                "<cust_name>OLD</cust_name>" +
                "</ft_customer>" +
                "</diffgr:before>" +
                "</diffgr:diffgram>";

            var result = Read(xml);

            Assert.DoesNotContain(result, c => c.RowState == ChangeKind.Delete);
        }

        // ---- unmatched before rows = deletes ----

        [Fact]
        [DisplayName("A before row without a matching current row is treated as a delete (sys_rowid skipped, new value null)")]
        public void Read_UnmatchedBeforeRow_EmitsDelete()
        {
            var rowId = Guid.NewGuid().ToString();
            var xml =
                $"<diffgr:diffgram xmlns:diffgr=\"{Diff}\">" +
                "<NewDataSet></NewDataSet>" +
                $"<diffgr:before xmlns:diffgr=\"{Diff}\">" +
                "<ft_customer diffgr:id=\"ft_customer9\">" +
                $"<sys_rowid>{rowId}</sys_rowid>" +
                "<cust_name>GONE</cust_name>" +
                "</ft_customer>" +
                "</diffgr:before>" +
                "</diffgr:diffgram>";

            var result = Read(xml);

            var change = Assert.Single(result);
            Assert.Equal(ChangeKind.Delete, change.RowState);
            Assert.Equal("cust_name", change.FieldName);
            Assert.Equal("GONE", change.OldValue);
            Assert.Null(change.NewValue);
            Assert.Equal(rowId, change.RowKey);
        }

        [Fact]
        [DisplayName("A before row without diffgr:id is not indexed and produces no delete")]
        public void Read_BeforeRowWithoutId_ProducesNoDelete()
        {
            var xml =
                $"<diffgr:diffgram xmlns:diffgr=\"{Diff}\">" +
                "<NewDataSet></NewDataSet>" +
                $"<diffgr:before xmlns:diffgr=\"{Diff}\">" +
                "<ft_customer>" +
                "<cust_name>NOID</cust_name>" +
                "</ft_customer>" +
                "</diffgr:before>" +
                "</diffgr:diffgram>";

            var result = Read(xml);

            Assert.Empty(result);
        }
    }
}
