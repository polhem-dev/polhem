using System.ComponentModel;
using Polhem.Definition.Database;
using Polhem.Definition.Settings;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The placeholder checks of the DatabaseSettings editor.
    /// </summary>
    public class DatabaseSettingsValidatorTests
    {
        private const string Template =
            "Data Source=localhost;Initial Catalog={@DbName};User ID={@UserId};Password={@Password}";

        private static DatabaseSettings Settings(string itemDbName)
        {
            var settings = new DatabaseSettings();
            settings.Servers!.Add(new DatabaseServer
            {
                Id = "sql", DatabaseType = DatabaseType.SQLServer, ConnectionString = Template, UserId = "sa", Password = "x",
            });
            settings.Items!.Add(new DatabaseItem
            {
                Id = "common", CategoryId = "common", DatabaseType = DatabaseType.SQLServer, ServerId = "sql", DbName = itemDbName,
            });
            return settings;
        }

        [Fact]
        [DisplayName("A server whose template names {@DbName} is not warned: its items fill the name")]
        public void Validate_ServerTemplateWithDbName_NoWarning()
        {
            Assert.Empty(DatabaseSettingsValidator.Validate(Settings("demo_common")));
        }

        [Fact]
        [DisplayName("An item with its own {@DbName} connection string and no DbName is still warned")]
        public void Validate_ItemWithoutDbName_Warns()
        {
            var settings = Settings("demo_common");
            settings.Items!["common"].ConnectionString = Template;
            settings.Items["common"].DbName = string.Empty;
            settings.Items["common"].UserId = "sa";
            settings.Items["common"].Password = "x";

            var issue = Assert.Single(DatabaseSettingsValidator.Validate(settings));
            Assert.Equal("Items.common", issue.Path);
            Assert.Contains("{@DbName}", issue.Message);
        }
    }
}
