using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Db.Dml;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    public class TableSchemaCommandBuilderTests : IClassFixture<SharedDbFixture>
    {
        public TableSchemaCommandBuilderTests(SharedDbFixture _) { }

        private static TableSchema BuildSampleSchema()
        {
            var schema = new TableSchema { TableName = "st_demo" };
            schema.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            schema.Fields!.Add("name", "Name", FieldDbType.String, 50);
            schema.Fields!.Add("age", "Age", FieldDbType.Integer);
            schema.Indexes!.AddPrimaryKey(SysFields.RowId);
            return schema;
        }

        #region Constructors

        [Fact]
        [DisplayName("The constructor throws ArgumentNullException for a null TableSchema")]
        public void Constructor_NullTableSchema_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TableSchemaCommandBuilder(DatabaseType.SQLServer, null!));
        }

        [Fact]
        [DisplayName("The constructor uses the explicitly given DatabaseType")]
        public void Constructor_UsesSpecifiedDatabaseType()
        {
            var schema = BuildSampleSchema();
            var builder = new TableSchemaCommandBuilder(DatabaseType.MySQL, schema);
            Assert.Equal(DatabaseType.MySQL, builder.DatabaseType);
            Assert.Same(schema, builder.TableSchema);
        }

        #endregion

        #region BuildInsertCommand

        [Fact]
        [DisplayName("BuildInsertCommand produces an INSERT statement with every non-auto-increment field")]
        public void BuildInsertCommand_ContainsAllNonAutoIncrementFields()
        {
            var schema = BuildSampleSchema();
            var builder = new TableSchemaCommandBuilder(DatabaseType.SQLServer, schema);

            var cmd = builder.BuildInsertCommand();

            Assert.Contains("Insert Into [st_demo]", cmd.CommandText);
            Assert.Contains("[sys_rowid]", cmd.CommandText);
            Assert.Contains("[name]", cmd.CommandText);
            Assert.Contains("[age]", cmd.CommandText);
            Assert.Contains("@sys_rowid", cmd.CommandText);
            Assert.Contains("@name", cmd.CommandText);
            Assert.Contains("@age", cmd.CommandText);
            Assert.Equal(3, cmd.Parameters.Count);
        }

        [Fact]
        [DisplayName("BuildInsertCommand skips AutoIncrement fields")]
        public void BuildInsertCommand_SkipsAutoIncrementField()
        {
            var schema = new TableSchema { TableName = "st_demo" };
            schema.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            schema.Fields!.Add("seq", "Seq", FieldDbType.AutoIncrement);
            schema.Fields!.Add("name", "Name", FieldDbType.String, 30);
            schema.Indexes!.AddPrimaryKey(SysFields.RowId);

            var builder = new TableSchemaCommandBuilder(DatabaseType.SQLServer, schema);
            var cmd = builder.BuildInsertCommand();

            Assert.DoesNotContain("[seq]", cmd.CommandText);
            Assert.DoesNotContain("@seq", cmd.CommandText);
            Assert.False(cmd.Parameters.Contains("seq"));
        }

        #endregion

        #region BuildUpdateCommand

        [Fact]
        [DisplayName("BuildUpdateCommand produces an UPDATE statement with the primary key as the WHERE condition")]
        public void BuildUpdateCommand_HasSetClauseAndPrimaryKeyWhere()
        {
            var schema = BuildSampleSchema();
            var builder = new TableSchemaCommandBuilder(DatabaseType.SQLServer, schema);

            var cmd = builder.BuildUpdateCommand();

            Assert.Contains("Update [st_demo] Set", cmd.CommandText);
            Assert.Contains("[name]=@name", cmd.CommandText);
            Assert.Contains("[age]=@age", cmd.CommandText);
            Assert.Contains("Where [sys_rowid]=@sys_rowid", cmd.CommandText);
            // Non-PK fields plus one PK field.
            Assert.Equal(3, cmd.Parameters.Count);
        }

        [Fact]
        [DisplayName("BuildUpdateCommand uses the Original version for the primary key parameter")]
        public void BuildUpdateCommand_KeyParameterUsesOriginalVersion()
        {
            var schema = BuildSampleSchema();
            var builder = new TableSchemaCommandBuilder(DatabaseType.SQLServer, schema);

            var cmd = builder.BuildUpdateCommand();

            Assert.Equal(DataRowVersion.Original, cmd.Parameters[SysFields.RowId].SourceVersion);
            Assert.Equal(DataRowVersion.Current, cmd.Parameters["name"].SourceVersion);
        }

        #endregion

        #region BuildDeleteCommand

        [Fact]
        [DisplayName("BuildDeleteCommand produces a DELETE statement with the primary key as the WHERE condition")]
        public void BuildDeleteCommand_HasPrimaryKeyWhere()
        {
            var schema = BuildSampleSchema();
            var builder = new TableSchemaCommandBuilder(DatabaseType.SQLServer, schema);

            var cmd = builder.BuildDeleteCommand();

            Assert.Contains("Delete From [st_demo]", cmd.CommandText);
            Assert.Contains("Where [sys_rowid]=@sys_rowid", cmd.CommandText);
            Assert.Single(cmd.Parameters);
            Assert.Equal(DataRowVersion.Original, cmd.Parameters[SysFields.RowId].SourceVersion);
        }

        #endregion

        #region BuildUpdateSpec

        [Fact]
        [DisplayName("BuildUpdateSpec wraps the Insert/Update/Delete commands and the DataTable together")]
        public void BuildUpdateSpec_PackagesAllThreeCommands()
        {
            var schema = BuildSampleSchema();
            var builder = new TableSchemaCommandBuilder(DatabaseType.SQLServer, schema);
            var dataTable = new DataTable("st_demo");

            var spec = builder.BuildUpdateSpec(dataTable);

            Assert.Same(dataTable, spec.DataTable);
            Assert.NotNull(spec.InsertCommand);
            Assert.NotNull(spec.UpdateCommand);
            Assert.NotNull(spec.DeleteCommand);
            Assert.Contains("Insert Into", spec.InsertCommand!.CommandText);
            Assert.Contains("Update", spec.UpdateCommand!.CommandText);
            Assert.Contains("Delete From", spec.DeleteCommand!.CommandText);
        }

        [Fact]
        [DisplayName("A pinned field is inserted from its own fixed parameter, not from the row")]
        public void BuildInsertCommand_PinnedField_UsesFixedValue()
        {
            var builder = new TableSchemaCommandBuilder(DatabaseType.SQLServer, BuildCompanySchema())
                .PinField(SysFields.CompanyId, "c001");

            var command = builder.BuildInsertCommand();

            var parameter = command.Parameters.Single(p => p.Name == SysFields.CompanyId);
            Assert.Equal("c001", parameter.Value);
            Assert.Equal(string.Empty, parameter.SourceColumn);
            Assert.Contains("[sys_company_id]", command.CommandText, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("An UPDATE leaves a pinned field out of SET and only touches rows that hold its value")]
        public void BuildUpdateCommand_PinnedField_NotSetAndRequired()
        {
            var builder = new TableSchemaCommandBuilder(DatabaseType.SQLServer, BuildCompanySchema())
                .PinField(SysFields.CompanyId, "c001");

            var command = builder.BuildUpdateCommand();

            Assert.DoesNotContain("[sys_company_id]=@sys_company_id,", command.CommandText, StringComparison.Ordinal);
            Assert.Contains("And [sys_company_id]=@sys_company_id", command.CommandText, StringComparison.Ordinal);
            var parameter = Assert.Single(command.Parameters, p => p.Name == SysFields.CompanyId);
            Assert.Equal("c001", parameter.Value);
            Assert.Equal(string.Empty, parameter.SourceColumn);
        }

        [Fact]
        [DisplayName("A DELETE only touches rows that hold the pinned field's value")]
        public void BuildDeleteCommand_PinnedField_Required()
        {
            var builder = new TableSchemaCommandBuilder(DatabaseType.SQLServer, BuildCompanySchema())
                .PinField(SysFields.CompanyId, "c001");

            var command = builder.BuildDeleteCommand();

            Assert.Contains("And [sys_company_id]=@sys_company_id", command.CommandText, StringComparison.Ordinal);
            Assert.Equal("c001", command.Parameters.Single(p => p.Name == SysFields.CompanyId).Value);
        }

        [Fact]
        [DisplayName("PinField throws for a field the table schema does not have")]
        public void PinField_UnknownField_Throws()
        {
            var builder = new TableSchemaCommandBuilder(DatabaseType.SQLServer, BuildSampleSchema());

            Assert.Throws<InvalidOperationException>(() => builder.PinField(SysFields.CompanyId, "c001"));
        }

        private static TableSchema BuildCompanySchema()
        {
            var schema = BuildSampleSchema();
            schema.Fields!.Add(SysFields.CompanyId, "Company", FieldDbType.String, 10);
            return schema;
        }

        [Fact]
        [DisplayName("BuildUpdateSpec throws ArgumentNullException for a null DataTable")]
        public void BuildUpdateSpec_NullDataTable_Throws()
        {
            var schema = BuildSampleSchema();
            var builder = new TableSchemaCommandBuilder(DatabaseType.SQLServer, schema);

            Assert.Throws<ArgumentNullException>(() => builder.BuildUpdateSpec(null!));
        }

        #endregion
    }
}
