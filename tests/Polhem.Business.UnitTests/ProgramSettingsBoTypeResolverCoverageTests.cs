using System.ComponentModel;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Gap-filling coverage for <see cref="ProgramSettingsBoTypeResolver"/>: the constructor null guard,
    /// the fallback when the assembly loads but the type does not exist (GetType returns null), a cache reset when the customized ProgramSettings reference changes,
    /// and FindItem searching across several categories.
    /// </summary>
    public class ProgramSettingsBoTypeResolverCoverageTests
    {
        private static string BaseFormBoFqn =>
            $"{typeof(ProgramSettingsBoTypeResolverTests.TestableCustomFormBo).FullName}, " +
            $"{typeof(ProgramSettingsBoTypeResolverTests.TestableCustomFormBo).Assembly.GetName().Name}";

        private static string TenantFormBoFqn =>
            $"{typeof(ProgramSettingsBoTypeResolverCustomizeTests.TenantFormBo).FullName}, " +
            $"{typeof(ProgramSettingsBoTypeResolverCustomizeTests.TenantFormBo).Assembly.GetName().Name}";

        private static ProgramSettings BuildSettings(params (string progId, string? businessObject)[] items)
        {
            var settings = new ProgramSettings();
            foreach (var (progId, businessObject) in items)
            {
                var item = settings.Items!.Add(progId, progId);
                if (businessObject != null) item.BusinessObject = businessObject;
            }
            return settings;
        }


        [Fact]
        [DisplayName("The constructor throws ArgumentNullException for a null defineAccess")]
        public void Ctor_NullDefineAccess_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ProgramSettingsBoTypeResolver(null!));
        }

        [Fact]
        [DisplayName("Resolve throws when the BusinessObject type name's assembly loads but the type does not exist (GetType returns null)")]
        public void Resolve_TypeNameLoadableAssemblyButMissingType_Throws()
        {
            var settings = BuildSettings(("P001", "Polhem.Business.NoSuchTypeXyz, Polhem.Business"));
            var defineAccess = new MutableDefineAccess(settings);
            var resolver = new ProgramSettingsBoTypeResolver(defineAccess);

            var ex = Assert.Throws<InvalidOperationException>(() => resolver.Resolve("P001"));

            Assert.Contains("NoSuchTypeXyz", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Replacing the customized ProgramSettings instance (a different reference) resets the cache and resolves again")]
        public void Resolve_CustSettingsInstanceReplaced_ResetsCache()
        {
            var defineAccess = new MutableDefineAccess(BuildSettings(("P001", BaseFormBoFqn)));
            var reader = new MutableCustomizeReader();
            reader.Set("acme", BuildSettings(("P001", TenantFormBoFqn)));
            var resolver = new ProgramSettingsBoTypeResolver(defineAccess, reader);

            var first = resolver.Resolve("acme", "P001");
            Assert.Equal(typeof(ProgramSettingsBoTypeResolverCustomizeTests.TenantFormBo), first);

            // File-watcher reload: hand back a new instance that no longer carries P001 at all.
            reader.Set("acme", BuildSettings(("P999", null)));

            var second = resolver.Resolve("acme", "P001");

            // Cust no longer overrides P001 → falls through to the base entry's BO.
            Assert.Equal(typeof(ProgramSettingsBoTypeResolverTests.TestableCustomFormBo), second);
        }

        [Fact]
        [DisplayName("With several registry entries, Resolve uses progId as the key to find the right one")]
        public void Resolve_ProgIdAmongSeveral_Resolves()
        {
            var defineAccess = new MutableDefineAccess(
                BuildSettings(("P001", null), ("P002", TenantFormBoFqn), ("P003", null)));
            var resolver = new ProgramSettingsBoTypeResolver(defineAccess);

            var result = resolver.Resolve("P002");

            Assert.Equal(typeof(ProgramSettingsBoTypeResolverCustomizeTests.TenantFormBo), result);
        }

        // ---- Test doubles ----

        private sealed class MutableCustomizeReader : ICustomizeDefineReader
        {
            private readonly Dictionary<string, ProgramSettings> _settings = new(StringComparer.Ordinal);

            public void Set(string customizeId, ProgramSettings settings) => _settings[customizeId] = settings;

            public ProgramSettings? GetCustomizeProgramSettings(string customizeId)
                => _settings.TryGetValue(customizeId, out var s) ? s : null;

            public LanguageResource? GetCustomizeLanguage(string customizeId, string lang, string ns) => null;
            public FormLayout? GetCustomizeFormLayout(string customizeId, string layoutId) => null;
            public MenuSettings? GetCustomizeMenuSettings(string customizeId) => null;
            public PluginSettings? GetCustomizePluginSettings(string customizeId) => null;
        }

        private sealed class MutableDefineAccess : IDefineAccess
        {
            public ProgramSettings Current { get; set; }
            public MutableDefineAccess(ProgramSettings initial) { Current = initial; }
            public ProgramSettings GetProgramSettings() => Current;

            public object GetDefine(DefineType defineType, string[]? keys = null) => throw new NotImplementedException();
            public void SaveDefine(DefineType defineType, object defineObject, string[]? keys = null) => throw new NotImplementedException();
            public SystemSettings GetSystemSettings() => throw new NotImplementedException();
            public void SaveSystemSettings(SystemSettings settings) => throw new NotImplementedException();
            public DatabaseSettings GetDatabaseSettings() => throw new NotImplementedException();
            public void SaveDatabaseSettings(DatabaseSettings settings) => throw new NotImplementedException();
            public void SaveProgramSettings(ProgramSettings settings) => throw new NotImplementedException();
            public DbCategorySettings GetDbCategorySettings() => throw new NotImplementedException();
            public void SaveDbCategorySettings(DbCategorySettings settings) => throw new NotImplementedException();
            public TableSchema GetTableSchema(string categoryId, string tableName) => throw new NotImplementedException();
            public void SaveTableSchema(string categoryId, TableSchema tableSchema) => throw new NotImplementedException();
            public FormSchema GetFormSchema(string progId) => throw new NotImplementedException();
            public void SaveFormSchema(FormSchema formSchema) => throw new NotImplementedException();
            public FormLayout GetFormLayout(string layoutId) => throw new NotImplementedException();
            public void SaveFormLayout(FormLayout formLayout) => throw new NotImplementedException();
            public LanguageResource GetLanguage(string lang, string ns) => throw new NotImplementedException();
            public void SaveLanguage(LanguageResource resource) => throw new NotImplementedException();
        }
    }
}
