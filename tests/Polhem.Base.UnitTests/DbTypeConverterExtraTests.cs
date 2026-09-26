using System.ComponentModel;
using System.Data;
using Polhem.Base.Data;

namespace Polhem.Base.UnitTests
{
    /// <summary>
    /// Additional DbTypeConverter tests: the Nullable/ByRef paths of ToTypeCode and
    /// the full FieldDbType→DbType mapping of ToDbType.
    /// </summary>
    public class DbTypeConverterExtraTests
    {
        [Theory]
        [InlineData(typeof(int), TypeCode.Int32)]
        [InlineData(typeof(string), TypeCode.String)]
        [InlineData(typeof(DateTime), TypeCode.DateTime)]
        [DisplayName("ToTypeCode returns the TypeCode of a non-nullable type")]
        public void ToTypeCode_NonNullable_ReturnsCorrectTypeCode(Type type, TypeCode expected)
        {
            Assert.Equal(expected, DbTypeConverter.ToTypeCode(type));
        }

        [Theory]
        [InlineData(typeof(int?), TypeCode.Int32)]
        [InlineData(typeof(DateTime?), TypeCode.DateTime)]
        [InlineData(typeof(decimal?), TypeCode.Decimal)]
        [DisplayName("ToTypeCode returns the TypeCode of the underlying type for Nullable<T>")]
        public void ToTypeCode_Nullable_UnwrapsAndReturnsInnerTypeCode(Type type, TypeCode expected)
        {
            Assert.Equal(expected, DbTypeConverter.ToTypeCode(type));
        }

        [Fact]
        [DisplayName("ToTypeCode unwraps a ByRef type and returns the element TypeCode")]
        public void ToTypeCode_ByRef_UnwrapsAndReturnsElementTypeCode()
        {
            var byRefType = typeof(int).MakeByRefType();
            Assert.Equal(TypeCode.Int32, DbTypeConverter.ToTypeCode(byRefType));
        }

        [Theory]
        [InlineData(FieldDbType.String, DbType.String)]
        [InlineData(FieldDbType.Text, DbType.String)]
        [InlineData(FieldDbType.Boolean, DbType.Boolean)]
        [InlineData(FieldDbType.AutoIncrement, DbType.Int32)]
        [InlineData(FieldDbType.Integer, DbType.Int32)]
        [InlineData(FieldDbType.Short, DbType.Int16)]
        [InlineData(FieldDbType.Long, DbType.Int64)]
        [InlineData(FieldDbType.Decimal, DbType.Decimal)]
        [InlineData(FieldDbType.Currency, DbType.Currency)]
        [InlineData(FieldDbType.Date, DbType.Date)]
        [InlineData(FieldDbType.DateTime, DbType.DateTime)]
        [InlineData(FieldDbType.Guid, DbType.Guid)]
        [InlineData(FieldDbType.Binary, DbType.Binary)]
        [DisplayName("ToDbType returns the correct DbType")]
        public void ToDbType_AllFieldDbTypes_ReturnsCorrectDbType(FieldDbType fieldDbType, DbType expected)
        {
            Assert.Equal(expected, DbTypeConverter.ToDbType(fieldDbType));
        }

        [Fact]
        [DisplayName("ToDbType throws ArgumentOutOfRangeException for Unknown")]
        public void ToDbType_Unknown_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => DbTypeConverter.ToDbType(FieldDbType.Unknown));
        }
    }
}
