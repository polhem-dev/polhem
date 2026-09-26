using System.ComponentModel;
using System.Data;
using Polhem.Definition;

namespace Polhem.Api.Client.UnitTests.FormData
{
    /// <summary>
    /// The preconditions and messages of <see cref="FormDataGuard"/>.
    /// </summary>
    /// <remarks>
    /// The message text itself is the point: it is the only thing a developer gets to read after wiring a form up
    /// wrong, and two copies would drift into two different explanations of the same error.
    /// </remarks>
    public class FormDataGuardTests
    {
        [Fact]
        [DisplayName("Without a connector the message names the operation and where to supply the connector")]
        public void RequireConnector_Null_ThrowsNamingOperationAndRemedy()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => FormDataGuard.RequireConnector(null, "LoadAsync"));

            Assert.Contains("LoadAsync", ex.Message, StringComparison.Ordinal);
            Assert.Contains("FormDataObject constructor", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("With no master row loaded the message says so explicitly")]
        public void RequireMasterRowId_NoRow_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => FormDataGuard.RequireMasterRowId(null));

            Assert.Contains("No master row is loaded", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A master table without the rowid column gives a message distinct from \"no master row\"")]
        public void RequireMasterRowId_MissingColumn_ThrowsDistinctMessage()
        {
            var table = new DataTable("master");
            table.Columns.Add("other", typeof(string));
            var row = table.NewRow();
            table.Rows.Add(row);

            var ex = Assert.Throws<InvalidOperationException>(() => FormDataGuard.RequireMasterRowId(row));

            Assert.Contains("missing", ex.Message, StringComparison.Ordinal);
            Assert.Contains(SysFields.RowId, ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("No master row is loaded", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A null rowid gives a message distinct from \"missing column\"")]
        public void RequireMasterRowId_NullValue_ThrowsDistinctMessage()
        {
            var row = NewMasterRow(DBNull.Value);

            var ex = Assert.Throws<InvalidOperationException>(() => FormDataGuard.RequireMasterRowId(row));

            Assert.Contains("null", ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("missing", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A rowid stored as a Guid or as a string yields the same value")]
        public void RequireMasterRowId_GuidOrString_BothParse()
        {
            var id = Guid.NewGuid();

            Assert.Equal(id, FormDataGuard.RequireMasterRowId(NewMasterRow(id, typeof(Guid))));
            Assert.Equal(id, FormDataGuard.RequireMasterRowId(NewMasterRow(id.ToString(), typeof(string))));
        }

        private static DataRow NewMasterRow(object value, Type columnType = null!)
        {
            var table = new DataTable("master");
            table.Columns.Add(SysFields.RowId, columnType ?? typeof(Guid));
            var row = table.NewRow();
            row[SysFields.RowId] = value;
            table.Rows.Add(row);
            return row;
        }
    }
}
