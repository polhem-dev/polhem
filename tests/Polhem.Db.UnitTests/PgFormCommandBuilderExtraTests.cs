using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Providers.PostgreSql;
using Polhem.Definition.Forms;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    public class PgFormCommandBuilderExtraTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public PgFormCommandBuilderExtraTests(SharedDbFixture fx) { _fx = fx; }
        private IDefineAccess DefineAccess => _fx.GetRequiredService<IDefineAccess>();

        private static FormSchema BuildFooSchema()
        {
            var schema = new FormSchema("X", "X");
            var table = schema.Tables!.Add("Foo", "Foo");
            table.DbTableName = "tb_foo";
            table.Fields!.Add(new FormField("name", "Name", FieldDbType.String) { MaxLength = 50 });
            return schema;
        }

        private PgFormCommandBuilder NewBuilder()
            => new(BuildFooSchema(), DefineAccess);

        [Fact]
        [DisplayName("BuildCount delegates to the PostgreSQL dialect and produces a SELECT COUNT(*) statement (double-quoted identifiers)")]
        public void BuildCount_DelegatesToPostgreSqlDialect()
        {
            var builder = NewBuilder();
            var spec = builder.BuildCount("Foo");

            Assert.Contains("COUNT(*)", spec.CommandText);
            Assert.Contains("\"tb_foo\"", spec.CommandText);
        }
    }
}
