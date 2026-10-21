using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;

namespace Polhem.Definition.Spreadsheet.UnitTests
{
    /// <summary>
    /// What <see cref="FormSchemaConventions"/> fills in for a form that lists only its business fields.
    /// </summary>
    public class FormSchemaConventionsTests
    {
        internal static FormWorkbookContent MinimalQuote()
        {
            var schema = new FormSchema("Quote", "Quote");
            var master = schema.Tables!.Add("Quote", "Quote");
            master.Fields!.Add(new FormField("sys_id", "Quote No", FieldDbType.String) { MaxLength = 20 });
            master.Fields!.Add("quote_date", "Quote Date", FieldDbType.Date);
            master.Fields!.Add(new FormField("customer_rowid", "Customer", FieldDbType.String) { RelationProgId = "Customer" });
            var lines = schema.Tables.Add("QuoteLine", "Quote Lines");
            lines.Fields!.Add("qty", "Quantity", FieldDbType.Integer);

            var content = new FormWorkbookContent(schema);
            content.ZhTw["Field.customer_rowid.Caption"] = "客戶";
            return content;
        }

        [Fact]
        [DisplayName("A form without a category, table names or system fields gets them by convention")]
        public void Apply_MinimalForm_FillsConventions()
        {
            var content = MinimalQuote();

            var issues = FormSchemaConventions.Apply(content);

            Assert.Empty(issues);
            var schema = content.Schema;
            Assert.Equal(DbCategoryIds.Company, schema.CategoryId);
            Assert.Equal("ft_quote", schema.Tables!["Quote"].DbTableName);
            Assert.Equal("ft_quote_line", schema.Tables["QuoteLine"].DbTableName);
            Assert.Equal(["sys_no", "sys_rowid", "sys_id"], schema.Tables["Quote"].Fields!.Take(3).Select(f => f.FieldName));
            Assert.Equal(["sys_no", "sys_rowid", "sys_master_rowid", "qty"], schema.Tables["QuoteLine"].Fields!.Select(f => f.FieldName));
            Assert.False(schema.Tables["Quote"].Fields!["sys_rowid"].Visible);
        }

        [Fact]
        [DisplayName("A relation field gets the ref fields and mappings of the related form, with captions in both languages")]
        public void Apply_RelationField_AddsRefFields()
        {
            var content = MinimalQuote();

            FormSchemaConventions.Apply(content);

            var fields = content.Schema.Tables!["Quote"].Fields!;
            var relation = fields["customer_rowid"];
            Assert.Equal(FieldDbType.Guid, relation.DbType);
            Assert.Equal(["sys_id->ref_customer_id", "sys_name->ref_customer_name"],
                relation.RelationFieldMappings!.Select(m => $"{m.SourceField}->{m.DestinationField}"));
            int at = fields.IndexOf(relation);
            Assert.Equal("ref_customer_id", fields[at + 1].FieldName);
            Assert.Equal("ref_customer_name", fields[at + 2].FieldName);
            Assert.Equal(FieldType.RelationField, fields[at + 1].Type);
            Assert.Equal("Customer Code", fields[at + 1].Caption);
            Assert.Equal("客戶代號", content.ZhTw["Field.ref_customer_id.Caption"]);
            Assert.Equal("客戶名稱", content.ZhTw["Field.ref_customer_name.Caption"]);
        }

        [Fact]
        [DisplayName("Applying the conventions twice changes nothing the second time")]
        public void Apply_Twice_IsIdempotent()
        {
            var content = MinimalQuote();
            FormSchemaConventions.Apply(content);
            var once = XmlCodec.Serialize(content.Schema);

            FormSchemaConventions.Apply(content);

            Assert.Equal(once, XmlCodec.Serialize(content.Schema));
        }

        [Fact]
        [DisplayName("A form whose tables do not include one named after the ProgId is refused")]
        public void Apply_NoMasterTable_ReportsError()
        {
            var schema = new FormSchema("Quote", "Quote");
            schema.Tables!.Add("Header", "Header");

            var issues = FormSchemaConventions.Apply(new FormWorkbookContent(schema));

            Assert.Equal(IssueSeverity.Error, Assert.Single(issues).Severity);
        }

        [Theory]
        [InlineData("Order", "order")]
        [InlineData("OrderDetail", "order_detail")]
        [InlineData("HTTPLog", "http_log")]
        [InlineData("Line2Item", "line2_item")]
        [InlineData("already_snake", "already_snake")]
        [DisplayName("ToSnakeCase splits PascalCase words and acronyms with underscores")]
        public void ToSnakeCase_Converts(string name, string expected)
        {
            Assert.Equal(expected, FormSchemaConventions.ToSnakeCase(name));
        }
    }
}
