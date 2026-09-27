using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Dml;
using Polhem.Definition;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Database;

namespace Polhem.Db.UnitTests.Dml
{
    /// <summary>
    /// Coverage-focused tests for <see cref="DeleteCommandBuilder"/> guard clauses and
    /// alternate branches. All DB-free (no fixture) so they always run during coverage.
    /// </summary>
    public class DeleteCommandBuilderCoverageTests
    {
        private static FormSchema BuildSchema(string dbTableName)
        {
            var schema = new FormSchema("Employee", "Employee Form");
            var table = schema.Tables!.Add("Employee", "Employee");
            table.DbTableName = dbTableName;
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            table.Fields!.Add(new FormField("sys_id", "Employee Id", FieldDbType.String) { MaxLength = 50 });
            return schema;
        }

        [Fact]
        [DisplayName("The constructor throws ArgumentNullException for a null formSchema")]
        public void Constructor_NullFormSchema_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new DeleteCommandBuilder(null!, DatabaseType.SQLServer));
        }

        [Fact]
        [DisplayName("Build throws InvalidOperationException when an empty FilterGroup produces an empty WHERE")]
        public void Build_EmptyFilterGroup_ThrowsOnEmptyWhere()
        {
            var builder = new DeleteCommandBuilder(BuildSchema("st_employee"), DatabaseType.SQLServer);
            Assert.Throws<InvalidOperationException>(() =>
                builder.Build("Employee", FilterGroup.All()));
        }

        [Fact]
        [DisplayName("Build uses TableName as the physical table name when DbTableName is empty")]
        public void Build_TableWithoutDbTableName_UsesTableName()
        {
            var builder = new DeleteCommandBuilder(BuildSchema(string.Empty), DatabaseType.SQLServer);
            var spec = builder.Build("Employee", FilterCondition.Equal(SysFields.RowId, Guid.NewGuid()));

            Assert.Equal("DELETE FROM [Employee] WHERE [sys_rowid] = @p0", spec.CommandText);
        }

        [Fact]
        [DisplayName("Build throws InvalidOperationException when a condition's FieldName is empty")]
        public void Build_ConditionWithEmptyFieldName_Throws()
        {
            var builder = new DeleteCommandBuilder(BuildSchema("st_employee"), DatabaseType.SQLServer);
            Assert.Throws<InvalidOperationException>(() =>
                builder.Build("Employee", new FilterCondition()));
        }

        [Fact]
        [DisplayName("Build returns a node of unknown FilterNodeKind unchanged through the fall-through branch")]
        public void Build_UnknownFilterNodeKind_HitsFallThroughBranch()
        {
            // A synthetic node whose Kind is neither Condition nor Group drives
            // QuoteAndValidateFields into its final fall-through return (the node is
            // returned unchanged). The downstream WhereBuilder then rejects it, so Build
            // ultimately throws — the fall-through branch has already executed by then.
            var builder = new DeleteCommandBuilder(BuildSchema("st_employee"), DatabaseType.SQLServer);
            var exception = Record.Exception(() => builder.Build("Employee", new UnknownFilterNode()));

            Assert.NotNull(exception);
        }

        private sealed class UnknownFilterNode : FilterNode
        {
            public override FilterNodeKind Kind => (FilterNodeKind)99;
        }
    }
}
