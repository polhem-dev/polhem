using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Dml;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Sorting;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests.Dml
{
    /// <summary>
    /// The SELECT path accepts filter and sort fields only when the form table declares them, and never lets a query
    /// name a <see cref="ProtectedFields"/> column.
    /// </summary>
    /// <remarks>
    /// Quoting already keeps a name from injecting SQL. What these tests pin down is that a caller cannot reach a
    /// column the form leaves out: filtering or sorting on it reads the column one comparison at a time. The forms
    /// here sit over <c>st_user</c>, whose <c>password</c> column is protected.
    /// </remarks>
    public class SelectFieldGuardTests : IClassFixture<PolhemTestFixture>
    {
        private const string UserForm = "GuardUser";
        private const string UserFormWithoutPassword = "GuardUserSlim";
        private const string ReferencingForm = "GuardRef";

        private readonly PolhemTestFixture _fx;

        public SelectFieldGuardTests(PolhemTestFixture fx) { _fx = fx; }

        // -------- Undeclared fields --------

        [Fact]
        [DisplayName("A filter on a column the form does not declare is refused")]
        public void Build_FilterOnUndeclaredColumn_Throws()
        {
            var builder = Builder(SlimUserSchema());

            Assert.Throws<InvalidOperationException>(
                () => builder.Build(UserFormWithoutPassword, string.Empty, FilterCondition.StartsWith(ProtectedFields.Password, "a")));
        }

        [Fact]
        [DisplayName("A sort on a column the form does not declare is refused")]
        public void Build_SortOnUndeclaredColumn_Throws()
        {
            var builder = Builder(SlimUserSchema());

            Assert.Throws<InvalidOperationException>(
                () => builder.Build(UserFormWithoutPassword, string.Empty, null, Sort(ProtectedFields.DeploymentAdmin)));
        }

        [Fact]
        [DisplayName("A COUNT filter on a column the form does not declare is refused")]
        public void BuildCount_FilterOnUndeclaredColumn_Throws()
        {
            var builder = Builder(SlimUserSchema());

            Assert.Throws<InvalidOperationException>(
                () => builder.BuildCount(UserFormWithoutPassword, FilterCondition.Equal(ProtectedFields.DeploymentAdmin, true)));
        }

        [Fact]
        [DisplayName("A filter nested inside a group is checked too")]
        public void Build_NestedFilterOnUndeclaredColumn_Throws()
        {
            var builder = Builder(SlimUserSchema());
            var filter = FilterGroup.All(
                FilterCondition.Equal("sys_id", "u1"),
                FilterGroup.Any(FilterCondition.StartsWith(ProtectedFields.Password, "a")));

            Assert.Throws<InvalidOperationException>(() => builder.Build(UserFormWithoutPassword, string.Empty, filter));
        }

        [Fact]
        [DisplayName("Control case: filter and sort on declared fields build normally")]
        public void Build_FilterAndSortOnDeclaredFields_Builds()
        {
            var builder = Builder(SlimUserSchema());

            var spec = builder.Build(UserFormWithoutPassword, string.Empty, FilterCondition.Equal("sys_id", "u1"), Sort("sys_id"));

            Assert.Contains("ORDER BY", spec.CommandText, StringComparison.Ordinal);
        }

        // -------- Protected columns a form declares --------

        [Fact]
        [DisplayName("A filter on a declared protected column is refused")]
        public void Build_FilterOnDeclaredProtectedColumn_Throws()
        {
            var builder = Builder(UserSchema());

            Assert.Throws<InvalidOperationException>(
                () => builder.Build(UserForm, string.Empty, FilterCondition.StartsWith(ProtectedFields.Password, "a")));
        }

        [Fact]
        [DisplayName("A sort on a declared protected column is refused")]
        public void Build_SortOnDeclaredProtectedColumn_Throws()
        {
            var builder = Builder(UserSchema());

            Assert.Throws<InvalidOperationException>(
                () => builder.Build(UserForm, string.Empty, null, Sort(ProtectedFields.Password)));
        }

        [Fact]
        [DisplayName("Naming a declared protected column in the select list is refused")]
        public void Build_SelectNamingProtectedColumn_Throws()
        {
            var builder = Builder(UserSchema());

            Assert.Throws<InvalidOperationException>(() => builder.Build(UserForm, "sys_id," + ProtectedFields.Password));
        }

        [Fact]
        [DisplayName("Selecting every field leaves a declared protected column out")]
        public void Build_SelectAll_OmitsProtectedColumn()
        {
            var builder = Builder(UserSchema());

            var spec = builder.Build(UserForm, string.Empty);

            Assert.Contains("[sys_id]", spec.CommandText, StringComparison.Ordinal);
            Assert.DoesNotContain("[password]", spec.CommandText, StringComparison.Ordinal);
        }

        // -------- Relation fields sourced from a protected column --------

        [Fact]
        [DisplayName("Selecting every field leaves out a relation field whose source is a protected column")]
        public void Build_SelectAll_OmitsRelationFieldOverProtectedColumn()
        {
            var builder = Builder(ReferencingSchema(), UserSchema());

            var spec = builder.Build(ReferencingForm, string.Empty);

            Assert.Contains("[ref_user_id]", spec.CommandText, StringComparison.Ordinal);
            Assert.DoesNotContain("[ref_user_password]", spec.CommandText, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A filter on a relation field whose source is a protected column is refused")]
        public void Build_FilterOnRelationFieldOverProtectedColumn_Throws()
        {
            var builder = Builder(ReferencingSchema(), UserSchema());

            Assert.Throws<InvalidOperationException>(
                () => builder.Build(ReferencingForm, string.Empty, FilterCondition.StartsWith("ref_user_password", "a")));
        }

        [Fact]
        [DisplayName("Control case: a filter on a relation field over an ordinary column builds and reads the related table")]
        public void Build_FilterOnOrdinaryRelationField_Builds()
        {
            var builder = Builder(ReferencingSchema(), UserSchema());

            var spec = builder.Build(ReferencingForm, "sys_id", FilterCondition.Equal("ref_user_id", "u1"));

            Assert.Contains("B.[sys_id]", spec.CommandText, StringComparison.Ordinal);
        }

        // -------- Fixtures --------

        private SelectCommandBuilder Builder(FormSchema schema, FormSchema? related = null)
        {
            IDefineAccess defineAccess = _fx.GetRequiredService<IDefineAccess>();
            if (related != null) { defineAccess = new FormSchemaOverlayDefineAccess(defineAccess, related); }
            return new SelectCommandBuilder(schema, DatabaseType.SQLServer, defineAccess);
        }

        private static SortFieldCollection Sort(string fieldName) => [new SortField(fieldName, SortDirection.Asc)];

        private static FormSchema UserSchema()
        {
            var schema = new FormSchema(UserForm, "Users");
            var table = schema.Tables!.Add(UserForm, "Users");
            table.DbTableName = "st_user";
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            table.Fields!.Add(new FormField("sys_id", "User ID", FieldDbType.String) { MaxLength = 50 });
            table.Fields.Add(new FormField(ProtectedFields.Password, "Password", FieldDbType.String) { MaxLength = 200 });
            return schema;
        }

        private static FormSchema SlimUserSchema()
        {
            var schema = new FormSchema(UserFormWithoutPassword, "Users");
            var table = schema.Tables!.Add(UserFormWithoutPassword, "Users");
            table.DbTableName = "st_user";
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            table.Fields!.Add(new FormField("sys_id", "User ID", FieldDbType.String) { MaxLength = 50 });
            return schema;
        }

        private static FormSchema ReferencingSchema()
        {
            var schema = new FormSchema(ReferencingForm, "Referencing");
            var table = schema.Tables!.Add(ReferencingForm, "Referencing");
            table.DbTableName = "ft_guard_ref";
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            table.Fields!.Add(new FormField("sys_id", "ID", FieldDbType.String) { MaxLength = 50 });

            var foreignKey = table.Fields.Add("user_rowid", "User", FieldDbType.Guid);
            foreignKey.RelationProgId = UserForm;
            foreignKey.RelationFieldMappings!.Add("sys_id", "ref_user_id");
            foreignKey.RelationFieldMappings!.Add(ProtectedFields.Password, "ref_user_password");

            table.Fields.Add(new FormField("ref_user_id", "User ID", FieldDbType.String) { MaxLength = 50, Type = FieldType.RelationField });
            table.Fields.Add(new FormField("ref_user_password", "User password", FieldDbType.String) { MaxLength = 200, Type = FieldType.RelationField });
            return schema;
        }
    }
}
