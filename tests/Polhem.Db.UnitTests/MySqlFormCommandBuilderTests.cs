using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Providers.MySql;
using Polhem.Definition;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Pure-syntax tests for <see cref="MySqlFormCommandBuilder"/>. Verifies that the
    /// MySQL provider routes through the dialect-agnostic cores in <see cref="Polhem.Db.Dml"/>
    /// with <see cref="Polhem.Definition.Database.DatabaseType.MySQL"/> and emits backtick-quoted
    /// identifiers (per <see cref="DatabaseTypeExtensions.QuoteIdentifier"/>).
    /// </summary>
    public class MySqlFormCommandBuilderTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public MySqlFormCommandBuilderTests(SharedDbFixture fx) { _fx = fx; }
        private IDefineAccess DefineAccess => _fx.GetRequiredService<IDefineAccess>();

        private static FormSchema BuildFooSchema()
        {
            var schema = new FormSchema("X", "X");
            var table = schema.Tables!.Add("Foo", "Foo");
            table.DbTableName = "tb_foo";
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            table.Fields!.Add(new FormField("name", "Name", FieldDbType.String) { MaxLength = 50 });
            return schema;
        }

        private MySqlFormCommandBuilder NewBuilder()
            => new(BuildFooSchema(), DefineAccess);

        [Fact]
        [DisplayName("The FormSchema constructor throws ArgumentNullException for null")]
        public void Constructor_NullFormSchema_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MySqlFormCommandBuilder(null!, DefineAccess));
        }

        [Fact]
        [DisplayName("The FormSchema constructor throws ArgumentNullException for a null IDefineAccess")]
        public void Constructor_NullDefineAccess_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MySqlFormCommandBuilder(BuildFooSchema(), null!));
        }

        [Fact]
        [DisplayName("BuildSelect delegates to the MySQL dialect and produces a SELECT statement (backtick identifiers)")]
        public void BuildSelect_DelegatesToMySqlDialect()
        {
            var builder = NewBuilder();

            var spec = builder.BuildSelect("Foo", "name", null, null);

            Assert.Contains("`tb_foo`", spec.CommandText);
            Assert.Contains("`name`", spec.CommandText);
        }


        [Fact]
        [DisplayName("BuildDelete delegates to the MySQL dialect and produces a DELETE statement")]
        public void BuildDelete_DelegatesToMySqlDialect()
        {
            var builder = NewBuilder();
            var spec = builder.BuildDelete("Foo", FilterCondition.Equal(SysFields.RowId, Guid.NewGuid()));

            Assert.Contains("DELETE FROM `tb_foo`", spec.CommandText);
        }

        [Fact]
        [DisplayName("BuildCount delegates to the MySQL dialect and produces a SELECT COUNT(*) statement (backtick identifiers)")]
        public void BuildCount_DelegatesToMySqlDialect()
        {
            var builder = NewBuilder();
            var spec = builder.BuildCount("Foo");

            Assert.Contains("COUNT(*)", spec.CommandText);
            Assert.Contains("`tb_foo`", spec.CommandText);
        }
    }
}
