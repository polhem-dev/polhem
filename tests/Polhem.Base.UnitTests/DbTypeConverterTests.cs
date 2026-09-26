using System.ComponentModel;
using Polhem.Base.Data;

namespace Polhem.Base.UnitTests
{
    public class DbTypeConverterTests
    {
        [Theory]
        [InlineData(FieldDbType.String, typeof(string))]
        [InlineData(FieldDbType.Text, typeof(string))]
        [InlineData(FieldDbType.Boolean, typeof(bool))]
        [InlineData(FieldDbType.AutoIncrement, typeof(int))]
        [InlineData(FieldDbType.Short, typeof(short))]
        [InlineData(FieldDbType.Integer, typeof(int))]
        [InlineData(FieldDbType.Long, typeof(long))]
        [InlineData(FieldDbType.Decimal, typeof(decimal))]
        [InlineData(FieldDbType.Currency, typeof(decimal))]
        [InlineData(FieldDbType.Date, typeof(DateTime))]
        [InlineData(FieldDbType.DateTime, typeof(DateTime))]
        [InlineData(FieldDbType.Guid, typeof(Guid))]
        [InlineData(FieldDbType.Binary, typeof(byte[]))]
        [DisplayName("ToType returns the correct CLR type")]
        public void ToType_AllFieldDbTypes_ReturnsCorrectClrType(FieldDbType fieldDbType, Type expectedType)
        {
            var result = DbTypeConverter.ToType(fieldDbType);
            Assert.Equal(expectedType, result);
        }

        [Theory]
        [InlineData(typeof(string), FieldDbType.String)]
        [InlineData(typeof(bool), FieldDbType.Boolean)]
        [InlineData(typeof(short), FieldDbType.Short)]
        [InlineData(typeof(int), FieldDbType.Integer)]
        [InlineData(typeof(long), FieldDbType.Long)]
        [InlineData(typeof(decimal), FieldDbType.Decimal)]
        [InlineData(typeof(DateTime), FieldDbType.DateTime)]
        [InlineData(typeof(Guid), FieldDbType.Guid)]
        [InlineData(typeof(byte[]), FieldDbType.Binary)]
        [DisplayName("ToFieldDbType returns the correct FieldDbType")]
        public void ToFieldDbType_SupportedTypes_ReturnsCorrectFieldDbType(Type clrType, FieldDbType expectedFieldDbType)
        {
            var result = DbTypeConverter.ToFieldDbType(clrType);
            Assert.Equal(expectedFieldDbType, result);
        }

        [Theory]
        [InlineData(typeof(double), FieldDbType.Decimal)]
        [InlineData(typeof(float), FieldDbType.Decimal)]
        [InlineData(typeof(char), FieldDbType.String)]
        [InlineData(typeof(ushort), FieldDbType.Short)]
        [InlineData(typeof(uint), FieldDbType.Integer)]
        [InlineData(typeof(ulong), FieldDbType.Long)]
        [DisplayName("ToFieldDbType maps compatible CLR types correctly")]
        public void ToFieldDbType_CompatibleTypes_ReturnsExpectedFieldDbType(Type clrType, FieldDbType expectedFieldDbType)
        {
            var result = DbTypeConverter.ToFieldDbType(clrType);
            Assert.Equal(expectedFieldDbType, result);
        }

        [Fact]
        [DisplayName("ToFieldDbType throws InvalidOperationException for an unsupported type")]
        public void ToFieldDbType_UnsupportedType_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => DbTypeConverter.ToFieldDbType(typeof(object)));
        }

        [Fact]
        [DisplayName("ToType throws InvalidOperationException for Unknown")]
        public void ToType_Unknown_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => DbTypeConverter.ToType(FieldDbType.Unknown));
        }
    }
}
