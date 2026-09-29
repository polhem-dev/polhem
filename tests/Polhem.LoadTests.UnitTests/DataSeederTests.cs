using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Database;
using Polhem.LoadTests.Bootstrap;

namespace Polhem.LoadTests.UnitTests
{
    /// <summary>
    /// Tests for the value generation in <see cref="DataSeeder"/>.
    /// </summary>
    public class DataSeederTests
    {
        private static DbField Field(FieldDbType type, string name = "col", int length = 0, int scale = 0)
            => new() { FieldName = name, DbType = type, Length = length, Scale = scale };

        [Fact]
        [DisplayName("CreateValue returns the same value for the same field and row, so two seeds produce the same data")]
        public void CreateValue_IsDeterministic()
        {
            var field = Field(FieldDbType.Guid, "sys_rowid");

            Assert.Equal(DataSeeder.CreateValue(field, 7), DataSeeder.CreateValue(field, 7));
        }

        [Fact]
        [DisplayName("CreateValue produces a different GUID for a different row")]
        public void CreateValue_Guid_DiffersByRow()
        {
            var field = Field(FieldDbType.Guid, "sys_rowid");

            Assert.NotEqual(DataSeeder.CreateValue(field, 1), DataSeeder.CreateValue(field, 2));
        }

        [Fact]
        [DisplayName("CreateValue produces different GUIDs for different columns in the same row")]
        public void CreateValue_Guid_DiffersByColumn()
        {
            Assert.NotEqual(
                DataSeeder.CreateValue(Field(FieldDbType.Guid, "sys_rowid"), 5),
                DataSeeder.CreateValue(Field(FieldDbType.Guid, "owner_rowid"), 5));
        }

        [Fact]
        [DisplayName("CreateValue truncates a string to the declared field length so the write is not rejected")]
        public void CreateValue_String_TruncatesToDeclaredLength()
        {
            var value = Assert.IsType<string>(
                DataSeeder.CreateValue(Field(FieldDbType.String, "customer_name", length: 6), 12345));

            Assert.Equal(6, value.Length);
        }

        [Fact]
        [DisplayName("CreateValue keeps the full string when it fits the declared length")]
        public void CreateValue_String_KeepsShortValue()
        {
            var value = Assert.IsType<string>(
                DataSeeder.CreateValue(Field(FieldDbType.String, "sys_id", length: 50), 3));

            Assert.Equal("sys_id-3", value);
        }

        [Fact]
        [DisplayName("CreateValue does not truncate when Length is 0")]
        public void CreateValue_String_ZeroLengthMeansNoLimit()
        {
            var value = Assert.IsType<string>(
                DataSeeder.CreateValue(Field(FieldDbType.String, "note"), 42));

            Assert.Equal("note-42", value);
        }

        [Theory]
        [InlineData(0, 2)]   // 1.5 rounds away from zero at scale 0.
        [InlineData(2, 1.5)]
        [DisplayName("CreateValue rounds a decimal to the field Scale so the engine does not alter it on write")]
        public void CreateValue_Decimal_RoundsToScale(int scale, double expected)
        {
            var value = Assert.IsType<decimal>(
                DataSeeder.CreateValue(Field(FieldDbType.Decimal, "amount", scale: scale), 1));

            Assert.Equal((decimal)expected, value);
        }

        [Fact]
        [DisplayName("CreateValue alternates Boolean values row by row")]
        public void CreateValue_Boolean_Alternates()
        {
            var field = Field(FieldDbType.Boolean, "is_active");

            Assert.True(Assert.IsType<bool>(DataSeeder.CreateValue(field, 0)));
            Assert.False(Assert.IsType<bool>(DataSeeder.CreateValue(field, 1)));
        }

        [Fact]
        [DisplayName("CreateValue returns a DateTime with UTC Kind, independent of the local time zone")]
        public void CreateValue_DateTime_IsUtc()
        {
            var value = Assert.IsType<DateTime>(
                DataSeeder.CreateValue(Field(FieldDbType.DateTime, "created_at"), 10));

            Assert.Equal(DateTimeKind.Utc, value.Kind);
        }

        [Theory]
        [InlineData(FieldDbType.Short)]
        [InlineData(FieldDbType.Integer)]
        [InlineData(FieldDbType.Long)]
        [DisplayName("CreateValue returns the matching CLR type for each integer type")]
        public void CreateValue_IntegerFamily_ReturnsMatchingClrType(FieldDbType type)
        {
            var value = DataSeeder.CreateValue(Field(type, "n"), 5);

            Assert.Equal(type switch
            {
                FieldDbType.Short => typeof(short),
                FieldDbType.Integer => typeof(int),
                _ => typeof(long)
            }, value.GetType());
        }

        [Fact]
        [DisplayName("CreateValue does not overflow a Short when the row number exceeds its maximum")]
        public void CreateValue_Short_DoesNotOverflow()
        {
            var value = Assert.IsType<short>(
                DataSeeder.CreateValue(Field(FieldDbType.Short, "n"), short.MaxValue + 10));

            Assert.True(value >= 0);
        }

        [Fact]
        [DisplayName("CreateValue returns an empty string for the Unknown type instead of throwing")]
        public void CreateValue_Unknown_ReturnsEmptyString()
        {
            Assert.Equal(string.Empty, DataSeeder.CreateValue(Field(FieldDbType.Unknown, "x"), 1));
        }
    }
}
