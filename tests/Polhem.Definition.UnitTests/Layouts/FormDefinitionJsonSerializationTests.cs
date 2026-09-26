using System.ComponentModel;
using System.Text.Json;
using Polhem.Base.Data;
using Polhem.Base.Serialization;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.UnitTests.Layouts
{
    /// <summary>
    /// Regression: FormSchema and FormLayout must serialise to JSON cleanly through
    /// JsonCodec (the same path the JSON-RPC API uses on the response side), so the
    /// JS-facing GetFormSchema / GetFormLayout methods can return plain JSON trees
    /// for camelCase, enum-as-string, and nested array structure.
    ///
    /// Also asserts <c>FormSchema.MasterTable</c> is excluded from JSON (the value is
    /// always equal to <c>Tables[0]</c> on a master-detail schema; duplicating it
    /// inflates the JS payload ~30%).
    /// </summary>
    public class FormDefinitionJsonSerializationTests
    {
        private static FormSchema BuildSampleSchema()
        {
            var schema = new FormSchema("Employee", "員工");
            var master = schema.Tables!.Add("Employee", "員工");
            master.Fields!.Add("sys_id", "員工編號", FieldDbType.String);
            master.Fields!.Add("sys_name", "員工姓名", FieldDbType.String);
            master.Fields!.Add("hire_date", "到職日", FieldDbType.DateTime);
            master.Fields!.Add("is_active", "在職中", FieldDbType.Boolean);

            var detail = schema.Tables!.Add("EmployeePhone", "員工電話");
            detail.Fields!.Add("phone_type", "電話類型", FieldDbType.String);
            detail.Fields!.Add("phone_no", "電話號碼", FieldDbType.String);
            return schema;
        }

        [Fact]
        [DisplayName("FormSchema JSON contains the key properties and nested structure")]
        public void FormSchema_JsonCodec_ContainsKeyStructure()
        {
            var schema = BuildSampleSchema();
            var json = JsonCodec.Serialize(schema);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("Employee", root.GetProperty("progId").GetString());
            Assert.Equal("員工", root.GetProperty("displayName").GetString());

            var tables = root.GetProperty("tables");
            Assert.Equal(2, tables.GetArrayLength());

            var masterTable = tables[0];
            Assert.Equal("Employee", masterTable.GetProperty("tableName").GetString());
            Assert.True(masterTable.GetProperty("fields").GetArrayLength() >= 4);

            // Enums are serialized as strings.
            var firstField = masterTable.GetProperty("fields")[0];
            Assert.Equal("String", firstField.GetProperty("dbType").GetString());
        }

        [Fact]
        [DisplayName("FormSchema JSON does not contain masterTable (tables[0] replaces it to avoid duplication)")]
        public void FormSchema_JsonCodec_DoesNotIncludeMasterTable()
        {
            var schema = BuildSampleSchema();
            var json = JsonCodec.Serialize(schema);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // With `[JsonIgnore]` in place this property must be gone, in any casing.
            Assert.False(root.TryGetProperty("masterTable", out _),
                "masterTable should be JsonIgnored to avoid duplicating tables[0] payload.");
            Assert.False(root.TryGetProperty("MasterTable", out _),
                "MasterTable (PascalCase) should also be absent.");
        }

        [Fact]
        [DisplayName("FormLayout JSON contains the key properties and nested structure")]
        public void FormLayout_JsonCodec_ContainsKeyStructure()
        {
            var layout = FormLayoutGenerator.Generate(BuildSampleSchema(), "default");
            var json = JsonCodec.Serialize(layout);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("default", root.GetProperty("layoutId").GetString());
            Assert.Equal("Employee", root.GetProperty("progId").GetString());
            Assert.Equal("員工", root.GetProperty("caption").GetString());
            Assert.Equal(2, root.GetProperty("columnCount").GetInt32());

            var sections = root.GetProperty("sections");
            Assert.True(sections.GetArrayLength() > 0, "sections should have at least 1 item");

            var section = sections[0];
            Assert.True(section.TryGetProperty("fields", out var fields));
            Assert.True(fields.GetArrayLength() > 0, "section.fields should have at least 1 item");

            var field = fields[0];
            Assert.True(field.TryGetProperty("fieldName", out _));
            Assert.True(field.TryGetProperty("caption", out _));
            Assert.True(field.TryGetProperty("controlType", out _));
            Assert.True(field.TryGetProperty("rowSpan", out _));
            Assert.True(field.TryGetProperty("columnSpan", out _));

            var details = root.GetProperty("details");
            Assert.Equal(1, details.GetArrayLength());
            var grid = details[0];
            Assert.Equal("EmployeePhone", grid.GetProperty("tableName").GetString());
            Assert.True(grid.TryGetProperty("columns", out var cols));
            Assert.True(cols.GetArrayLength() > 0, "grid.columns should have at least 1 item");
        }
    }
}
