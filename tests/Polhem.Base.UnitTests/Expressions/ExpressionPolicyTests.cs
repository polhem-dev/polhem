using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Base.Expressions;

namespace Polhem.Base.UnitTests.Expressions
{
    /// <summary>
    /// Tests for <see cref="ExpressionPolicy"/>: the FieldDbType → CLR type mapping, and the consistent
    /// coercion of DBNull/null to the type's default value.
    /// </summary>
    public class ExpressionPolicyTests
    {
        [Theory]
        [InlineData(FieldDbType.String, typeof(string))]
        [InlineData(FieldDbType.Integer, typeof(int))]
        [InlineData(FieldDbType.Long, typeof(long))]
        [InlineData(FieldDbType.Decimal, typeof(decimal))]
        [InlineData(FieldDbType.Currency, typeof(decimal))]
        [InlineData(FieldDbType.Boolean, typeof(bool))]
        [InlineData(FieldDbType.DateTime, typeof(DateTime))]
        [InlineData(FieldDbType.Guid, typeof(Guid))]
        [DisplayName("ToClrType maps each FieldDbType to its CLR type")]
        public void ToClrType_MapsFieldDbTypeToClrType(FieldDbType dbType, Type expected)
        {
            Assert.Equal(expected, ExpressionPolicy.ToClrType(dbType));
        }

        [Fact]
        [DisplayName("CoerceValue returns 0 (decimal) for DBNull in a numeric column")]
        public void CoerceValue_DbNullCurrency_ReturnsZero()
        {
            var result = ExpressionPolicy.CoerceValue(DBNull.Value, FieldDbType.Currency);

            Assert.Equal(0m, result);
        }

        [Fact]
        [DisplayName("CoerceValue returns an empty string for DBNull in a string column")]
        public void CoerceValue_DbNullString_ReturnsEmpty()
        {
            var result = ExpressionPolicy.CoerceValue(DBNull.Value, FieldDbType.String);

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        [DisplayName("CoerceValue returns false for null in a boolean column")]
        public void CoerceValue_NullBoolean_ReturnsFalse()
        {
            var result = ExpressionPolicy.CoerceValue(null, FieldDbType.Boolean);

            Assert.False((bool)result!);
        }

        [Fact]
        [DisplayName("CoerceValue returns Guid.Empty for DBNull in a Guid column")]
        public void CoerceValue_DbNullGuid_ReturnsEmptyGuid()
        {
            var result = ExpressionPolicy.CoerceValue(DBNull.Value, FieldDbType.Guid);

            Assert.Equal(Guid.Empty, result);
        }

        [Fact]
        [DisplayName("CoerceValue returns the value unchanged when the type already matches")]
        public void CoerceValue_MatchingType_ReturnsSameValue()
        {
            var result = ExpressionPolicy.CoerceValue(12.5m, FieldDbType.Currency);

            Assert.Equal(12.5m, result);
        }

        [Fact]
        [DisplayName("CoerceValue converts an int value for a decimal column to decimal")]
        public void CoerceValue_IntToDecimal_Converts()
        {
            var result = ExpressionPolicy.CoerceValue(5, FieldDbType.Decimal);

            Assert.IsType<decimal>(result);
            Assert.Equal(5m, result);
        }

        [Fact]
        [DisplayName("CoerceValue parses a string value for a Guid column into a Guid (the wire and SQLite carry GUIDs as TEXT)")]
        public void CoerceValue_StringToGuid_Parses()
        {
            var id = Guid.NewGuid();

            var result = ExpressionPolicy.CoerceValue(id.ToString(), FieldDbType.Guid);

            Assert.IsType<Guid>(result);
            Assert.Equal(id, result);
        }

        [Fact]
        [DisplayName("CoerceValue returns a Guid value for a Guid column unchanged")]
        public void CoerceValue_GuidToGuid_ReturnsSameValue()
        {
            var id = Guid.NewGuid();

            var result = ExpressionPolicy.CoerceValue(id, FieldDbType.Guid);

            Assert.Equal(id, result);
        }

        [Fact]
        [DisplayName("CoerceValue decodes a base64 string for a Binary column into byte[]")]
        public void CoerceValue_Base64ToBinary_Decodes()
        {
            var bytes = new byte[] { 1, 2, 3, 4 };

            var result = ExpressionPolicy.CoerceValue(Convert.ToBase64String(bytes), FieldDbType.Binary);

            Assert.Equal(bytes, Assert.IsType<byte[]>(result));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("CoerceValue returns Guid.Empty for an empty or whitespace string in a Guid column (an unselected key or an empty TEXT GUID column) instead of throwing from Parse")]
        public void CoerceValue_EmptyStringToGuid_ReturnsEmptyGuid(string value)
        {
            var result = ExpressionPolicy.CoerceValue(value, FieldDbType.Guid);

            Assert.Equal(Guid.Empty, result);
        }

        [Fact]
        [DisplayName("CoerceValue returns an empty array for an empty string in a Binary column instead of throwing from FromBase64String")]
        public void CoerceValue_EmptyStringToBinary_ReturnsEmptyArray()
        {
            var result = ExpressionPolicy.CoerceValue(string.Empty, FieldDbType.Binary);

            Assert.Empty(Assert.IsType<byte[]>(result));
        }
    }
}
