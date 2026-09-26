using System.ComponentModel;
using System.Reflection;
using Polhem.Analyzers.Definitions;
using Polhem.Definition.Database;

namespace Polhem.Analyzers.UnitTests.Definitions
{
    /// <summary>
    /// Keeps the scope list hard-coded in the analyzer in sync with the framework constants.
    /// </summary>
    /// <remarks>
    /// The analyzer project targets netstandard2.0 and cannot reference the net10.0 <c>Polhem.Definition</c>, so
    /// <see cref="DbCategoryScopes"/> can only copy the values of <see cref="DbCategoryIds"/>. This test project
    /// references both and acts as a drift gate: when the framework adds a scope the analyzer lacks, this test fails.
    /// </remarks>
    public class DbCategoryScopesSyncTests
    {
        [Fact]
        [DisplayName("The analyzer's scope list matches the framework's DbCategoryIds exactly")]
        public void All_MatchesFrameworkConstants()
        {
            // Arrange
            var frameworkScopes = typeof(DbCategoryIds)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue()!)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            // Act
            var analyzerScopes = DbCategoryScopes.All
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            // Assert
            Assert.Equal(frameworkScopes, analyzerScopes);
        }

        [Fact]
        [DisplayName("IsValid accepts every framework constant")]
        public void IsValid_AcceptsEveryFrameworkConstant()
        {
            // Assert
            Assert.True(DbCategoryScopes.IsValid(DbCategoryIds.Common));
            Assert.True(DbCategoryScopes.IsValid(DbCategoryIds.Company));
            Assert.True(DbCategoryScopes.IsValid(DbCategoryIds.Log));
        }
    }
}
