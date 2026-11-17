using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;

namespace Polhem.Definition.UnitTests.Forms
{
    public class FormSchemaQueryFieldsTests
    {
        private static FormSchema BuildSchema(string listFields = "", string queryFields = "", string dbTableName = "ft_order")
        {
            var schema = new FormSchema("Order", "Order") { ListFields = listFields, QueryFields = queryFields };
            var master = schema.Tables!.Add("Order", "Order");
            master.DbTableName = dbTableName;
            master.Fields!.Add(new FormField(SysFields.RowId, "Row ID", FieldDbType.Guid));
            master.Fields!.Add(new FormField("sys_id", "Order No", FieldDbType.String));
            master.Fields!.Add(new FormField("order_date", "Date", FieldDbType.Date));
            master.Fields!.Add(new FormField("ref_customer_name", "Customer", FieldDbType.String) { Type = FieldType.RelationField });
            master.Fields!.Add(new FormField("password", "Password", FieldDbType.String));
            var detail = schema.Tables!.Add("OrderDetail", "Detail");
            detail.Fields!.Add(new FormField("quantity", "Quantity", FieldDbType.Integer));
            return schema;
        }

        [Fact]
        [DisplayName("GetQueryFields returns the QueryFields in their declared order")]
        public void GetQueryFields_Declared_ReturnsInOrder()
        {
            var schema = BuildSchema(listFields: "sys_id", queryFields: "order_date, ref_customer_name ,sys_id");

            var names = schema.GetQueryFields().Select(f => f.FieldName);

            Assert.Equal(["order_date", "ref_customer_name", "sys_id"], names);
        }

        [Fact]
        [DisplayName("GetQueryFields falls back to ListFields when QueryFields is empty")]
        public void GetQueryFields_Empty_FallsBackToListFields()
        {
            var schema = BuildSchema(listFields: "sys_id,order_date");

            Assert.Equal(["sys_id", "order_date"], schema.GetQueryFields().Select(f => f.FieldName));
        }

        [Fact]
        [DisplayName("GetQueryFields offers nothing when both QueryFields and ListFields are empty, or there is no master table")]
        public void GetQueryFields_NothingDeclared_ReturnsEmpty()
        {
            Assert.Empty(BuildSchema().GetQueryFields());
            Assert.Empty(new FormSchema { QueryFields = "sys_id" }.GetQueryFields());
        }

        [Fact]
        [DisplayName("GetQueryFields skips unknown names, detail fields, sys_rowid and repeats in other casing")]
        public void GetQueryFields_SkipsWhatIsNotAQueryField()
        {
            var schema = BuildSchema(queryFields: "sys_id,unknown,quantity,sys_rowid,SYS_ID");

            Assert.Equal(["sys_id"], schema.GetQueryFields().Select(f => f.FieldName));
        }

        [Fact]
        [DisplayName("GetQueryFields leaves out a stored column ProtectedFields lists, matched by the database table name")]
        public void GetQueryFields_ProtectedColumn_IsLeftOut()
        {
            var userSchema = BuildSchema(queryFields: "sys_id,password", dbTableName: "st_user");
            var otherSchema = BuildSchema(queryFields: "sys_id,password");

            Assert.Equal(["sys_id"], userSchema.GetQueryFields().Select(f => f.FieldName));
            Assert.Equal(["sys_id", "password"], otherSchema.GetQueryFields().Select(f => f.FieldName));
        }

        [Fact]
        [DisplayName("Clone copies QueryFields")]
        public void Clone_CopiesQueryFields()
        {
            var schema = BuildSchema(queryFields: "sys_id,order_date");

            Assert.Equal("sys_id,order_date", schema.Clone().QueryFields);
        }

        [Fact]
        [DisplayName("QueryFields round-trips through XML and is not written when empty")]
        public void QueryFields_XmlRoundTrip_OmitsEmpty()
        {
            var declared = XmlCodec.Serialize(BuildSchema(queryFields: "sys_id,order_date"));
            var empty = XmlCodec.Serialize(BuildSchema());

            Assert.Equal("sys_id,order_date", XmlCodec.Deserialize<FormSchema>(declared)!.QueryFields);
            Assert.DoesNotContain("QueryFields", empty, StringComparison.Ordinal);
        }
    }
}
