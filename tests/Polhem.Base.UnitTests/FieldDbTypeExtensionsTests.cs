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

        [Fact]
        [DisplayName("ToFieldValue takes the conversion branch that matches the FieldDbType")]
        public void ToFieldValue_VariousDbTypes_ReturnsExpectedResult()
        {
            Assert.Equal("abc", FieldDbType.String.ToFieldValue("abc"));
            Assert.Equal("abc", FieldDbType.Text.ToFieldValue("abc"));
            Assert.True((bool)FieldDbType.Boolean.ToFieldValue("1")!);
            Assert.Equal(123, FieldDbType.Integer.ToFieldValue("123"));
            Assert.Equal(123.45m, FieldDbType.Decimal.ToFieldValue("123.45"));
            Assert.Equal(123.45m, FieldDbType.Currency.ToFieldValue("123.45"));

            var date = new DateTime(2026, 4, 18, 0, 0, 0, DateTimeKind.Unspecified);
            Assert.Equal(date, FieldDbType.Date.ToFieldValue("2026-04-18"));
            Assert.Equal(date, FieldDbType.DateTime.ToFieldValue("2026-04-18"));
            // Regression: the Date branch must not switch to `ValueUtilities.CDateOnly`, which returns `DateOnly`.
            // A calendar-date field is still a DateTime column on the `DataColumn`, and `DateOnly` does not
            // implement `IConvertible`, so storing it back throws.
            Assert.IsType<DateTime>(FieldDbType.Date.ToFieldValue("2026-04-18"));

            var guid = Guid.NewGuid();
            Assert.Equal(guid, FieldDbType.Guid.ToFieldValue(guid.ToString()));

            // A FieldDbType without a conversion branch returns the value unchanged.
            var raw = new byte[] { 0x01, 0x02 };
            Assert.Same(raw, FieldDbType.Binary.ToFieldValue(raw));
        }

        [Fact]
        [DisplayName("ToDbFieldValue returns DBNull.Value for DateTime.MinValue and otherwise delegates to ToFieldValue")]
        public void ToDbFieldValue_DateTimeMinValue_ReturnsDbNull()
        {
            Assert.Equal(DBNull.Value, FieldDbType.DateTime.ToDbFieldValue(DateTime.MinValue));
            Assert.Equal(DBNull.Value, FieldDbType.Date.ToDbFieldValue(DateTime.MinValue));

            var date = new DateTime(2026, 4, 18, 0, 0, 0, DateTimeKind.Unspecified);
            Assert.Equal(date, FieldDbType.DateTime.ToDbFieldValue(date));
            Assert.Equal("abc", FieldDbType.String.ToDbFieldValue("abc"));
        }
    }
}
