using System.ComponentModel;
using System.Data;

namespace Polhem.Db.UnitTests
{
    public class DbTypeMapperTests
    {
        // The test data covers many CLR types (string/int/DateTime/Guid/byte[] and more), so the TheoryData can only
        // use object as its first type argument. Warning xUnit1045 does not apply to this deliberate design.
#pragma warning disable xUnit1045 // Avoid using TheoryData type arguments that might not be serializable
        public static TheoryData<object, DbType> Infer_Inputs() => new()
        {
            { "abc", DbType.String },
            { 1, DbType.Int32 },
            { (long)1, DbType.Int64 },
            { (short)1, DbType.Int16 },
            { (byte)1, DbType.Byte },
            { true, DbType.Boolean },
            { new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), DbType.DateTime },
            { 1.5m, DbType.Decimal },
            { 1.5d, DbType.Double },
            { 1.5f, DbType.Single },
            { Guid.NewGuid(), DbType.Guid },
            { new byte[] { 1, 2 }, DbType.Binary },
            { TimeSpan.FromSeconds(1), DbType.Time },
        };

        [Theory]
        [MemberData(nameof(Infer_Inputs))]
        [DisplayName("Infer returns the DbType matching the type of the value")]
        public void Infer_KnownTypes_ReturnsExpected(object value, DbType expected)
        {
            var result = DbTypeMapper.Infer(value);
            Assert.Equal(expected, result);
        }
#pragma warning restore xUnit1045

        [Fact]
        [DisplayName("Infer returns null for a null value")]
        public void Infer_Null_ReturnsNull()
        {
            Assert.Null(DbTypeMapper.Infer(null!));
        }

        [Fact]
        [DisplayName("Infer returns null for DBNull")]
        public void Infer_DBNull_ReturnsNull()
        {
            Assert.Null(DbTypeMapper.Infer(DBNull.Value));
        }

        [Fact]
        [DisplayName("Infer returns null for an unsupported type")]
        public void Infer_UnsupportedType_ReturnsNull()
        {
            Assert.Null(DbTypeMapper.Infer(new object()));
        }
    }
}
