using System.ComponentModel;
using Polhem.Definition.Filters;
using Polhem.Db.Dml;
using Polhem.Definition.Database;

namespace Polhem.Db.UnitTests
{
    public class WhereBuilderTests
    {
        [Fact]
        [DisplayName("Build produces the SQL Server WHERE clause for an equality condition")]
        public void Build_EqualCondition_BuildsSqlServerWhere()
        {
            var root = FilterCondition.Equal("DeptId", 10);
            var builder = new WhereBuilder(DatabaseType.SQLServer);
            var result = builder.Build(root, null);
            Assert.Equal("WHERE DeptId = @p0", result.WhereClause);
            Assert.NotNull(result.Parameters);
            Assert.Single(result.Parameters);
            Assert.Equal(10, result.Parameters["@p0"]);
        }

        [Fact]
        [DisplayName("Build adds wildcards for a Contains condition")]
        public void Build_LikeContains_AddsWildcards()
        {
            var root = FilterCondition.Contains("Name", "Lee");
            var builder = new WhereBuilder(DatabaseType.SQLServer);
            var result = builder.Build(root, null, includeWhereKeyword: false);
            Assert.Equal("Name LIKE @p0", result.WhereClause);
            Assert.NotNull(result.Parameters);
            Assert.Equal("%Lee%", result.Parameters["@p0"]);
        }

        [Fact]
        [DisplayName("Build parenthesizes nested AND/OR groups")]
        public void Build_GroupAndOr_BuildsParentheses()
        {
            var root = FilterGroup.All(
                FilterCondition.Equal("DeptId", 10),
                FilterGroup.Any(
                    FilterCondition.Contains("Name", "Lee"),
                    FilterCondition.Between("HireDate", new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Unspecified))
                )
            );

            var builder = new WhereBuilder(DatabaseType.SQLServer);
            var result = builder.Build(root, null);
            Assert.StartsWith("WHERE (", result.WhereClause);
            Assert.Contains(" AND ", result.WhereClause);
            Assert.Contains(" OR ", result.WhereClause);
            Assert.NotNull(result.Parameters);
            Assert.Equal(4, result.Parameters.Count);
        }

        [Fact]
        [DisplayName("Build produces IS NULL for an equality condition with a null value")]
        public void Build_NullEquals_BecomesIsNull()
        {
            var root = new FilterCondition { FieldName = "Memo", Operator = ComparisonOperator.Equal, Value = null };
            var builder = new WhereBuilder(DatabaseType.SQLServer);
            var result = builder.Build(root, null);
            Assert.Equal("WHERE Memo IS NULL", result.WhereClause);
            Assert.True(result.Parameters == null || result.Parameters.Count == 0);
        }

        [Fact]
        [DisplayName("Build ignores the condition when IgnoreIfNull is true and the value is null")]
        public void Build_IgnoreIfNull_DropsNullCondition()
        {
            var root = FilterGroup.All(
                new FilterCondition { FieldName = "Keyword", Operator = ComparisonOperator.Contains, Value = null, IgnoreIfNull = true },
                FilterCondition.Equal("DeptId", 1)
            );

            var builder = new WhereBuilder(DatabaseType.SQLServer);
            var result = builder.Build(root, null);
            Assert.Equal("WHERE (DeptId = @p0)", result.WhereClause);
            Assert.Single(result.Parameters!);
        }

        [Fact]
        [DisplayName("Build produces an always-false constant for an IN condition with an empty collection")]
        public void Build_InWithEmptyList_ReturnsFalseConstant()
        {
            var root = FilterCondition.In("Id", []);
            var builder = new WhereBuilder(DatabaseType.SQLServer);
            var result = builder.Build(root, null, includeWhereKeyword: false);
            Assert.Equal("1 = 0", result.WhereClause);
            Assert.True(result.Parameters == null || result.Parameters.Count == 0);
        }

        [Fact]
        [DisplayName("Build produces an IN condition with several values")]
        public void Build_InWithMultipleValues_BuildsCorrectly()
        {
            var root = FilterCondition.In("Id", [1, 2, 3, 4]);
            var builder = new WhereBuilder(DatabaseType.SQLServer);
            var result = builder.Build(root, null, includeWhereKeyword: false);
            Assert.Equal("Id IN (@p0, @p1, @p2, @p3)", result.WhereClause);
            Assert.NotNull(result.Parameters);
            Assert.Equal(4, result.Parameters.Count);
        }

        [Fact]
        [DisplayName("Build returns an empty WHERE clause for an empty FilterGroup")]
        public void Build_EmptyFilterGroup_ReturnsEmptyWhereClause()
        {
            var root = new FilterGroup(); // Nodes is empty by default.
            var builder = new WhereBuilder(DatabaseType.SQLServer);
            var result = builder.Build(root, null);
            Assert.Equal(string.Empty, result.WhereClause);
        }

