using System.ComponentModel;
using Polhem.Definition.Filters;
using Polhem.Db.Providers.SqlServer;
using Polhem.Tests.Shared;
using Polhem.Definition.Database;
using Polhem.Definition.Sorting;
using Polhem.Definition.Storage;

namespace Polhem.Db.UnitTests
{
    public class BuildSelectTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public BuildSelectTests(SharedDbFixture fx) { _fx = fx; }
        private IDefineAccess DefineAccess => _fx.GetRequiredService<IDefineAccess>();

        private SqlFormCommandBuilder NewBuilder(string progId)
            => new(DefineAccess.GetFormSchema(progId), DefineAccess);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("BuildSelect produces no JOIN when only master fields are selected")]
        public void BuildSelect_SelectOnlyMasterFields_NoJoin()
        {
            var builder = NewBuilder("Project");
            var command = builder.BuildSelect("Project", "sys_id,sys_name", null, null);

            Assert.NotNull(command);
            Assert.NotNull(command.CommandText);
            Assert.DoesNotContain("JOIN", command.CommandText, StringComparison.OrdinalIgnoreCase);
        }

        [DbFact(DatabaseType.SQLServer)]
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

        [DbFact(DatabaseType.SQLServer)]
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

        [DbFact(DatabaseType.SQLServer)]
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

        [DbFact(DatabaseType.SQLServer)]
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

        [DbFact(DatabaseType.SQLServer)]
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

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SqlFormCommandBuilder builds a Select command with a filter and a sort")]
        public void BuildSelect_WithFilterAndSort_ReturnsCommands()
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
            Assert.NotNull(command);

            var command2 = builder.BuildSelect("Employee", "sys_id,sys_name,ref_dept_name,ref_supervisor_name", filter, sortFields);
            Assert.NotNull(command2);

            filter = new FilterCondition
            {
                FieldName = "ref_supervisor_id",
                Operator = ComparisonOperator.Equal,
                Value = "U001"
            };
            var command3 = builder.BuildSelect("Employee", "sys_id,sys_name", filter, sortFields);
            Assert.NotNull(command2);
        }
    }
}
