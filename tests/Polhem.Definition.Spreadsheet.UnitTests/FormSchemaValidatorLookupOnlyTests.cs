using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;

namespace Polhem.Definition.Spreadsheet.UnitTests
{
    /// <summary>
    /// Lookup-only mapping checks of <see cref="FormSchemaValidator"/>: a relation field must also be written by a
    /// mapping the query join reads, or it is empty again after a reload.
    /// </summary>
    public class FormSchemaValidatorLookupOnlyTests
    {
        private static FormSchema LeaveSchema(bool deptJoinsItsName)
        {
            var schema = new FormSchema("Leave", "請假單");
            var table = schema.Tables!.Add("Leave", "請假單");
            table.Fields!.Add(new FormField("ref_dept_name", "部門名稱", FieldDbType.String, FieldType.RelationField));

            var emp = new FormField("emp_rowid", "員工", FieldDbType.Guid) { RelationProgId = "Employee" };
            emp.RelationFieldMappings!.Add(new FieldMapping("dept_rowid", "dept_rowid") { LookupOnly = true });
            emp.RelationFieldMappings!.Add(new FieldMapping("ref_dept_name", "ref_dept_name") { LookupOnly = true });
            table.Fields!.Add(emp);

            var dept = new FormField("dept_rowid", "部門", FieldDbType.Guid) { RelationProgId = "Department" };
            if (deptJoinsItsName)
                dept.RelationFieldMappings!.Add("sys_name", "ref_dept_name");
            table.Fields!.Add(dept);
            return schema;
        }

        private static List<DefinitionIssue> Issues(FormSchema schema)
            => FormSchemaValidator.Validate(schema, ["Employee", "Department"]).ToList();

        [Fact]
        [DisplayName("A relation field written only by lookup-only mappings reports a Warning")]
        public void Validate_RelationFieldOnlyLookupOnly_ReportsWarning()
        {
            var issue = Assert.Single(Issues(LeaveSchema(deptJoinsItsName: false)));

            Assert.Equal(IssueSeverity.Warning, issue.Severity);
            Assert.Equal("Leave.ref_dept_name", issue.Location);
        }

        [Fact]
        [DisplayName("A relation field that a join mapping also writes reports nothing, nor does a lookup-only mapping into a stored field")]
        public void Validate_RelationFieldAlsoJoined_ReportsNothing()
        {
            Assert.Empty(Issues(LeaveSchema(deptJoinsItsName: true)));
        }
    }
}