        [Fact]
        [DisplayName("Build returns an empty WhereBuildResult for a null root")]
        public void Build_NullRoot_ReturnsEmptyResult()
        {
            var builder = new WhereBuilder(DatabaseType.SQLServer);
            var result = builder.Build(null, null);
            Assert.Equal(string.Empty, result.WhereClause);
        }

        [Theory]
        [InlineData(ComparisonOperator.GreaterThan, ">")]
        [InlineData(ComparisonOperator.GreaterThanOrEqual, ">=")]
        [InlineData(ComparisonOperator.LessThan, "<")]
        [InlineData(ComparisonOperator.LessThanOrEqual, "<=")]
        [InlineData(ComparisonOperator.NotEqual, "<>")]
        [DisplayName("Build produces the matching SQL operator for each comparison operator")]
        public void Build_ComparisonOperators_BuildExpectedSql(ComparisonOperator op, string sqlOp)
        {
            var root = new FilterCondition { FieldName = "Age", Operator = op, Value = 18 };
            var builder = new WhereBuilder(DatabaseType.SQLServer);
            var result = builder.Build(root, null, includeWhereKeyword: false);
            Assert.Equal($"Age {sqlOp} @p0", result.WhereClause);
            Assert.Equal(18, result.Parameters!["@p0"]);
        }

        [Fact]
        [DisplayName("Build uses the raw value for a Like condition")]
        public void Build_Like_UsesRawValue()
        {
            var root = new FilterCondition { FieldName = "Name", Operator = ComparisonOperator.Like, Value = "Lee%" };
            var builder = new WhereBuilder(DatabaseType.SQLServer);
            var result = builder.Build(root, null, includeWhereKeyword: false);
            Assert.Equal("Name LIKE @p0", result.WhereClause);
            Assert.Equal("Lee%", result.Parameters!["@p0"]);
        }

        [Fact]
        [DisplayName("Build appends a wildcard for StartsWith")]
        public void Build_StartsWith_AddsTrailingWildcard()
        {
            var root = FilterCondition.StartsWith("Name", "Lee");
            var builder = new WhereBuilder(DatabaseType.SQLServer);
            var result = builder.Build(root, null, includeWhereKeyword: false);
            Assert.Equal("Name LIKE @p0", result.WhereClause);
            Assert.Equal("Lee%", result.Parameters!["@p0"]);
        }

        [Fact]
        [DisplayName("Build prepends a wildcard for EndsWith")]
        public void Build_EndsWith_AddsLeadingWildcard()
        {
            var root = FilterCondition.EndsWith("Name", "Lee");
            var builder = new WhereBuilder(DatabaseType.SQLServer);
            var result = builder.Build(root, null, includeWhereKeyword: false);
            Assert.Equal("Name LIKE @p0", result.WhereClause);
            Assert.Equal("%Lee", result.Parameters!["@p0"]);
        }

        [Fact]
        [DisplayName("Build produces IS NOT NULL for NotEqual with a null value")]
        public void Build_NotEqualNull_BecomesIsNotNull()
        {
            var root = new FilterCondition { FieldName = "Memo", Operator = ComparisonOperator.NotEqual, Value = null };
            var builder = new WhereBuilder(DatabaseType.SQLServer);
            var result = builder.Build(root, null);
            Assert.Equal("WHERE Memo IS NOT NULL", result.WhereClause);
        }

        [Fact]
        [DisplayName("Build throws InvalidOperationException for a comparison operator that does not support null")]
        public void Build_UnsupportedNullOperator_Throws()
        {
            var root = new FilterCondition { FieldName = "Age", Operator = ComparisonOperator.GreaterThan, Value = null };
            var builder = new WhereBuilder(DatabaseType.SQLServer);
            Assert.Throws<InvalidOperationException>(() => builder.Build(root, null));
        }

        [Fact]
        [DisplayName("Build ignores a Between condition missing its second value when IgnoreIfNull=true")]
        public void Build_BetweenMissingSecondValue_IgnoreIfNull_DropsCondition()
        {
            var root = new FilterCondition
            {
                FieldName = "Age",
                Operator = ComparisonOperator.Between,
                Value = 18,
                SecondValue = null,
                IgnoreIfNull = true
            };
            var builder = new WhereBuilder(DatabaseType.SQLServer);
            var result = builder.Build(root, null, includeWhereKeyword: false);
            Assert.Equal(string.Empty, result.WhereClause);
        }

        [Fact]
        [DisplayName("Build throws for a Between condition missing its second value without IgnoreIfNull")]
        public void Build_BetweenMissingSecondValue_Throws()
        {
            var root = new FilterCondition
            {
                FieldName = "Age",
                Operator = ComparisonOperator.Between,
                Value = 18,
                SecondValue = null
            };
            var builder = new WhereBuilder(DatabaseType.SQLServer);
            Assert.Throws<InvalidOperationException>(() => builder.Build(root, null));
        }

