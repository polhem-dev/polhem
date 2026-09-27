using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Providers.PostgreSql;
using Polhem.Definition;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    public class PgFormCommandBuilderTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public PgFormCommandBuilderTests(SharedDbFixture fx) { _fx = fx; }
        private IDefineAccess DefineAccess => _fx.GetRequiredService<IDefineAccess>();

        private PgFormCommandBuilder NewBuilder(FormSchema schema)
            => new(schema, DefineAccess);

        [Fact]
        [DisplayName("The FormSchema constructor throws ArgumentNullException for null")]
        public void Constructor_NullFormSchema_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new PgFormCommandBuilder(null!, DefineAccess));
        }

        [Fact]
        [DisplayName("The FormSchema constructor throws ArgumentNullException for a null IDefineAccess")]
        public void Constructor_NullDefineAccess_Throws()
        {
            var schema = new FormSchema("X", "X");
            Assert.Throws<ArgumentNullException>(() => new PgFormCommandBuilder(schema, null!));
        }


        [Fact]
        [DisplayName("BuildDelete delegates to the PostgreSQL dialect and produces a DELETE statement")]
        public void BuildDelete_DelegatesToPostgreSqlDialect()
        {
            var schema = new FormSchema("X", "X");
            var table = schema.Tables!.Add("Foo", "Foo");
            table.DbTableName = "tb_foo";
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);

            var builder = NewBuilder(schema);
            var spec = builder.BuildDelete("Foo", FilterCondition.Equal(SysFields.RowId, Guid.NewGuid()));

            Assert.Contains("DELETE FROM \"tb_foo\"", spec.CommandText);
        }

        [Fact]
        [DisplayName("BuildSelect delegates to the PostgreSQL dialect and produces a SELECT statement")]
        public void BuildSelect_DelegatesToPostgreSqlDialect()
        {
            var schema = new FormSchema("X", "X");
            var table = schema.Tables!.Add("Foo", "Foo");
            table.DbTableName = "tb_foo";
            table.Fields!.Add(new FormField("name", "Name", FieldDbType.String) { MaxLength = 50 });

            var builder = NewBuilder(schema);
            var spec = builder.BuildSelect("Foo", "");

            Assert.Contains("\"tb_foo\"", spec.CommandText);
        }
    }
}
