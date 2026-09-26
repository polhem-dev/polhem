namespace Polhem.Definition
{
    /// <summary>
    /// Extension methods for <see cref="DefineType"/>.
    /// </summary>
    public static class DefineTypeExtensions
    {
        private static readonly Dictionary<DefineType, string> s_defineTypeNames = new()
        {
            { DefineType.SystemSettings,   "Polhem.Definition.Settings.SystemSettings" },
            { DefineType.DatabaseSettings, "Polhem.Definition.Settings.DatabaseSettings" },
            { DefineType.DbCategorySettings, "Polhem.Definition.Settings.DbCategorySettings" },
            { DefineType.ProgramSettings,  "Polhem.Definition.Settings.ProgramSettings" },
            { DefineType.MenuSettings,     "Polhem.Definition.Settings.MenuSettings" },
            { DefineType.PluginSettings,   "Polhem.Definition.Settings.PluginSettings" },
            { DefineType.TableSchema,      "Polhem.Definition.Database.TableSchema" },
            { DefineType.FormSchema,       "Polhem.Definition.Forms.FormSchema" },
            { DefineType.FormLayout,       "Polhem.Definition.Layouts.FormLayout" },
            { DefineType.PermissionModels, "Polhem.Definition.Settings.PermissionModels" },
            { DefineType.CurrencySettings, "Polhem.Definition.Settings.CurrencySettings" },
            { DefineType.UnitSettings,     "Polhem.Definition.Settings.UnitSettings" },
        };

        /// <summary>
        /// Gets the CLR type for the specified define type.
        /// </summary>
        /// <param name="defineType">The define data type.</param>
        /// <exception cref="NotSupportedException">Thrown when the define type is not registered.</exception>
        public static Type ToClrType(this DefineType defineType)
        {
            if (!s_defineTypeNames.TryGetValue(defineType, out string? typeName))
                throw new NotSupportedException($"Type not found: {defineType}");
            var assembly = typeof(DefineTypeExtensions).Assembly;
            var type = assembly.GetType(typeName);
            if (type == null)
                throw new NotSupportedException($"Type not found: {typeName}");
            return type;
        }
    }
}
