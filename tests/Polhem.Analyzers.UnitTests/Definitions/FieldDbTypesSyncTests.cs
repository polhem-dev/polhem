using System.ComponentModel;
using Polhem.Analyzers.Definitions;
using Polhem.Core.Data;

namespace Polhem.Analyzers.UnitTests.Definitions
{
    /// <summary>
    /// Keeps the field type list hard-coded in the analyzer in sync with the framework enum.
    /// </summary>
    /// <remarks>
    /// The analyzer project targets netstandard2.0 and cannot reference the net10.0 framework assemblies, so
    /// <see cref="FieldDbTypes"/> can only copy the member names of <see cref="FieldDbType"/>. This test is a drift
    /// gate: it fails as soon as the enum gains a member the analyzer lacks.
    /// </remarks>
    public class FieldDbTypesSyncTests
    {
        [Fact]
        [DisplayName("The analyzer's field type list matches the framework's FieldDbType enum exactly")]
        public void All_MatchesFrameworkEnum()
        {
            // Arrange
            var frameworkNames = Enum.GetNames<FieldDbType>()
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            // Act
            var analyzerNames = FieldDbTypes.All
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            // Assert
            Assert.Equal(frameworkNames, analyzerNames);
        }

        [Theory]
        [InlineData(FieldDbType.String)]
        [InlineData(FieldDbType.Currency)]
        [InlineData(FieldDbType.AutoIncrement)]
        [InlineData(FieldDbType.Guid)]
        [DisplayName("IsValid accepts the framework enum members")]
        public void IsValid_AcceptsFrameworkMembers(FieldDbType dbType)
        {
            // Assert
            Assert.True(FieldDbTypes.IsValid(dbType.ToString()));
        }

        [Fact]
        [DisplayName("FindCaseInsensitiveMatch returns the correct spelling for a casing-only mismatch")]
        public void FindCaseInsensitiveMatch_ReturnsCorrectCasing()
        {
            // Assert
            Assert.Equal("String", FieldDbTypes.FindCaseInsensitiveMatch("string"));
            Assert.Equal("DateTime", FieldDbTypes.FindCaseInsensitiveMatch("datetime"));
            Assert.Null(FieldDbTypes.FindCaseInsensitiveMatch("Varchar"));
        }
    }
}
