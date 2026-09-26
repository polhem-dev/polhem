using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Dml;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Database;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    public class SelectCommandBuilderTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public SelectCommandBuilderTests(SharedDbFixture fx) { _fx = fx; }
        private static FormSchema BuildSimpleSchema()
        {
            var schema = new FormSchema("demo", "Demo Form");
            var table = schema.Tables!.Add("demo", "Demo Table");
            table.DbTableName = "tb_demo";
            table.Fields!.Add("Id", "Id", FieldDbType.Integer);
            table.Fields!.AddStringField("Name", "Name", 50);
            return schema;
        }

        private SelectCommandBuilder NewBuilder(FormSchema schema, DatabaseType dbType = DatabaseType.SQLServer)
            => new(schema, dbType, _fx.GetRequiredService<IDefineAccess>());

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("Build throws ArgumentException for a blank tableName")]
        public void Build_EmptyTableName_Throws(string tableName)
        {
            var schema = BuildSimpleSchema();
            var builder = NewBuilder(schema);

            Assert.Throws<ArgumentException>(() => builder.Build(tableName, string.Empty));
        }

        [Fact]
        [DisplayName("Build throws ArgumentException for a null tableName")]
        public void Build_NullTableName_Throws()
        {
            var schema = BuildSimpleSchema();
            var builder = NewBuilder(schema);

            Assert.Throws<ArgumentException>(() => builder.Build(null!, string.Empty));
        }

        [Fact]
        [DisplayName("Build produces a command with SELECT and FROM for a simple schema")]
        public void Build_SimpleSchema_ProducesSelectAndFromClauses()
        {
            var schema = BuildSimpleSchema();
            var builder = NewBuilder(schema);

            var spec = builder.Build("demo", string.Empty);

            Assert.NotNull(spec);
            Assert.Equal(DbCommandKind.DataTable, spec.Kind);
            Assert.Contains("SELECT", spec.CommandText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("FROM", spec.CommandText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("tb_demo", spec.CommandText);
        }

        [Fact]
        [DisplayName("Build with selectFields includes only the given fields")]
        public void Build_WithSelectFields_RestrictsColumns()
        {
            var schema = BuildSimpleSchema();
            var builder = NewBuilder(schema);

            var spec = builder.Build("demo", "Id");

            Assert.NotNull(spec);
            Assert.Contains("Id", spec.CommandText);
        }

        [Fact]
        [DisplayName("Build with a Between filter produces a BETWEEN clause and two parameters")]
        public void Build_BetweenFilter_ProducesBetweenClauseWithTwoParameters()
        {
            var schema = BuildSimpleSchema();
            var builder = NewBuilder(schema);
            var filter = FilterCondition.Between("Id", 10, 20);

            var spec = builder.Build("demo", string.Empty, filter);

            Assert.Contains("A.[Id] BETWEEN @p0 AND @p1", spec.CommandText);
            Assert.Equal(2, spec.Parameters.Count);
        }

        [Fact]
        [DisplayName("BuildCount with a Between filter produces a BETWEEN clause and two parameters")]
        public void BuildCount_BetweenFilter_ProducesBetweenClauseWithTwoParameters()
        {
            var schema = BuildSimpleSchema();
            var builder = NewBuilder(schema);
            var filter = FilterCondition.Between("Id", 10, 20);

            var spec = builder.BuildCount("demo", filter);

            Assert.Contains("A.[Id] BETWEEN @p0 AND @p1", spec.CommandText);
            Assert.Equal(2, spec.Parameters.Count);
        }

        [Fact]
        [DisplayName("Build removes an IgnoreIfNull filter from the WHERE clause when its value is null")]
        public void Build_IgnoreIfNullFilter_OmitsConditionFromWhere()
        {
            var schema = BuildSimpleSchema();
            var builder = NewBuilder(schema);
            var filter = FilterGroup.All(
                new FilterCondition { FieldName = "Name", Operator = ComparisonOperator.Contains, Value = null, IgnoreIfNull = true },
                FilterCondition.Equal("Id", 1)
            );

            var spec = builder.Build("demo", string.Empty, filter);

            Assert.Contains("WHERE (A.[Id] = @p0)", spec.CommandText);
            Assert.DoesNotContain("Name] LIKE", spec.CommandText);
            Assert.Single(spec.Parameters);
        }

        [Fact]
        [DisplayName("Build does not turn an IgnoreIfNull Equal filter into IS NULL when its value is null")]
        public void Build_IgnoreIfNullEqualFilter_DoesNotBecomeIsNull()
        {
            var schema = BuildSimpleSchema();
            var builder = NewBuilder(schema);
            var filter = FilterCondition.Equal("Name", null!, ignoreIfNull: true);

            var spec = builder.Build("demo", string.Empty, filter);

            Assert.DoesNotContain("WHERE", spec.CommandText, StringComparison.Ordinal);
            Assert.DoesNotContain("IS NULL", spec.CommandText, StringComparison.Ordinal);
        }
    }
}
