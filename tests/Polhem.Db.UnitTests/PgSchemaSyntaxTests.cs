using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Providers.PostgreSql;
using Polhem.Definition.Database;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Pure syntax tests covering how <see cref="PgSchemaSyntax"/> translates a Boolean default across dialects.
    /// The canonical form is "1"/"0", which the PostgreSQL concatenation layer translates to TRUE/FALSE.
    /// </summary>
    public class PgSchemaSyntaxTests
    {
        [Fact]
        [DisplayName("PG GetDefaultExpression returns TRUE for a Boolean with DefaultValue=1")]
        public void GetDefaultExpression_BooleanTrue_ReturnsTrue()
        {
            var field = new DbField("enabled", "Enabled", FieldDbType.Boolean) { DefaultValue = "1" };
            Assert.Equal("TRUE", PgSchemaSyntax.GetDefaultExpression(field));
        }

        [Fact]
        [DisplayName("PG GetDefaultExpression returns FALSE for a Boolean with DefaultValue=0")]
        public void GetDefaultExpression_BooleanFalse_ReturnsFalse()
        {
            var field = new DbField("enabled", "Enabled", FieldDbType.Boolean) { DefaultValue = "0" };
            Assert.Equal("FALSE", PgSchemaSyntax.GetDefaultExpression(field));
        }

        [Fact]
        [DisplayName("PG GetDefaultExpression returns FALSE for a Boolean without a custom default (built-in 0 → FALSE)")]
        public void GetDefaultExpression_BooleanNoCustom_ReturnsFalse()
        {
            var field = new DbField("enabled", "Enabled", FieldDbType.Boolean);
            Assert.Equal("FALSE", PgSchemaSyntax.GetDefaultExpression(field));
        }

        [Fact]
        [DisplayName("PG GetDefaultExpression returns an empty string for an AllowNull Boolean (no DEFAULT clause)")]
        public void GetDefaultExpression_BooleanAllowNull_ReturnsEmpty()
        {
            var field = new DbField("enabled", "Enabled", FieldDbType.Boolean) { AllowNull = true };
            Assert.Equal(string.Empty, PgSchemaSyntax.GetDefaultExpression(field));
        }

        [Fact]
        [DisplayName("PG GetColumnDefinition produces boolean NOT NULL DEFAULT TRUE for a Boolean with DefaultValue=1")]
        public void GetColumnDefinition_BooleanTrue_IncludesDefaultTrue()
        {
            var field = new DbField("enabled", "Enabled", FieldDbType.Boolean) { DefaultValue = "1" };
            var sql = PgSchemaSyntax.GetColumnDefinition(field);
            Assert.Equal("\"enabled\" boolean NOT NULL DEFAULT TRUE", sql);
        }

        [Fact]
        [DisplayName("PG GetColumnDefinition keeps the existing behavior for an Integer DefaultValue (regression guard)")]
        public void GetColumnDefinition_IntegerCustomDefault_RawNumber()
        {
            var field = new DbField("age", "Age", FieldDbType.Integer) { DefaultValue = "42" };
            var sql = PgSchemaSyntax.GetColumnDefinition(field);
            Assert.Equal("\"age\" integer NOT NULL DEFAULT 42", sql);
        }
    }
}
