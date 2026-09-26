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
        [DisplayName("FormSchema 建構子 null 應擲 ArgumentNullException")]
        public void Constructor_NullFormSchema_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new PgFormCommandBuilder(null!, DefineAccess));
        }

        [Fact]
        [DisplayName("FormSchema 建構子 null IDefineAccess 應擲 ArgumentNullException")]
        public void Constructor_NullDefineAccess_Throws()
        {
            var schema = new FormSchema("X", "X");
            Assert.Throws<ArgumentNullException>(() => new PgFormCommandBuilder(schema, null!));
        }


        [Fact]
        [DisplayName("BuildDelete 應委派至 PostgreSQL 方言並產生 DELETE 語句")]
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
        [DisplayName("BuildSelect 應委派至 PostgreSQL 方言並產生 SELECT 語句")]
        public void BuildSelect_DelegatesToPostgreSqlDialect()
        {
            var schema = new FormSchema("X", "X");
            var table = schema.Tables!.Add("Foo", "Foo");
            table.DbTableName = "tb_foo";
            table.Fields!.AddStringField("name", "Name", 50);

            var builder = NewBuilder(schema);
            var spec = builder.BuildSelect("Foo", "");

            Assert.Contains("\"tb_foo\"", spec.CommandText);
        }
    }
}
