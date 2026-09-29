using System.ComponentModel;
using Polhem.Core.Data;

namespace Polhem.Core.UnitTests
{
    public class FieldDbTypeExtensionsTests
    {
        [Theory]
        [InlineData(FieldDbType.String, "")]
        [InlineData(FieldDbType.Text, "")]
        [InlineData(FieldDbType.Time, "")]
        [InlineData(FieldDbType.Boolean, false)]
        [InlineData(FieldDbType.Short, (short)0)]
        [InlineData(FieldDbType.Integer, 0)]
        [InlineData(FieldDbType.Long, 0L)]
        [DisplayName("GetDefaultValue returns the matching default value, boxed as the column's CLR type, for primitive types")]
        public void GetDefaultValue_ReturnsExpectedForPrimitiveTypes(FieldDbType type, object expected)
        {
            Assert.Equal(expected, type.GetDefaultValue());
        }

        [Theory]
        [InlineData(FieldDbType.Decimal)]
        [InlineData(FieldDbType.Currency)]
        [DisplayName("GetDefaultValue returns a decimal zero for Decimal and Currency")]
        public void GetDefaultValue_DecimalTypes_ReturnsDecimalZero(FieldDbType type)
        {
            Assert.Equal(0m, Assert.IsType<decimal>(type.GetDefaultValue()));
        }

        [Fact]
        [DisplayName("GetDefaultValue returns sensible defaults for Date, DateTime and Guid")]
        public void GetDefaultValue_ReturnsExpectedForDateAndGuidTypes()
        {
            // UTC, not `DateTime.Today`: the framework's date default is `UtcNow.Date` (ADR-032 D12).
            // Asserting the local date always fails locally between 00:00 and 08:00 at UTC+8, and CI runs in UTC so it never sees it.
            var dayBefore = DateTime.UtcNow.Date;
            var actual = Assert.IsType<DateTime>(FieldDbType.Date.GetDefaultValue());
            Assert.InRange(actual, dayBefore, DateTime.UtcNow.Date);

            var now = FieldDbType.DateTime.GetDefaultValue();
            Assert.IsType<DateTime>(now);

            Assert.Equal(Guid.Empty, FieldDbType.Guid.GetDefaultValue());
        }

        [Fact]
        [DisplayName("GetDefaultValue returns an empty byte array for Binary")]
        public void GetDefaultValue_Binary_ReturnsEmptyByteArray()
        {
            Assert.Empty(Assert.IsType<byte[]>(FieldDbType.Binary.GetDefaultValue()));
        }

        [Theory]
        [InlineData(FieldDbType.AutoIncrement)]
        [InlineData(FieldDbType.Unknown)]
        [DisplayName("GetDefaultValue returns DBNull.Value for types with no empty value")]
        public void GetDefaultValue_NoEmptyValue_ReturnsDbNull(FieldDbType type)
        {
            Assert.Equal(DBNull.Value, type.GetDefaultValue());
        }
    }
}
