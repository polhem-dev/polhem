using System.ComponentModel;
using Polhem.Db.Providers.SqlServer;
using Polhem.Definition.Filters;
using Polhem.Definition.Sorting;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Verifies how <see cref="SqlFormCommandBuilder"/> produces SELECT statements driven by the FormSchema, using
    /// <c>tests/Define/FormSchema/Employee.FormSchema.xml</c>. No database connection is needed; only the produced SQL
    /// strings are compared.
    /// </summary>
    public class EmployeeBuildSelectTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public EmployeeBuildSelectTests(PolhemTestFixture fx) { _fx = fx; }

        private IDefineAccess DefineAccess => _fx.GetRequiredService<IDefineAccess>();

        private SqlFormCommandBuilder NewBuilder()
            => new(DefineAccess.GetFormSchema("Employee"), DefineAccess);

        private static int CountJoins(string sql)
        {
            int count = 0;
            int index = 0;
            while ((index = sql.IndexOf("JOIN", index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                count++;
                index += "JOIN".Length;
            }
            return count;
        }

        [Fact]
        [DisplayName("Employee BuildSelect without fields produces a SELECT/FROM on st_employee")]
        public void BuildSelect_AllFields_ContainsTableNameAndKeywords()
        {
            var builder = NewBuilder();

            var spec = builder.BuildSelect("Employee", string.Empty, null, null);

            Assert.NotNull(spec);
            Assert.Equal(DbCommandKind.DataTable, spec.Kind);
            Assert.Contains("SELECT", spec.CommandText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("FROM", spec.CommandText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("st_employee", spec.CommandText);
        }

        [Fact]
        [DisplayName("Employee BuildSelect produces no JOIN when only master fields are selected")]
        public void BuildSelect_MasterFieldsOnly_NoJoin()
        {
            var builder = NewBuilder();

            var spec = builder.BuildSelect("Employee", "sys_id,sys_name", null, null);

            Assert.NotNull(spec);
            Assert.DoesNotContain("JOIN", spec.CommandText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("sys_id", spec.CommandText);
            Assert.Contains("sys_name", spec.CommandText);
        }

        [Fact]
        [DisplayName("Employee BuildSelect with the department reference field JOINs st_department")]
        public void BuildSelect_WithDeptRelationField_JoinsDepartment()
        {
            var builder = NewBuilder();

            // `ref_dept_name` comes from the mapping dept_rowid → Department.sys_name.
            var spec = builder.BuildSelect("Employee", "sys_id,sys_name,ref_dept_name", null, null);

            Assert.NotNull(spec);
            Assert.Contains("JOIN", spec.CommandText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("st_department", spec.CommandText);
            Assert.Equal(1, CountJoins(spec.CommandText));
        }

        [Fact]
        [DisplayName("Employee BuildSelect with the supervisor reference field produces nested JOINs (department, then back to employee)")]
        public void BuildSelect_WithSupervisorRelationField_GeneratesChainedJoins()
        {
            var builder = NewBuilder();

            // `ref_supervisor_name` comes through dept_rowid → Department → manager_rowid → Employee.
            var spec = builder.BuildSelect("Employee", "sys_id,sys_name,ref_supervisor_name", null, null);

            Assert.NotNull(spec);
            int joins = CountJoins(spec.CommandText);
            Assert.True(joins >= 2, $"Expected at least 2 JOINs (Department + Employee), actual {joins}");
            Assert.Contains("st_department", spec.CommandText);
            Assert.Contains("st_employee", spec.CommandText);
        }

        [Fact]
        [DisplayName("Employee BuildSelect produces no JOIN when filtering on a master field")]
        public void BuildSelect_FilterOnMasterField_NoJoinAndOneParameter()
        {
            var builder = NewBuilder();
            var filter = FilterCondition.Equal("sys_id", "E001");

            var spec = builder.BuildSelect("Employee", "sys_id,sys_name", filter, null);

            Assert.NotNull(spec);
            Assert.DoesNotContain("JOIN", spec.CommandText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("WHERE", spec.CommandText, StringComparison.OrdinalIgnoreCase);
            Assert.Single(spec.Parameters);
        }

        [Fact]
        [DisplayName("Employee BuildSelect produces a JOIN when filtering on a reference field")]
        public void BuildSelect_FilterOnRelationField_GeneratesJoin()
        {
            var builder = NewBuilder();
            var filter = FilterCondition.Equal("ref_dept_id", "D001");

            var spec = builder.BuildSelect("Employee", "sys_id,sys_name", filter, null);

            Assert.NotNull(spec);
            Assert.Contains("JOIN", spec.CommandText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("st_department", spec.CommandText);
            Assert.Single(spec.Parameters);
        }

        [Fact]
        [DisplayName("Employee BuildSelect produces a JOIN and ORDER BY when sorting on a reference field")]
        public void BuildSelect_SortByRelationField_GeneratesJoinAndOrderBy()
        {
            var builder = NewBuilder();
            var sortFields = new SortFieldCollection
            {
                new SortField("ref_dept_name", SortDirection.Asc)
            };

            var spec = builder.BuildSelect("Employee", "sys_id,sys_name", null, sortFields);

            Assert.NotNull(spec);
            Assert.Contains("JOIN", spec.CommandText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("st_department", spec.CommandText);
            Assert.Contains("ORDER BY", spec.CommandText, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [DisplayName("Employee BuildSelect with a multi-condition FilterGroup produces the matching number of parameters")]
        public void BuildSelect_FilterGroupWithMultipleConditions_ProducesParameters()
        {
            var builder = NewBuilder();

            var filterGroup = FilterGroup.All(
                FilterCondition.Contains("sys_name", "張"),
                FilterCondition.Equal("ref_dept_id", "D001")
            );

            var spec = builder.BuildSelect("Employee", "sys_id,sys_name", filterGroup, null);

            Assert.NotNull(spec);
            Assert.Equal(2, spec.Parameters.Count);
            Assert.Contains("JOIN", spec.CommandText, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [DisplayName("Employee BuildSelect filtering on the supervisor reference field produces nested JOINs")]
        public void BuildSelect_FilterOnSupervisorRelationField_GeneratesChainedJoins()
        {
            var builder = NewBuilder();
            var filter = FilterCondition.StartsWith("ref_supervisor_name", "王");

            var spec = builder.BuildSelect("Employee", "sys_id,sys_name", filter, null);

            Assert.NotNull(spec);
            int joins = CountJoins(spec.CommandText);
            Assert.True(joins >= 2, $"Expected at least 2 JOINs (Department + Employee), actual {joins}");
            Assert.Single(spec.Parameters);
        }

        [Fact]
        [DisplayName("Employee BuildSelect throws InvalidOperationException for a table name that does not exist")]
        public void BuildSelect_UnknownTableName_Throws()
        {
            var builder = NewBuilder();

            Assert.Throws<InvalidOperationException>(
                () => builder.BuildSelect("DoesNotExist", string.Empty, null, null));
        }
    }
}
