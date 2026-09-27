using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Dml;
using Polhem.Definition;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Database;

namespace Polhem.Db.UnitTests.Dml
{
    public class DeleteCommandBuilderTests
    {
        private static FormSchema BuildEmployeeSchema()
        {
            var schema = new FormSchema("Employee", "Employee Form");
            var table = schema.Tables!.Add("Employee", "Employee");
            table.DbTableName = "st_employee";
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            table.Fields!.Add(SysFields.MasterRowId, "Master Row ID", FieldDbType.Guid);
            table.Fields!.Add(new FormField("sys_id", "Employee Id", FieldDbType.String) { MaxLength = 50 });
            table.Fields!.Add(new FormField("ref_dept_name", "Department Name", FieldDbType.String) { MaxLength = 100 });
            table.Fields!["ref_dept_name"].Type = FieldType.RelationField;
            return schema;
        }

        [Fact]
        [DisplayName("Build throws ArgumentException for a blank tableName")]
        public void Build_EmptyTableName_Throws()
        {
            var builder = new DeleteCommandBuilder(BuildEmployeeSchema(), DatabaseType.SQLServer);
            Assert.Throws<ArgumentException>(() =>
                builder.Build(string.Empty, FilterCondition.Equal(SysFields.RowId, Guid.NewGuid())));
        }

        [Fact]
        [DisplayName("Build throws ArgumentNullException for a null filter")]
        public void Build_NullFilter_Throws()
        {
            var builder = new DeleteCommandBuilder(BuildEmployeeSchema(), DatabaseType.SQLServer);
            Assert.Throws<ArgumentNullException>(() => builder.Build("Employee", null!));
        }

        [Fact]
        [DisplayName("Build throws InvalidOperationException for a tableName that does not exist")]
        public void Build_UnknownTableName_Throws()
        {
            var builder = new DeleteCommandBuilder(BuildEmployeeSchema(), DatabaseType.SQLServer);
            Assert.Throws<InvalidOperationException>(() =>
                builder.Build("NoSuchTable", FilterCondition.Equal(SysFields.RowId, Guid.NewGuid())));
        }

        [Fact]
        [DisplayName("Build produces the SQL Server dialect and quotes identifiers and fields")]
        public void Build_SqlServer_GeneratesExpectedSqlAndParam()
        {
            var rowId = Guid.NewGuid();
            var builder = new DeleteCommandBuilder(BuildEmployeeSchema(), DatabaseType.SQLServer);
            var spec = builder.Build("Employee", FilterCondition.Equal(SysFields.RowId, rowId));

            Assert.Equal(DbCommandKind.NonQuery, spec.Kind);
            Assert.Equal("DELETE FROM [st_employee] WHERE [sys_rowid] = @p0", spec.CommandText);
            Assert.Single(spec.Parameters);
            Assert.Equal(rowId, spec.Parameters[0].Value);
        }

        [Fact]
        [DisplayName("Build produces the PostgreSQL dialect and quotes identifiers and fields")]
        public void Build_PostgreSql_GeneratesExpectedSql()
        {
            var builder = new DeleteCommandBuilder(BuildEmployeeSchema(), DatabaseType.PostgreSQL);
            var spec = builder.Build("Employee", FilterCondition.Equal(SysFields.RowId, Guid.NewGuid()));

            Assert.Equal("DELETE FROM \"st_employee\" WHERE \"sys_rowid\" = @p0", spec.CommandText);
        }

        [Fact]
        [DisplayName("Build can delete details with sys_master_rowid as the condition")]
        public void Build_MasterRowId_DeletesDetailRows()
        {
            var masterId = Guid.NewGuid();
            var builder = new DeleteCommandBuilder(BuildEmployeeSchema(), DatabaseType.SQLServer);
            var spec = builder.Build("Employee", FilterCondition.Equal(SysFields.MasterRowId, masterId));

            Assert.Contains("[sys_master_rowid] = @p0", spec.CommandText);
            Assert.Equal(masterId, spec.Parameters[0].Value);
        }

        [Fact]
        [DisplayName("Build throws NotSupportedException when referencing a RelationField")]
        public void Build_RelationFieldInFilter_Throws()
        {
            var builder = new DeleteCommandBuilder(BuildEmployeeSchema(), DatabaseType.SQLServer);
            Assert.Throws<NotSupportedException>(() =>
                builder.Build("Employee", FilterCondition.Equal("ref_dept_name", "Sales")));
        }

        [Fact]
        [DisplayName("Build throws NotSupportedException when referencing an unknown field")]
        public void Build_UnknownFieldInFilter_Throws()
        {
            var builder = new DeleteCommandBuilder(BuildEmployeeSchema(), DatabaseType.SQLServer);
            Assert.Throws<NotSupportedException>(() =>
                builder.Build("Employee", FilterCondition.Equal("not_in_schema", 1)));
        }

        [Fact]
        [DisplayName("Build expands a FilterGroup into the WHERE clause")]
        public void Build_FilterGroup_ProducesCompositeWhere()
        {
            var builder = new DeleteCommandBuilder(BuildEmployeeSchema(), DatabaseType.SQLServer);
            var filter = FilterGroup.All(
                FilterCondition.Equal(SysFields.RowId, Guid.NewGuid()),
                FilterCondition.Equal("sys_id", "E001"));

            var spec = builder.Build("Employee", filter);

            Assert.Contains("[sys_rowid] = @p0", spec.CommandText);
            Assert.Contains("[sys_id] = @p1", spec.CommandText);
            Assert.Equal(2, spec.Parameters.Count);
        }
    }
}
