using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Providers.Sqlite;
using Polhem.Definition;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    public class SqliteFormCommandBuilderTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public SqliteFormCommandBuilderTests(SharedDbFixture fx) { _fx = fx; }
        private static FormSchema BuildFooSchema()
        {
            var schema = new FormSchema("X", "X");
            var table = schema.Tables!.Add("Foo", "Foo");
            table.DbTableName = "tb_foo";
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            table.Fields!.Add(new FormField("name", "Name", FieldDbType.String) { MaxLength = 50 });
            return schema;
        }

        private SqliteFormCommandBuilder NewBuilder(FormSchema schema)
            => new(schema, _fx.GetRequiredService<IDefineAccess>());

        [Fact]
        [DisplayName("The FormSchema constructor throws ArgumentNullException for null")]
        public void Constructor_NullFormSchema_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SqliteFormCommandBuilder(
                null!, _fx.GetRequiredService<IDefineAccess>()));
        }

        [Fact]
        [DisplayName("The FormSchema constructor throws ArgumentNullException for a null IDefineAccess")]
        public void Constructor_NullDefineAccess_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SqliteFormCommandBuilder(BuildFooSchema(), null!));
        }

        [Fact]
        [DisplayName("BuildSelect delegates to the SQLite dialect and produces a SELECT statement")]
        public void BuildSelect_DelegatesToSqliteDialect()
        {
            var builder = NewBuilder(BuildFooSchema());

            var spec = builder.BuildSelect("Foo", "name", null, null);

            Assert.Contains("\"tb_foo\"", spec.CommandText);
            Assert.Contains("\"name\"", spec.CommandText);
        }


        [Fact]
        [DisplayName("BuildDelete delegates to the SQLite dialect and produces a DELETE statement")]
        public void BuildDelete_DelegatesToSqliteDialect()
        {
            var builder = NewBuilder(BuildFooSchema());
            var spec = builder.BuildDelete("Foo", FilterCondition.Equal(SysFields.RowId, Guid.NewGuid()));

            Assert.Contains("DELETE FROM \"tb_foo\"", spec.CommandText);
        }
    }
}
