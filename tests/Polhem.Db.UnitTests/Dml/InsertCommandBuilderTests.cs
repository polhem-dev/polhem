using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Tests.Shared;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Database;

namespace Polhem.Db.UnitTests.Dml
{
    public class InsertCommandBuilderTests
    {
        private static FormSchema BuildEmployeeSchema()
        {
            var schema = new FormSchema("Employee", "Employee Form");
            var table = schema.Tables!.Add("Employee", "Employee");
            table.DbTableName = "st_employee";

            // Auto-increment system field — should be skipped.
            table.Fields!.Add(SysFields.No, "Sequence", FieldDbType.AutoIncrement);
            // Primary key, written by client.
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            table.Fields!.Add(new FormField("sys_id", "Employee Id", FieldDbType.String) { MaxLength = 50 });
            table.Fields!.Add(new FormField("sys_name", "Employee Name", FieldDbType.String) { MaxLength = 100 });

            // Relation field — should be skipped.
            table.Fields!.Add(new FormField("ref_dept_name", "Department Name", FieldDbType.String) { MaxLength = 100 });
            table.Fields!["ref_dept_name"].Type = FieldType.RelationField;

            return schema;
        }

        private static DataTable BuildEmployeeDataTable()
        {
            var dt = new DataTable("Employee");
            dt.Columns.Add(SysFields.RowId, typeof(Guid));
            dt.Columns.Add("sys_id", typeof(string));
            dt.Columns.Add("sys_name", typeof(string));
            dt.Columns.Add("ref_dept_name", typeof(string));
            return dt;
        }

        [Fact]
        [DisplayName("Build throws ArgumentException for a blank tableName")]
        public void Build_EmptyTableName_Throws()
        {
            var schema = BuildEmployeeSchema();
            var builder = new InsertCommandBuilder(schema, DatabaseType.SQLServer);
            var dt = BuildEmployeeDataTable();
            var row = dt.NewRow();

            Assert.Throws<ArgumentException>(() => builder.Build(string.Empty, row));
        }

        [Fact]
        [DisplayName("Build throws ArgumentNullException for a null row")]
        public void Build_NullRow_Throws()
        {
            var schema = BuildEmployeeSchema();
            var builder = new InsertCommandBuilder(schema, DatabaseType.SQLServer);

            Assert.Throws<ArgumentNullException>(() => builder.Build("Employee", null!));
        }

        [Fact]
        [DisplayName("Build throws InvalidOperationException for a tableName that does not exist")]
        public void Build_UnknownTableName_Throws()
        {
            var schema = BuildEmployeeSchema();
            var builder = new InsertCommandBuilder(schema, DatabaseType.SQLServer);
            var dt = BuildEmployeeDataTable();

            Assert.Throws<InvalidOperationException>(() => builder.Build("NoSuchTable", dt.NewRow()));
        }

        [Fact]
        [DisplayName("Build produces the SQL Server dialect and excludes RelationField and AutoIncrement fields")]
        public void Build_SqlServer_GeneratesExpectedSqlAndParams()
        {
            var schema = BuildEmployeeSchema();
            var builder = new InsertCommandBuilder(schema, DatabaseType.SQLServer);
            var dt = BuildEmployeeDataTable();
            var rowId = Guid.NewGuid();
            var row = dt.NewRow();
            row[SysFields.RowId] = rowId;
            row["sys_id"] = "E001";
            row["sys_name"] = "Alice";
            // ref_dept_name supplied but is a RelationField; must be excluded.
            row["ref_dept_name"] = "RD";

            var spec = builder.Build("Employee", row);

            Assert.Equal(DbCommandKind.NonQuery, spec.Kind);
            Assert.Equal(
                "INSERT INTO [st_employee] ([sys_rowid], [sys_id], [sys_name]) VALUES ({0}, {1}, {2})",
                spec.CommandText);
            Assert.Equal(3, spec.Parameters.Count);
            Assert.Equal(rowId, spec.Parameters[0].Value);
            Assert.Equal("E001", spec.Parameters[1].Value);
            Assert.Equal("Alice", spec.Parameters[2].Value);
        }

        [Fact]
        [DisplayName("Build produces the PostgreSQL dialect and excludes RelationField and AutoIncrement fields")]
        public void Build_PostgreSql_GeneratesExpectedSql()
        {
            var schema = BuildEmployeeSchema();
            var builder = new InsertCommandBuilder(schema, DatabaseType.PostgreSQL);
            var dt = BuildEmployeeDataTable();
            var row = dt.NewRow();
            row[SysFields.RowId] = Guid.NewGuid();
            row["sys_id"] = "E001";
            row["sys_name"] = "Alice";

            var spec = builder.Build("Employee", row);

            Assert.Equal(
                "INSERT INTO \"st_employee\" (\"sys_rowid\", \"sys_id\", \"sys_name\") VALUES ({0}, {1}, {2})",
                spec.CommandText);
        }

        [Fact]
        [DisplayName("Build skips DBNull fields so the database defaults apply")]
        public void Build_DbNullFields_Skipped()
        {
            var schema = BuildEmployeeSchema();
            var builder = new InsertCommandBuilder(schema, DatabaseType.SQLServer);
            var dt = BuildEmployeeDataTable();
            var row = dt.NewRow();
            row[SysFields.RowId] = Guid.NewGuid();
            row["sys_id"] = "E001";
            // sys_name left as DBNull on purpose.

            var spec = builder.Build("Employee", row);

            Assert.DoesNotContain("sys_name", spec.CommandText);
            Assert.Equal(2, spec.Parameters.Count);
        }

        [Fact]
        [DisplayName("Build throws InvalidOperationException when no field can be written")]
        public void Build_NoWritableFields_Throws()
        {
            var schema = BuildEmployeeSchema();
            var builder = new InsertCommandBuilder(schema, DatabaseType.SQLServer);
            var dt = BuildEmployeeDataTable();
            var row = dt.NewRow();
            // All columns left as DBNull.

            Assert.Throws<InvalidOperationException>(() => builder.Build("Employee", row));
        }

        [Fact]
        [DisplayName("Build falls back to TableName when DbTableName is not set")]
        public void Build_FallbackToTableNameWhenDbTableNameMissing()
        {
            var schema = new FormSchema("X", "X");
            var table = schema.Tables!.Add("Foo", "Foo");
            // DbTableName intentionally left empty.
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            table.Fields!.Add(new FormField("name", "Name", FieldDbType.String) { MaxLength = 50 });

            var builder = new InsertCommandBuilder(schema, DatabaseType.SQLServer);
            var dt = new DataTable();
            dt.Columns.Add(SysFields.RowId, typeof(Guid));
            dt.Columns.Add("name", typeof(string));
            var row = dt.NewRow();
            row[SysFields.RowId] = Guid.NewGuid();
            row["name"] = "n";

            var spec = builder.Build("Foo", row);

            Assert.Contains("[Foo]", spec.CommandText);
        }
    }
}
