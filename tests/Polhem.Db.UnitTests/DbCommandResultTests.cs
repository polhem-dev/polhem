using System.ComponentModel;
using System.Data;

namespace Polhem.Db.UnitTests
{
    public class DbCommandResultTests
    {
        [Fact]
        [DisplayName("ForRowsAffected returns Kind NonQuery and sets RowsAffected")]
        public void ForRowsAffected_SetsKindAndRows()
        {
            var result = DbCommandResult.ForRowsAffected(7);

            Assert.Equal(DbCommandKind.NonQuery, result.Kind);
            Assert.Equal(7, result.RowsAffected);
            Assert.Null(result.Scalar);
            Assert.Null(result.Table);
        }

        [Fact]
        [DisplayName("ForScalar returns Kind Scalar and keeps the value")]
        public void ForScalar_SetsKindAndScalar()
        {
            var result = DbCommandResult.ForScalar(123);

            Assert.Equal(DbCommandKind.Scalar, result.Kind);
            Assert.Equal(123, result.Scalar);
            Assert.Equal(0, result.RowsAffected);
            Assert.Null(result.Table);
        }

        [Fact]
        [DisplayName("ForScalar with a null value still returns Kind Scalar")]
        public void ForScalar_NullValue_ReturnsScalarKind()
        {
            var result = DbCommandResult.ForScalar(null);

            Assert.Equal(DbCommandKind.Scalar, result.Kind);
            Assert.Null(result.Scalar);
        }

        [Fact]
        [DisplayName("ForTable returns Kind DataTable and keeps the table reference")]
        public void ForTable_SetsKindAndTable()
        {
            var table = new DataTable("demo");
            var result = DbCommandResult.ForTable(table);

            Assert.Equal(DbCommandKind.DataTable, result.Kind);
            Assert.Same(table, result.Table);
            Assert.Equal(0, result.RowsAffected);
            Assert.Null(result.Scalar);
        }
    }
}
