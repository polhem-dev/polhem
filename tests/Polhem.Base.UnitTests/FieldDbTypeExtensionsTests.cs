using System.ComponentModel;
using Polhem.Base.Data;

namespace Polhem.Base.UnitTests
{
    public class FieldDbTypeExtensionsTests
    {
        [Theory]
        [InlineData(FieldDbType.String, "")]
        [InlineData(FieldDbType.Text, "")]
        [InlineData(FieldDbType.Boolean, false)]
        [InlineData(FieldDbType.Integer, 0)]
        [InlineData(FieldDbType.Decimal, 0)]
        [InlineData(FieldDbType.Currency, 0)]
        [DisplayName("GetDefaultValue returns the matching default value for primitive types")]
        public void GetDefaultValue_ReturnsExpectedForPrimitiveTypes(FieldDbType type, object expected)
        {
            Assert.Equal(expected, type.GetDefaultValue());
        }

        [Fact]
        [DisplayName("GetDefaultValue returns sensible defaults for Date, DateTime and Guid")]
        public void GetDefaultValue_ReturnsExpectedForDateAndGuidTypes()
        {
            // UTC, not `DateTime.Today`: the framework's date default is `UtcNow.Date` (ADR-032 D12).
            // Asserting the local date always fails locally between 00:00 and 08:00 at UTC+8, and CI runs in UTC so it never sees it.
            Assert.Equal(DateTime.UtcNow.Date, FieldDbType.Date.GetDefaultValue());

            var now = FieldDbType.DateTime.GetDefaultValue();
            Assert.IsType<DateTime>(now);

            Assert.Equal(Guid.Empty, FieldDbType.Guid.GetDefaultValue());
        }

        [Fact]
        [DisplayName("GetDefaultValue returns DBNull.Value for unmapped types")]
        public void GetDefaultValue_UnmappedType_ReturnsDbNull()
        {
            Assert.Equal(DBNull.Value, FieldDbType.Binary.GetDefaultValue());
            Assert.Equal(DBNull.Value, FieldDbType.Unknown.GetDefaultValue());
            Assert.Equal(DBNull.Value, FieldDbType.AutoIncrement.GetDefaultValue());
            Assert.Equal(DBNull.Value, FieldDbType.Short.GetDefaultValue());
            Assert.Equal(DBNull.Value, FieldDbType.Long.GetDefaultValue());
        }
    }
}
