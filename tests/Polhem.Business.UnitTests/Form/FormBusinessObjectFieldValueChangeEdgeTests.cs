using System.ComponentModel;
using System.Data;
using Polhem.Business.Form;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// <see cref="FormBusinessObject.FieldValueChange"/> and <see cref="FieldValueChangeResult"/> on the shapes that need
    /// no database: values that cannot name a row, destinations the form does not declare or that hold dates, and
    /// marked fields that have nothing to resolve.
    /// </summary>
    public class FormBusinessObjectFieldValueChangeEdgeTests : IClassFixture<PolhemTestFixture>
    {
        private const string ProgId = "FvcEdge";

        private readonly PolhemTestFixture _fx;

        public FormBusinessObjectFieldValueChangeEdgeTests(PolhemTestFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("A value that is not a rowid reports an error on the field and clears every destination")]
        public void FieldValueChange_NonGuidValue_ReportsErrorAndClears()
        {
            var result = Bo().FieldValueChange(Args("emp_rowid", "not-a-guid"));

            Assert.True(result.Errors.ContainsKey("emp_rowid"));
            Assert.False(string.IsNullOrEmpty(result.Errors["emp_rowid"]));
            var row = Assert.Single(result.Values!.Rows.Cast<DataRow>());
            Assert.Equal(string.Empty, row["ref_emp_id"]);
        }

        [Fact]
        [DisplayName("A cleared date destination becomes DBNull carried as a DateTime column")]
        public void FieldValueChange_EmptyValue_ClearsDateDestinationToDbNull()
        {
            var result = Bo().FieldValueChange(Args("emp_rowid", string.Empty));

            Assert.Equal(DBNull.Value, result.Values!.Rows[0]["hire_date"]);
            Assert.Equal(typeof(DateTime), result.Values.Columns["hire_date"]!.DataType);
        }

        [Fact]
        [DisplayName("A cleared destination the form does not declare becomes DBNull carried as text")]
        public void FieldValueChange_EmptyValue_ClearsUndeclaredDestinationAsText()
        {
            var result = Bo().FieldValueChange(Args("emp_rowid", string.Empty));

            Assert.Equal(DBNull.Value, result.Values!.Rows[0]["no_such_destination"]);
            Assert.Equal(typeof(string), result.Values.Columns["no_such_destination"]!.DataType);
        }

        [Fact]
        [DisplayName("A cleared numeric destination gets zero in its own type")]
        public void FieldValueChange_EmptyValue_ClearsNumericDestinationToZero()
        {
            var result = Bo().FieldValueChange(Args("emp_rowid", string.Empty));

            Assert.Equal(0, result.Values!.Rows[0]["emp_age"]);
        }

        [Fact]
        [DisplayName("A marked field without a related form gets an empty result")]
        public void FieldValueChange_MarkedWithoutRelation_ReturnsEmpty()
        {
            var result = Bo().FieldValueChange(Args("no_relation_rowid", Guid.NewGuid().ToString()));

            Assert.Null(result.Values);
            Assert.Empty(result.Errors);
        }

        [Fact]
        [DisplayName("A marked relation field without mappings gets an empty result")]
        public void FieldValueChange_MarkedWithoutMappings_ReturnsEmpty()
        {
            var result = Bo().FieldValueChange(Args("no_mapping_rowid", Guid.NewGuid().ToString()));

            Assert.Null(result.Values);
            Assert.Empty(result.Errors);
        }

        [Fact]
        [DisplayName("ResolveRelationRow on a form without a master table resolves nothing")]
        public void ResolveRelationRow_NoMasterTable_ReturnsNull()
        {
            var schema = new FormSchema(ProgId, "No master") { CategoryId = "company" };
            var bo = Bo(schema);

            Assert.Null(bo.ResolveRelationRow(Guid.NewGuid(), ["sys_id"]));
        }

        [Fact]
        [DisplayName("ResolveRelationRow refuses a null field list")]
        public void ResolveRelationRow_NullFieldNames_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Bo().ResolveRelationRow(Guid.NewGuid(), null!));
        }

        // -------- FieldValueChangeResult --------

        [Fact]
        [DisplayName("SetValue of null without a type adds a text column holding DBNull")]
        public void SetValue_NullWithoutType_AddsTextColumn()
        {
            var result = new FieldValueChangeResult();

            result.SetValue("memo", null);

            Assert.Equal(typeof(string), result.Values!.Columns["memo"]!.DataType);
            Assert.Equal(DBNull.Value, result.Values.Rows[0]["memo"]);
        }

        [Fact]
        [DisplayName("SetValue without a type uses the value's own type, and a second call replaces the value")]
        public void SetValue_TwiceForSameField_ReplacesValue()
        {
            var result = new FieldValueChangeResult();

            result.SetValue("qty", 3);
            result.SetValue("qty", 4);

            Assert.Equal(typeof(int), result.Values!.Columns["qty"]!.DataType);
            Assert.Equal(4, Assert.Single(result.Values.Rows.Cast<DataRow>())["qty"]);
        }

        [Fact]
        [DisplayName("SetValue of DBNull without a type adds a text column")]
        public void SetValue_DbNullWithoutType_AddsTextColumn()
        {
            var result = new FieldValueChangeResult();

            result.SetValue("memo", DBNull.Value);

            Assert.Equal(typeof(string), result.Values!.Columns["memo"]!.DataType);
        }

        [Fact]
        [DisplayName("The response interface exposes the same errors and warnings")]
        public void ResponseInterface_ExposesErrorsAndWarnings()
        {
            var result = new FieldValueChangeResult();
            result.Errors["a"] = "error";
            result.Warnings["b"] = "warning";

            Polhem.Api.Contracts.Form.IFieldValueChangeResponse response = result;

            Assert.Equal("error", response.Errors["a"]);
            Assert.Equal("warning", response.Warnings["b"]);
        }

        // -------- Fixtures --------

        private static FieldValueChangeArgs Args(string field, string value)
            => new() { TableName = ProgId, FieldName = field, Value = value };

        private FormBusinessObject Bo(FormSchema? schema = null)
        {
            var ctx = new BusinessObjectContext
            {
                DefineAccess = new FormSchemaOverlayDefineAccess(_fx.GetRequiredService<IDefineAccess>(), schema ?? BuildSchema()),
                SessionInfoService = _fx.GetRequiredService<ISessionInfoService>(),
                LanguageService = _fx.GetRequiredService<ILanguageService>(),
                BoFactory = _fx.GetRequiredService<IBusinessObjectFactory>(),
                Services = _fx.Provider,
            };
            return new FormBusinessObject(ctx, TestSessionFactory.CreateAccessToken(_fx), ProgId);
        }

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema(ProgId, "Value change edges") { CategoryId = "company" };
            var table = schema.Tables!.Add(ProgId, "Value change edges");
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);

            var employee = table.Fields.Add("emp_rowid", "Employee", FieldDbType.Guid);
            employee.RelationProgId = "Employee";
            employee.ValueChangeCallback = true;
            employee.RelationFieldMappings!.Add(new FieldMapping("sys_id", "ref_emp_id"));
            employee.RelationFieldMappings.Add(new FieldMapping("hire_date", "hire_date"));
            employee.RelationFieldMappings.Add(new FieldMapping("age", "emp_age"));
            employee.RelationFieldMappings.Add(new FieldMapping("sys_name", "no_such_destination"));

            table.Fields.Add("no_relation_rowid", "No relation", FieldDbType.Guid).ValueChangeCallback = true;
            var noMapping = table.Fields.Add("no_mapping_rowid", "No mapping", FieldDbType.Guid);
            noMapping.RelationProgId = "Employee";
            noMapping.ValueChangeCallback = true;
            noMapping.RelationFieldMappings!.Clear();

            table.Fields!.Add(new FormField("ref_emp_id", "ref_emp_id", FieldDbType.String, FieldType.RelationField));
            table.Fields.Add("hire_date", "Hire date", FieldDbType.Date);
            table.Fields.Add("emp_age", "Age", FieldDbType.Integer);
            return schema;
        }
    }
}
