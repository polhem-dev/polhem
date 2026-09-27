using System.ComponentModel;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests
{
    public class DefineTypeExtensionsTests
    {
        [Theory]
        [InlineData(DefineType.SystemSettings, typeof(SystemSettings))]
        [InlineData(DefineType.DatabaseSettings, typeof(DatabaseSettings))]
        [InlineData(DefineType.DbCategorySettings, typeof(DbCategorySettings))]
        [InlineData(DefineType.ProgramSettings, typeof(ProgramSettings))]
        [InlineData(DefineType.TableSchema, typeof(TableSchema))]
        [InlineData(DefineType.FormSchema, typeof(FormSchema))]
        [InlineData(DefineType.FormLayout, typeof(FormLayout))]
        [InlineData(DefineType.Language, typeof(LanguageResource))]
        [InlineData(DefineType.PermissionModels, typeof(PermissionModels))]
        [InlineData(DefineType.CurrencySettings, typeof(CurrencySettings))]
        [InlineData(DefineType.UnitSettings, typeof(UnitSettings))]
        [InlineData(DefineType.MenuSettings, typeof(MenuSettings))]
        [InlineData(DefineType.PluginSettings, typeof(PluginSettings))]
        [DisplayName("ToClrType returns the correct type for a valid define type")]
        public void ToClrType_ValidType_ReturnsExpectedType(DefineType defineType, Type expectedType)
        {
            var result = defineType.ToClrType();

            Assert.Equal(expectedType, result);
        }

        [Fact]
        [DisplayName("ToClrType throws NotSupportedException for an unsupported type")]
        public void ToClrType_UnsupportedType_ThrowsNotSupportedException()
        {
            var invalid = (DefineType)999;

            Assert.Throws<NotSupportedException>(() => invalid.ToClrType());
        }

        [Fact]
        [DisplayName("ToClrType maps every DefineType member to a type in Polhem.Definition")]
        public void ToClrType_EveryDefineType_IsMapped()
        {
            foreach (var defineType in Enum.GetValues<DefineType>())
            {
                var type = defineType.ToClrType();

                Assert.Same(typeof(DefineType).Assembly, type.Assembly);
            }
        }
    }
}
