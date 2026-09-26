using System.ComponentModel;
using Polhem.Db.Providers.SqlServer;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Additional coverage for the static methods of <see cref="SqlTableSchemaProvider"/>: the MONEY, FLOAT and
    /// default branch paths that <see cref="SqlTableSchemaProviderStaticTests"/> does not reach yet.
    /// </summary>
    public class SqlTableSchemaProviderAdditionalTests
    {
        #region ParseDBDefaultValue additional types

        [Theory]
        [InlineData("MONEY", "((0))", "0", "")]
        [InlineData("MONEY", "((500))", "0", "500")]
        [InlineData("FLOAT", "((0))", "0", "")]
        [InlineData("FLOAT", "((3.14))", "0", "3.14")]
        [DisplayName("SQL Server ParseDBDefaultValue strips the ((...)) wrapper for MONEY/FLOAT")]
        public void ParseDBDefaultValue_MoneyAndFloat_StripsDoubleParens(
            string dataType, string defaultValue, string originalDefault, string expected)
        {
            var result = SqlTableSchemaProvider.ParseDBDefaultValue(dataType, defaultValue, originalDefault);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("DECIMAL", "((10.5))", "")]
        [InlineData("BIGINT", "((1000))", "")]
        [InlineData("VARBINARY", "0x", "")]
        [DisplayName("SQL Server ParseDBDefaultValue returns an empty string for an unsupported type")]
        public void ParseDBDefaultValue_UnsupportedTypes_ReturnsEmpty(
            string dataType, string defaultValue, string expected)
        {
            var result = SqlTableSchemaProvider.ParseDBDefaultValue(dataType, defaultValue, string.Empty);

            Assert.Equal(expected, result);
        }

        [Fact]
        [DisplayName("SQL Server ParseDBDefaultValue parses an NVARCHAR default expression")]
        public void ParseDBDefaultValue_NvarcharWithSingleQuotePrefix_StripsNPrefix()
        {
            // SQL Server stores an NVARCHAR default in the form (N'value').
            var result = SqlTableSchemaProvider.ParseDBDefaultValue("NVARCHAR", "(N'active')", "");

            Assert.Equal("active", result);
        }

        [Fact]
        [DisplayName("SQL Server ParseDBDefaultValue parses an NVARCHAR default expression without the N prefix")]
        public void ParseDBDefaultValue_NvarcharWithoutNPrefix_StripsParens()
        {
            // Some SQL Server expressions have no N prefix.
            var result = SqlTableSchemaProvider.ParseDBDefaultValue("NVARCHAR", "('world')", "");

            Assert.Equal("world", result);
        }

        #endregion
    }
}