        [Fact]
        [DisplayName("Build produces a BETWEEN clause for a complete Between condition")]
        public void Build_Between_BuildsBetweenClause()
        {
            var root = FilterCondition.Between("Age", 18, 60);
            var builder = new WhereBuilder(DatabaseType.SQLServer);
            var result = builder.Build(root, null, includeWhereKeyword: false);
            Assert.Equal("Age BETWEEN @p0 AND @p1", result.WhereClause);
            Assert.Equal(18, result.Parameters!["@p0"]);
            Assert.Equal(60, result.Parameters["@p1"]);
        }

        [Fact]
        [DisplayName("Build throws for an IN condition with a non-enumerable value")]
        public void Build_InWithNonEnumerable_Throws()
        {
            var root = new FilterCondition { FieldName = "Id", Operator = ComparisonOperator.In, Value = 1 };
            var builder = new WhereBuilder(DatabaseType.SQLServer);
            Assert.Throws<InvalidOperationException>(() => builder.Build(root, null));
        }

        [Fact]
        [DisplayName("Build throws InvalidOperationException for an empty FieldName")]
        public void Build_EmptyFieldName_Throws()
        {
            var root = new FilterCondition { FieldName = "", Operator = ComparisonOperator.Equal, Value = 1 };
            var builder = new WhereBuilder(DatabaseType.SQLServer);
            Assert.Throws<InvalidOperationException>(() => builder.Build(root, null));
        }

        /// <summary>
        /// Builds a context whose field mapping resolves `RefDeptName` to a joined table,
        /// leaving every other field on the main table.
        /// </summary>
        private static SelectContext BuildSelectContext()
        {
            var context = new SelectContext();
            context.FieldMappings.Add(new QueryFieldMapping
            {
                FieldName = "RefDeptName",
                SourceAlias = "B",
                SourceField = "dept_name",
            });
            return context;
        }

        [Fact]
        [DisplayName("Build keeps the second value of a Between condition after selectContext rewrites the field name")]
        public void Build_BetweenWithSelectContext_PreservesSecondValue()
        {
            var root = FilterCondition.Between("HireDate", new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc));
            var builder = new WhereBuilder(DatabaseType.SQLServer);

            var result = builder.Build(root, BuildSelectContext(), includeWhereKeyword: false);

            Assert.Equal("A.[HireDate] BETWEEN @p0 AND @p1", result.WhereClause);
            Assert.NotNull(result.Parameters);
            Assert.Equal(2, result.Parameters.Count);
            Assert.Equal(new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc), result.Parameters["@p1"]);
        }

        [Fact]
        [DisplayName("Build keeps the second value of a Between condition after the relation field mapping is applied")]
        public void Build_BetweenOnMappedField_PreservesSecondValue()
        {
            var root = FilterCondition.Between("RefDeptName", "A", "M");
            var builder = new WhereBuilder(DatabaseType.SQLServer);

            var result = builder.Build(root, BuildSelectContext(), includeWhereKeyword: false);

            Assert.Equal("B.[dept_name] BETWEEN @p0 AND @p1", result.WhereClause);
            Assert.Equal("M", result.Parameters!["@p1"]);
        }

        [Fact]
        [DisplayName("Build still ignores an IgnoreIfNull condition after selectContext rewrites the field name")]
        public void Build_IgnoreIfNullWithSelectContext_DropsNullCondition()
        {
            var root = FilterGroup.All(
                new FilterCondition { FieldName = "Keyword", Operator = ComparisonOperator.Contains, Value = null, IgnoreIfNull = true },
                FilterCondition.Equal("DeptId", 1)
            );
            var builder = new WhereBuilder(DatabaseType.SQLServer);

            var result = builder.Build(root, BuildSelectContext());

            Assert.Equal("WHERE (A.[DeptId] = @p0)", result.WhereClause);
            Assert.Single(result.Parameters!);
        }

        [Fact]
        [DisplayName("Build does not turn an IgnoreIfNull Equal condition into IS NULL after selectContext rewrites it")]
        public void Build_IgnoreIfNullEqualWithSelectContext_DoesNotBecomeIsNull()
        {
            var root = new FilterCondition { FieldName = "Memo", Operator = ComparisonOperator.Equal, Value = null, IgnoreIfNull = true };
            var builder = new WhereBuilder(DatabaseType.SQLServer);

            var result = builder.Build(root, BuildSelectContext());

            Assert.Equal(string.Empty, result.WhereClause);
        }

        [Fact]
        [DisplayName("Build ignores a Between condition missing its second value with IgnoreIfNull=true after selectContext rewrites it, instead of throwing")]
        public void Build_BetweenMissingSecondValueWithSelectContext_DropsCondition()
        {
            var root = new FilterCondition
            {
                FieldName = "Age",
                Operator = ComparisonOperator.Between,
                Value = 18,
                SecondValue = null,
                IgnoreIfNull = true
            };
            var builder = new WhereBuilder(DatabaseType.SQLServer);

            var result = builder.Build(root, BuildSelectContext(), includeWhereKeyword: false);

            Assert.Equal(string.Empty, result.WhereClause);
        }
    }
}
