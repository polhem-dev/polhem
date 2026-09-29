using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Db.Providers.SqlServer;
using Polhem.Definition;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    public class SqlFormCommandBuilderTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public SqlFormCommandBuilderTests(SharedDbFixture fx) { _fx = fx; }
        private IDefineAccess DefineAccess => _fx.GetRequiredService<IDefineAccess>();

        private SqlFormCommandBuilder NewBuilder(FormSchema schema)
            => new(schema, DefineAccess);

        [Fact]
        [DisplayName("The FormSchema constructor throws ArgumentNullException for null")]
        public void Constructor_NullFormSchema_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SqlFormCommandBuilder(null!, DefineAccess));
        }

        [Fact]
        [DisplayName("The FormSchema constructor throws ArgumentNullException for a null IDefineAccess")]
        public void Constructor_NullDefineAccess_Throws()
        {
            var schema = new FormSchema("X", "X");
            Assert.Throws<ArgumentNullException>(() => new SqlFormCommandBuilder(schema, null!));
        }


        [Fact]
        [DisplayName("BuildDelete delegates to the SQL Server dialect and produces a DELETE statement")]
        public void BuildDelete_DelegatesToSqlServerDialect()
        {
            var schema = new FormSchema("X", "X");
            var table = schema.Tables!.Add("Foo", "Foo");
            table.DbTableName = "tb_foo";
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);

            var builder = NewBuilder(schema);
            var spec = builder.BuildDelete("Foo", FilterCondition.Equal(SysFields.RowId, Guid.NewGuid()));

            Assert.Contains("DELETE FROM [tb_foo]", spec.CommandText);
        }
    }
}
