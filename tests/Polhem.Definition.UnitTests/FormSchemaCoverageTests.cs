using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Forms;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// Additional tests for <see cref="FormSchema"/>: the master, named-table and missing-field branches of
    /// <see cref="FormSchema.FindField"/>, the early return of <see cref="FormSchema.GetLookupFields"/> when there
    /// is no master table, and <see cref="FormSchema.ToString"/>.
    /// </summary>
    public class FormSchemaCoverageTests
    {
        private static FormSchema BuildMasterDetailSchema()
        {
            var schema = new FormSchema("Order", "訂單") { CategoryId = "company" };
            var master = schema.Tables!.Add("Order", "訂單");
            master.Fields!.Add("sys_id", "編號", FieldDbType.String);
            master.Fields.Add("sys_name", "名稱", FieldDbType.String);

            var detail = schema.Tables!.Add("OrderLine", "明細");
            detail.Fields!.Add("qty", "數量", FieldDbType.Integer);
            return schema;
        }

        [Fact]
        [DisplayName("FindField resolves the master table and returns an existing field when tableName is empty")]
        public void FindField_EmptyTableName_ResolvesMasterField()
        {
            var schema = BuildMasterDetailSchema();

            var field = schema.FindField("sys_id");

            Assert.NotNull(field);
            Assert.Equal("sys_id", field!.FieldName);
        }

        [Fact]
        [DisplayName("FindField returns the field of an existing named table")]
        public void FindField_NamedTablePresent_ResolvesField()
        {
            var schema = BuildMasterDetailSchema();

            var field = schema.FindField("qty", "OrderLine");

            Assert.NotNull(field);
            Assert.Equal("qty", field!.FieldName);
        }

        [Fact]
        [DisplayName("FindField returns null when the named table does not exist")]
        public void FindField_NamedTableAbsent_ReturnsNull()
        {
            var schema = BuildMasterDetailSchema();

            var field = schema.FindField("qty", "NoSuchTable");

            Assert.Null(field);
        }

        [Fact]
        [DisplayName("FindField returns null when the table exists but the field does not")]
        public void FindField_FieldAbsent_ReturnsNull()
        {
            var schema = BuildMasterDetailSchema();

            var field = schema.FindField("no_such_field", "OrderLine");

            Assert.Null(field);
        }

        [Fact]
        [DisplayName("FindField returns null when there is no master table (the ProgId maps to no table)")]
        public void FindField_NoMasterTable_ReturnsNull()
        {
            var schema = new FormSchema();   // An empty ProgId leaves MasterTable null.

            var field = schema.FindField("sys_id");

            Assert.Null(field);
        }

        [Fact]
        [DisplayName("GetLookupFields returns an empty list when there is no master table")]
        public void GetLookupFields_NoMasterTable_ReturnsEmpty()
        {
            var schema = new FormSchema();   // No master table.

            var fields = schema.GetLookupFields();

            Assert.Empty(fields);
        }

        [Fact]
        [DisplayName("GetLookupFields returns the default lookup fields sys_id and sys_name when there is a master table")]
        public void GetLookupFields_WithMaster_ReturnsDefaultFields()
        {
            var schema = BuildMasterDetailSchema();

            var fields = schema.GetLookupFields();

            Assert.Contains(fields, f => f.FieldName == "sys_id");
            Assert.Contains(fields, f => f.FieldName == "sys_name");
        }

        [Fact]
        [DisplayName("ToString returns the 'ProgId - DisplayName' format")]
        public void ToString_ReturnsProgIdDashDisplayName()
        {
            var schema = new FormSchema("Order", "訂單");

            Assert.Equal("Order - 訂單", schema.ToString());
        }
    }
}
