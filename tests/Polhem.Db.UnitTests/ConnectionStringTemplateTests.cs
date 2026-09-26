using System.ComponentModel;
using System.Data.Common;
using Polhem.Db.Manager;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// <see cref="ConnectionStringTemplate"/> resolves the <c>{@DbName}</c> / <c>{@UserId}</c> / <c>{@Password}</c>
    /// placeholders without letting a value break out of its key/value pair.
    /// </summary>
    /// <remarks>
    /// The placeholders used to be filled by plain text replacement, so a password containing <c>;</c> ended its pair
    /// and the rest was parsed as further connection options.
    /// </remarks>
    public class ConnectionStringTemplateTests
    {
        private const string Template = "Server=db.local;Database={@DbName};User ID={@UserId};Password={@Password}";

        [Fact]
        [DisplayName("A password containing ; and = stays one value and adds no connection option")]
        public void Resolve_PasswordWithSeparators_StaysOneValue()
        {
            const string password = "p;Integrated Security=true;x='\"";

            var resolved = ConnectionStringTemplate.Resolve(Template, "erp", "app", password);

            var parsed = new DbConnectionStringBuilder { ConnectionString = resolved };
            Assert.Equal(password, parsed["Password"]);
            Assert.False(parsed.ContainsKey("Integrated Security"));
            Assert.Equal("erp", parsed["Database"]);
            Assert.Equal("app", parsed["User ID"]);
        }

        [Fact]
        [DisplayName("A placeholder inside a longer value is replaced in place")]
        public void Resolve_PlaceholderInsideValue_IsReplaced()
        {
            var resolved = ConnectionStringTemplate.Resolve(
                "Data Source=file:app_{@DbName}?mode=memory&cache=shared", "company", string.Empty, string.Empty);

            var parsed = new DbConnectionStringBuilder { ConnectionString = resolved };
            Assert.Equal("file:app_company?mode=memory&cache=shared", parsed["Data Source"]);
        }

        [Fact]
        [DisplayName("Placeholders match regardless of letter case")]
        public void Resolve_PlaceholderInOtherCase_IsReplaced()
        {
            var resolved = ConnectionStringTemplate.Resolve("Database={@dbname}", "erp", string.Empty, string.Empty);

            var parsed = new DbConnectionStringBuilder { ConnectionString = resolved };
            Assert.Equal("erp", parsed["Database"]);
        }

        [Fact]
        [DisplayName("An empty value leaves its placeholder in place")]
        public void Resolve_EmptyValue_LeavesPlaceholder()
        {
            var resolved = ConnectionStringTemplate.Resolve(Template, "erp", "app", string.Empty);

            var parsed = new DbConnectionStringBuilder { ConnectionString = resolved };
            Assert.Equal("{@Password}", parsed["Password"]);
        }

        [Fact]
        [DisplayName("A template without placeholders is returned unchanged")]
        public void Resolve_NoPlaceholder_ReturnsTemplate()
        {
            const string template = "Data Source=localhost:1521/FREEPDB1;User Id=app;Password=secret;";

            Assert.Same(template, ConnectionStringTemplate.Resolve(template, "erp", "other", "other"));
        }
    }
}
