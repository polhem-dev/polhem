using System.ComponentModel;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
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
        [InlineData(DefineType.PermissionModels, typeof(PermissionModels))]
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
        [DisplayName("ToClrType throws NotSupportedException for Language, which is not in the mapping dictionary")]
        public void ToClrType_Language_ThrowsNotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => DefineType.Language.ToClrType());
        }
    }
}
