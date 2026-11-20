using System.ComponentModel;
using Polhem.Db;
using Polhem.Db.Dml;
using Polhem.Definition;
using Polhem.Definition.Filters;
using Polhem.Definition.Sorting;
using Polhem.Repository.Form;

namespace Polhem.Repository.UnitTests.Form
{
    /// <summary>
    /// Tests for <see cref="CompanyScopedFormCommandBuilder"/>: it confines the filter to one company and passes the
    /// rest of the query to the dialect's builder unchanged.
    /// </summary>
    public class CompanyScopedFormCommandBuilderTests
    {
        [Fact]
        [DisplayName("BuildSelect confines the filter to the company and keeps the fields, order and page of the query")]
        public void BuildSelect_ConfinesFilter_KeepsTheRestOfTheQuery()
        {
            var inner = new RecordingBuilder();
            var sort = new SortFieldCollection { new SortField("sys_id", SortDirection.Asc) };
            var callerFilter = FilterCondition.Equal("sys_name", "A");
            var query = new FormSelectQuery
            {
                SelectFields = "sys_id,sys_name",
                Filter = callerFilter,
                SortFields = sort,
                Skip = 20,
                Take = 10,
            };

            new CompanyScopedFormCommandBuilder(inner, "C1").BuildSelect("Project", query);

            var passed = Assert.IsType<FormSelectQuery>(inner.LastQuery);
            Assert.Equal("sys_id,sys_name", passed.SelectFields);
            Assert.Same(sort, passed.SortFields);
            Assert.Equal(20, passed.Skip);
            Assert.Equal(10, passed.Take);
            var group = Assert.IsType<FilterGroup>(passed.Filter);
            Assert.Contains(group.Nodes, node => node is FilterCondition { FieldName: SysFields.CompanyId });
            Assert.Contains(callerFilter, group.Nodes);
            Assert.Same(callerFilter, query.Filter);
        }

        private sealed class RecordingBuilder : IFormCommandBuilder
        {
            public FormSelectQuery? LastQuery { get; private set; }

            public DbCommandSpec BuildSelect(string tableName, FormSelectQuery query)
            {
                LastQuery = query;
                return new DbCommandSpec(DbCommandKind.DataTable, "SELECT 1");
            }

            public DbCommandSpec BuildCount(string tableName, FilterNode? filter) =>
                new(DbCommandKind.Scalar, "SELECT 1");

            public DbCommandSpec BuildDelete(string tableName, FilterNode filter) =>
                new(DbCommandKind.NonQuery, "SELECT 1");
        }
    }
}
