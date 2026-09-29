using System.ComponentModel;
using System.Text.Json;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.UnitTests.Forms
{
    /// <summary>
    /// Definition-layer lookup tests: serialization round-trips and Clone of FormField.DisplayFields and
    /// FormSchema.LookupFields, and how FormLayoutGenerator resolves relation fields.
    /// </summary>
    public class LookupDefinitionTests
    {
        /// <summary>
        /// Builds a test schema with a relation field (mirroring the shape of the Project schema in tests/Define).
        /// </summary>
        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Order", "訂單")
            {
                CategoryId = "company",
                ListFields = "sys_id,sys_name",
                LookupFields = "sys_id,sys_name,customer_grade",
            };
            var table = schema.Tables!.Add("Order", "訂單");
            table.Fields!.Add(new FormField("sys_rowid", "唯一識別", FieldDbType.Guid));
            table.Fields!.Add(new FormField("sys_id", "單號", FieldDbType.String));
            table.Fields!.Add(new FormField("sys_name", "名稱", FieldDbType.String));

            var customerField = new FormField("customer_rowid", "客戶", FieldDbType.Guid)
            {
                RelationProgId = "Customer",
                DisplayFields = "ref_customer_name",
            };
            customerField.RelationFieldMappings!.Add("sys_id", "ref_customer_id");
            customerField.RelationFieldMappings!.Add("sys_name", "ref_customer_name");
            table.Fields!.Add(customerField);
            table.Fields!.Add(new FormField("ref_customer_id", "客戶代碼", FieldDbType.String, FieldType.RelationField));
            table.Fields!.Add(new FormField("ref_customer_name", "客戶名稱", FieldDbType.String, FieldType.RelationField));
            return schema;
        }

        [Fact]
        [DisplayName("DisplayFields and LookupFields are restored through XML serialization")]
        public void XmlRoundTrip_RestoresDisplayFieldAndLookupFields()
        {
            var schema = BuildSchema();

            var xml = XmlCodec.Serialize(schema);
            var restored = XmlCodec.Deserialize<FormSchema>(xml);

            Assert.NotNull(restored);
            Assert.Equal("sys_id,sys_name,customer_grade", restored.LookupFields);
            Assert.Equal("ref_customer_name", restored.Tables![0].Fields!["customer_rowid"].DisplayFields);
        }

        [Fact]
        [DisplayName("DisplayFields and LookupFields appear in the JSON output (consumed one way by JS frontends)")]
        public void JsonSerialize_CarriesDisplayFieldAndLookupFields()
        {
            var schema = BuildSchema();

            var json = JsonCodec.Serialize(schema);
            var restored = JsonCodec.Deserialize<FormSchema>(json);

            // The JSON wire is one-way for JS frontends: getter-only collections such as
            // `Tables` do not repopulate on deserialize, so field-level values are asserted
            // on the serialized payload and only top-level properties on the restored object.
            // The payload is inspected through JsonDocument rather than by substring so the
            // assertion tests the value rather than the writer's whitespace.
            using var document = JsonDocument.Parse(json);
            var field = document.RootElement
                .GetProperty("tables")[0]
                .GetProperty("fields")
                .EnumerateArray()
                .Single(f => f.GetProperty("fieldName").GetString() == "customer_rowid");
            Assert.Equal("ref_customer_name", field.GetProperty("displayFields").GetString());
            Assert.NotNull(restored);
            Assert.Equal("sys_id,sys_name,customer_grade", restored.LookupFields);
        }

        [Fact]
        [DisplayName("DisplayFields and LookupFields that are not set do not appear in the XML output")]
        public void XmlSerialize_DefaultsOmitted()
        {
            var schema = new FormSchema("Customer", "客戶") { CategoryId = "company" };
            var table = schema.Tables!.Add("Customer", "客戶");
            table.Fields!.Add(new FormField("sys_id", "代碼", FieldDbType.String));

            var xml = XmlCodec.Serialize(schema);

            Assert.DoesNotContain("LookupFields", xml);
            Assert.DoesNotContain("DisplayField", xml);
        }

        [Fact]
        [DisplayName("Clone copies DisplayFields and LookupFields")]
        public void Clone_CopiesDisplayFieldAndLookupFields()
        {
            var schema = BuildSchema();

            var clone = schema.Clone();

            Assert.Equal("sys_id,sys_name,customer_grade", clone.LookupFields);
            Assert.Equal("ref_customer_name", clone.Tables![0].Fields!["customer_rowid"].DisplayFields);
        }

        [Fact]
        [DisplayName("GetLookupFields returns fields in declaration order and skips fields missing from the master")]
        public void GetLookupFields_Declared_SkipsMissingFields()
        {
            var schema = BuildSchema();

            var fields = schema.GetLookupFields();

            // Declared "sys_id,sys_name,customer_grade" — customer_grade is not on the master table.
            Assert.Equal(2, fields.Count);
            Assert.Equal("sys_id", fields[0].FieldName);
            Assert.Equal("sys_name", fields[1].FieldName);
        }

        [Fact]
        [DisplayName("GetLookupFields defaults to sys_id and sys_name when nothing is declared")]
        public void GetLookupFields_NotDeclared_DefaultsToIdAndName()
        {
            var schema = BuildSchema();
            schema.LookupFields = string.Empty;

            var fields = schema.GetLookupFields();

            Assert.Equal(2, fields.Count);
            Assert.Equal("sys_id", fields[0].FieldName);
            Assert.Equal("sys_name", fields[1].FieldName);
        }

        [Fact]
        [DisplayName("GetLookupFields excludes a declared sys_rowid (callers always prepend it)")]
        public void GetLookupFields_DeclaredRowId_Excluded()
        {
            var schema = BuildSchema();
            schema.LookupFields = "sys_rowid,sys_id";

            var fields = schema.GetLookupFields();

            Assert.Single(fields);
            Assert.Equal("sys_id", fields[0].FieldName);
        }

        [Fact]
        [DisplayName("GetLookupFields default set returns only sys_id when the master has no sys_name")]
        public void GetLookupFields_MasterWithoutSysName_DefaultsToIdOnly()
        {
            var schema = new FormSchema("Unit", "單位") { CategoryId = "common" };
            var table = schema.Tables!.Add("Unit", "單位");
            table.Fields!.Add(new FormField("sys_id", "代碼", FieldDbType.String));

            var fields = schema.GetLookupFields();

            Assert.Single(fields);
            Assert.Equal("sys_id", fields[0].FieldName);
        }

        [Fact]
        [DisplayName("GetLookupLayout contains the lookup fields and a hidden sys_rowid, and allows no edit actions")]
        public void GetLookupLayout_BuildsSelectionOnlyGrid()
        {
            var schema = BuildSchema();

            var layout = schema.GetLookupLayout();

            Assert.Equal(GridControlAllowActions.None, layout.AllowActions);
            Assert.Equal(3, layout.Columns!.Count);
            Assert.Equal("sys_id", layout.Columns[0].FieldName);
            Assert.Equal("sys_name", layout.Columns[1].FieldName);
            Assert.Equal("sys_rowid", layout.Columns[2].FieldName);
            Assert.False(layout.Columns[2].Visible);
        }

        [Fact]
        [DisplayName("FormLayoutGenerator.Generate resolves a relation field to a ButtonEdit carrying DisplayFields")]
        public void Generate_RelationField_ResolvesButtonEditWithDisplayField()
        {
            var schema = BuildSchema();

            var layout = FormLayoutGenerator.Generate(schema, "default");
            var field = layout.Sections![0].Fields!.First(f => f.FieldName == "customer_rowid");

            Assert.Equal(ControlType.ButtonEdit, field.ControlType);
            Assert.Equal("ref_customer_name", field.DisplayFields);
        }

        [Fact]
        [DisplayName("FormLayoutGenerator.Generate still emits a ButtonEdit for a relation field with Visible=false, and fields covered by DisplayFields are not emitted separately")]
        public void Generate_RelationField_CoversDisplayFields()
        {
            // The intuitive setup: the raw rowid value (a Guid) is never seen, so it is Visible=false.
            // The ref fields are the values actually seen, so they are Visible=true. The ButtonEdit is generated from `RelationProgId`
            // and carries `DisplayFields`, and the covered fields no longer appear on their own.
            var schema = BuildSchema();
            var customerField = schema.MasterTable!.Fields!["customer_rowid"];
            customerField.Visible = false;
            customerField.DisplayFields = string.Empty;  // By convention, `ref_customer_id` plus `ref_customer_name`.

            var layout = FormLayoutGenerator.Generate(schema, "default");
            var fields = layout.Sections![0].Fields!;

            // The relation field is still emitted as the editing entry point.
            var lookup = fields.First(f => f.FieldName == "customer_rowid");
            Assert.Equal(ControlType.ButtonEdit, lookup.ControlType);
            Assert.Equal("ref_customer_id,ref_customer_name", lookup.DisplayFields);
            // Fields covered by the composite display are not emitted separately.
            Assert.DoesNotContain(fields, f => f.FieldName == "ref_customer_id");
            Assert.DoesNotContain(fields, f => f.FieldName == "ref_customer_name");
        }

        [Fact]
        [DisplayName("FormLayoutGenerator.Generate still emits ref fields not covered by DisplayFields on their own (no information is lost)")]
        public void Generate_UncoveredRelationDisplayField_StillEmitted()
        {
            var schema = BuildSchema();
            var customerField = schema.MasterTable!.Fields!["customer_rowid"];
            // Only the name is shown explicitly, so `ref_customer_id` is not covered and must be emitted as its own field.
            customerField.DisplayFields = "ref_customer_name";

            var layout = FormLayoutGenerator.Generate(schema, "default");
            var fields = layout.Sections![0].Fields!;

            Assert.Contains(fields, f => f.FieldName == "ref_customer_id");
            Assert.DoesNotContain(fields, f => f.FieldName == "ref_customer_name");
        }

        [Fact]
        [DisplayName("FormLayoutGenerator.Generate applies the same coverage rule to detail tables")]
        public void Generate_DetailGrid_CoversDisplayFields()
        {
            var schema = BuildSchema();
            var detail = schema.Tables!.Add("OrderLine", "訂單明細");
            detail.Fields!.Add(new FormField("sys_rowid", "唯一識別", FieldDbType.Guid));
            detail.Fields!.Add(new FormField("sys_master_rowid", "主檔識別", FieldDbType.Guid));
            var productField = new FormField("product_rowid", "商品", FieldDbType.Guid)
            {
                RelationProgId = "Product",
                Visible = false,
            };
            productField.RelationFieldMappings!.Add("sys_id", "ref_product_id");
            productField.RelationFieldMappings!.Add("sys_name", "ref_product_name");
            detail.Fields!.Add(productField);
            detail.Fields!.Add(new FormField("ref_product_id", "商品代碼", FieldDbType.String, FieldType.RelationField));
            detail.Fields!.Add(new FormField("ref_product_name", "商品名稱", FieldDbType.String, FieldType.RelationField));

            var layout = FormLayoutGenerator.Generate(schema, "default");
            var columns = layout.Details![0].Columns!;

            var lookup = columns.First(c => c.FieldName == "product_rowid");
            Assert.Equal(ControlType.ButtonEdit, lookup.ControlType);
            Assert.DoesNotContain(columns, c => c.FieldName == "ref_product_id");
            Assert.DoesNotContain(columns, c => c.FieldName == "ref_product_name");
        }
    }
}
