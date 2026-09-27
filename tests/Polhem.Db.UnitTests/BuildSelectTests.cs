using System.ComponentModel;
using Polhem.Definition.Filters;
using Polhem.Db.Providers.SqlServer;
using Polhem.Tests.Shared;
using Polhem.Definition.Sorting;
using Polhem.Definition.Storage;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// SQL generation of <see cref="SqlFormCommandBuilder.BuildSelect"/>. Pure logic: the fixture only supplies the
    /// FormSchema from <c>tests/Define</c>, so no database is needed and nothing is skipped without one.
    /// </summary>
    public class BuildSelectTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public BuildSelectTests(PolhemTestFixture fx) { _fx = fx; }
        private IDefineAccess DefineAccess => _fx.GetRequiredService<IDefineAccess>();

        private SqlFormCommandBuilder NewBuilder(string progId)
            => new(DefineAccess.GetFormSchema(progId), DefineAccess);

        [Fact]
        [DisplayName("BuildSelect produces no JOIN when only master fields are selected")]
        public void BuildSelect_SelectOnlyMasterFields_NoJoin()
        {
            var builder = NewBuilder("Project");
            var command = builder.BuildSelect("Project", "sys_id,sys_name", null, null);

            Assert.NotNull(command);
            Assert.NotNull(command.CommandText);
            Assert.DoesNotContain("JOIN", command.CommandText, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [DisplayName("BuildSelect produces a JOIN when the Where condition uses a reference field")]
        public void BuildSelect_WhereOnReferencedField_GeneratesJoin()
        {
            var builder = NewBuilder("Project");
            var filter = new FilterCondition("ref_pm_name", ComparisonOperator.StartsWith, "張");
            var command = builder.BuildSelect("Project", "sys_id,sys_name", filter, null);

            Assert.NotNull(command);
            Assert.NotNull(command.CommandText);
            Assert.Contains("JOIN", command.CommandText, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [DisplayName("BuildSelect produces a JOIN when Order By uses a reference field")]
        public void BuildSelect_OrderByReferencedField_GeneratesJoin()
        {
            var builder = NewBuilder("Project");
            var sortFields = new SortFieldCollection
            {
                new SortField("ref_pm_dept_name", SortDirection.Asc)
            };

            var command = builder.BuildSelect("Project", "sys_id,sys_name", null, sortFields);

            Assert.NotNull(command);
            Assert.NotNull(command.CommandText);
            Assert.Contains("JOIN", command.CommandText, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [DisplayName("BuildSelect produces several JOINs when several reference fields are selected")]
        public void BuildSelect_SelectWithMultipleReferences_GeneratesMultipleJoins()
        {
            var builder = NewBuilder("Project");

            // Assumes `ref_owner_dept_name` and `ref_pm_dept_name` come from different reference tables.
            var command = builder.BuildSelect("Project", "sys_id,sys_name,ref_owner_dept_name,ref_pm_dept_name", null, null);

            Assert.NotNull(command);
            Assert.NotNull(command.CommandText);
            int joinCount = 0;
            int index = 0;
            while ((index = command.CommandText.IndexOf("JOIN", index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                joinCount++;
                index += "JOIN".Length;
            }
            Assert.True(joinCount >= 2, $"Expected at least 2 JOINs, actual:{joinCount}");

            // Every top-level foreign key must JOIN from the master alias A. An earlier bug made the second
            // relation JOIN from the alias of the first one (such as `B.pm_rowid`), which produced invalid SQL
            // ("no such column"). Identifier quotes are stripped before comparing, so no dialect's quoting is assumed.
            var normalized = command.CommandText
                .Replace("[", "").Replace("]", "").Replace("\"", "").Replace("`", "");
            Assert.Contains("A.owner_dept_rowid", normalized, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("A.pm_rowid", normalized, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [DisplayName("BuildSelect with a multi-condition FilterGroup produces the parameters and JOINs")]
        public void BuildSelect_FilterGroupWithMultipleConditions_GeneratesParametersAndJoin()
        {
            var builder = NewBuilder("Project");

            var filterGroup = FilterGroup.All(
                FilterCondition.Contains("sys_name", "專案"),
                FilterCondition.Equal("ref_pm_name", "張三")
            );

            var sortFields = new SortFieldCollection
            {
                new SortField("sys_id", SortDirection.Asc)
            };

            var command = builder.BuildSelect(
                "Project",
                "sys_id,sys_name",
                filterGroup,
                sortFields
            );

            Assert.NotNull(command);
            Assert.NotNull(command.CommandText);
            Assert.Equal(2, command.Parameters.Count);
            // Only the table behind `ref_pm_name` is joined, because the select list needs no other reference field.
            Assert.Contains("JOIN", command.CommandText, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [DisplayName("SqlFormCommandBuilder builds a Select command")]
        public void BuildSelect_WithAndWithoutFields_ReturnsCommands()
        {
            var builder = NewBuilder("Employee");
            var command = builder.BuildSelect("Employee", string.Empty, null, null);
            var command2 = builder.BuildSelect("Employee", "sys_id,sys_name,ref_dept_name,ref_supervisor_name", null, null);

            Assert.NotNull(command);
            Assert.False(string.IsNullOrWhiteSpace(command.CommandText));
            Assert.NotNull(command2);
            Assert.False(string.IsNullOrWhiteSpace(command2.CommandText));
        }

        [Fact]
        [DisplayName("BuildSelect with a filter and a sort emits WHERE, ORDER BY and the filter parameter, and joins the relations a reference-field filter needs")]
        public void BuildSelect_WithFilterAndSort_EmitsFilterSortAndJoins()
        {
            var builder = NewBuilder("Employee");

            var filter = new FilterCondition
            {
                FieldName = "sys_id",
                Operator = ComparisonOperator.Equal,
                Value = "001"
            };

            var sortFields = new SortFieldCollection
            {
                new SortField("sys_id", SortDirection.Asc)
            };

            var command = builder.BuildSelect("Employee", string.Empty, filter, sortFields);
            Assert.Contains("WHERE", command.CommandText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ORDER BY", command.CommandText, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("001", Assert.Single(command.Parameters).Value);

            var command2 = builder.BuildSelect("Employee", "sys_id,sys_name,ref_dept_name,ref_supervisor_name", filter, sortFields);
            Assert.True(CountJoins(command2.CommandText) >= 2,
                $"Selecting two reference fields from different relations must join both: {command2.CommandText}");
            Assert.Contains("ORDER BY", command2.CommandText, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("001", Assert.Single(command2.Parameters).Value);

            filter = new FilterCondition
            {
                FieldName = "ref_supervisor_id",
                Operator = ComparisonOperator.Equal,
                Value = "U001"
            };
            var command3 = builder.BuildSelect("Employee", "sys_id,sys_name", filter, sortFields);
            // The select list has no reference field, so every JOIN comes from the filter. `ref_supervisor_id` is the
            // department's `ref_manager_id`, a relation of a relation, so reaching it takes two JOINs.
            Assert.Equal(2, CountJoins(command3.CommandText));
            Assert.Equal("U001", Assert.Single(command3.Parameters).Value);
        }

        private static int CountJoins(string commandText)
        {
            int count = 0;
            int index = 0;
            while ((index = commandText.IndexOf("JOIN", index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                count++;
                index += "JOIN".Length;
            }
            return count;
        }
    }
}
