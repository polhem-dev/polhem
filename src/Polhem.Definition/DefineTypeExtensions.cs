using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;

namespace Polhem.Definition
{
    /// <summary>
    /// Extension methods for <see cref="DefineType"/>.
    /// </summary>
    public static class DefineTypeExtensions
    {
        /// <summary>
        /// Gets the CLR type for the specified define type.
        /// </summary>
        /// <param name="defineType">The define data type.</param>
        /// <exception cref="NotSupportedException">Thrown when the value is not a member of <see cref="DefineType"/>.</exception>
        public static Type ToClrType(this DefineType defineType) => defineType switch
        {
            DefineType.SystemSettings => typeof(SystemSettings),
            DefineType.DatabaseSettings => typeof(DatabaseSettings),
            DefineType.DbCategorySettings => typeof(DbCategorySettings),
            DefineType.ProgramSettings => typeof(ProgramSettings),
            DefineType.TableSchema => typeof(TableSchema),
            DefineType.FormSchema => typeof(FormSchema),
            DefineType.FormLayout => typeof(FormLayout),
            DefineType.Language => typeof(LanguageResource),
            DefineType.PermissionModels => typeof(PermissionModels),
            DefineType.CurrencySettings => typeof(CurrencySettings),
            DefineType.UnitSettings => typeof(UnitSettings),
            DefineType.MenuSettings => typeof(MenuSettings),
            DefineType.PluginSettings => typeof(PluginSettings),
            // NOTE: A new `DefineType` member without an arm above falls through to this throw. The test
            // `ToClrType_EveryDefineType_IsMapped` in Polhem.Definition.UnitTests fails when that happens.
            _ => throw new NotSupportedException($"Type not found: {defineType}")
        };
    }
}
