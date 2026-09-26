using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Providers.Oracle;
using Polhem.Definition;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Pure-syntax tests for <see cref="OracleFormCommandBuilder"/>. Verifies that the
    /// Oracle provider routes through the dialect-agnostic cores in <see cref="Polhem.Db.Dml"/>
    /// with <see cref="Polhem.Definition.Database.DatabaseType.Oracle"/> and emits double-quoted
    /// identifiers + <c>:</c> bind-variable prefixes (per <see cref="DatabaseTypeExtensions.QuoteIdentifier"/>
    /// and <see cref="DatabaseTypeExtensions.GetParameterPrefix"/>).
    /// </summary>
    public class OracleFormCommandBuilderTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public OracleFormCommandBuilderTests(SharedDbFixture fx) { _fx = fx; }
        private IDefineAccess DefineAccess => _fx.GetRequiredService<IDefineAccess>();

        private static FormSchema BuildFooSchema()
        {
            var schema = new FormSchema("X", "X");
            var table = schema.Tables!.Add("Foo", "Foo");
            table.DbTableName = "tb_foo";
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            table.Fields!.AddStringField("name", "Name", 50);
            return schema;
        }

        private OracleFormCommandBuilder NewBuilder()
            => new(BuildFooSchema(), DefineAccess);

        [Fact]
        [DisplayName("The FormSchema constructor throws ArgumentNullException for null")]
        public void Constructor_NullFormSchema_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new OracleFormCommandBuilder(null!, DefineAccess));
        }

        [Fact]
        [DisplayName("The FormSchema constructor throws ArgumentNullException for a null IDefineAccess")]
        public void Constructor_NullDefineAccess_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new OracleFormCommandBuilder(BuildFooSchema(), null!));
        }

        [Fact]
        [DisplayName("BuildSelect delegates to the Oracle dialect and produces a SELECT statement (double-quoted identifiers)")]
        public void BuildSelect_DelegatesToOracleDialect()
        {
            var builder = NewBuilder();

            var spec = builder.BuildSelect("Foo", "name", null, null);

            Assert.Contains("\"TB_FOO\"", spec.CommandText);
            Assert.Contains("\"NAME\"", spec.CommandText);
        }


        [Fact]
        [DisplayName("BuildDelete delegates to the Oracle dialect and produces a DELETE statement")]
        public void BuildDelete_DelegatesToOracleDialect()
        {
            var builder = NewBuilder();
            var spec = builder.BuildDelete("Foo", FilterCondition.Equal(SysFields.RowId, Guid.NewGuid()));

            Assert.Contains("DELETE FROM \"TB_FOO\"", spec.CommandText);
        }

        [Fact]
        [DisplayName("BuildCount delegates to the Oracle dialect and produces a SELECT COUNT(*) statement (double-quoted identifiers)")]
        public void BuildCount_DelegatesToOracleDialect()
        {
            var builder = NewBuilder();
            var spec = builder.BuildCount("Foo");

            Assert.Contains("COUNT", spec.CommandText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"TB_FOO\"", spec.CommandText);
        }
    }
}
