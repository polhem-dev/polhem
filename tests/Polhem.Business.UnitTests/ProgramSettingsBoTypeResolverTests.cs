using System.ComponentModel;
using Polhem.Business.Form;
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
    /// Unit tests for <see cref="ProgramSettingsBoTypeResolver"/>.
    /// </summary>
    public class ProgramSettingsBoTypeResolverTests
    {
        // Test subclass used to verify the "valid FormBusinessObject derivative" branch.
        // Reachable via its assembly-qualified type name from inside the same test assembly.
        public class TestableCustomFormBo : FormBusinessObject
        {
            public TestableCustomFormBo(IPolhemContext ctx, Guid accessToken, string progId, bool isLocalCall = true)
                : base(ctx, accessToken, progId, isLocalCall) { }
        }

        private static string TestableCustomFormBoFqn =>
            $"{typeof(TestableCustomFormBo).FullName}, {typeof(TestableCustomFormBo).Assembly.GetName().Name}";

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
        [DisplayName("Resolve falls back to FormBusinessObject when ProgramSettings.xml does not exist (FileNotFoundException)")]
        public void Resolve_ProgramSettingsFileMissing_FallsBackToFormBusinessObject()
        {
            var defineAccess = new ThrowingDefineAccess();
            var resolver = new ProgramSettingsBoTypeResolver(defineAccess);

            var result = resolver.Resolve("P001");

            Assert.Equal(typeof(FormBusinessObject), result);
        }

        [Fact]
        [DisplayName("Resolve returns FormBusinessObject when the ProgId is not in ProgramSettings")]
        public void Resolve_ProgIdNotRegistered_ReturnsFormBusinessObject()
        {
            var defineAccess = new ProgramSettingsDefineAccess(new ProgramSettings());
            var resolver = new ProgramSettingsBoTypeResolver(defineAccess);

            var result = resolver.Resolve("UNKNOWN");

            Assert.Equal(typeof(FormBusinessObject), result);
        }

        [Fact]
        [DisplayName("Resolve returns FormBusinessObject when the ProgId exists but BusinessObject is an empty string")]
        public void Resolve_BusinessObjectEmpty_ReturnsFormBusinessObject()
        {
            var defineAccess = new ProgramSettingsDefineAccess(BuildSettings(("P001", null)));
            var resolver = new ProgramSettingsBoTypeResolver(defineAccess);

            var result = resolver.Resolve("P001");

            Assert.Equal(typeof(FormBusinessObject), result);
        }

        [Fact]
        [DisplayName("Resolve throws and names the progId and the type name when BusinessObject points to a type that does not exist")]
        public void Resolve_BusinessObjectUnresolvable_Throws()
        {
            var settings = BuildSettings(("P001", "NonExistent.Bo, NonExistent.Assembly"));
            var defineAccess = new ProgramSettingsDefineAccess(settings);
            var resolver = new ProgramSettingsBoTypeResolver(defineAccess);

            var ex = Assert.Throws<InvalidOperationException>(() => resolver.Resolve("P001"));

            Assert.Contains("P001", ex.Message, StringComparison.Ordinal);
            Assert.Contains("NonExistent.Bo, NonExistent.Assembly", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Resolve throws and names the expected base when BusinessObject points to a type that is not a BusinessObject subclass")]
        public void Resolve_BusinessObjectNotAssignable_Throws()
        {
            // System.Object is a real type but not assignable to BusinessObject.
            var settings = BuildSettings(("P001", "System.Object, System.Private.CoreLib"));
            var defineAccess = new ProgramSettingsDefineAccess(settings);
            var resolver = new ProgramSettingsBoTypeResolver(defineAccess);

            var ex = Assert.Throws<InvalidOperationException>(() => resolver.Resolve("P001"));

            Assert.Contains("P001", ex.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(BusinessObject), ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Resolve returns the type when BusinessObject points to a valid FormBusinessObject subclass")]
        public void Resolve_BusinessObjectValid_ReturnsCustomType()
        {
            var settings = BuildSettings(("P001", TestableCustomFormBoFqn));
            var defineAccess = new ProgramSettingsDefineAccess(settings);
            var resolver = new ProgramSettingsBoTypeResolver(defineAccess);

            var result = resolver.Resolve("P001");

            Assert.Equal(typeof(TestableCustomFormBo), result);
        }

        [Fact]
        [DisplayName("Resolving the same ProgId twice uses the cache instead of reading ProgramSettings again")]
        public void Resolve_SameProgIdTwice_UsesCache()
        {
            var settings = BuildSettings(("P001", TestableCustomFormBoFqn));
            var defineAccess = new ProgramSettingsDefineAccess(settings);
            var resolver = new ProgramSettingsBoTypeResolver(defineAccess);

            var first = resolver.Resolve("P001");

            // Mutate the BusinessObject after first resolve; if cache works the
            // second call must still return the original type.
            settings.Items!["P001"].BusinessObject = "Garbage, Garbage";

            var second = resolver.Resolve("P001");

            Assert.Equal(typeof(TestableCustomFormBo), first);
            Assert.Equal(typeof(TestableCustomFormBo), second);
        }

        [Fact]
        [DisplayName("Replacing the ProgramSettings instance resets the cache and resolves again")]
        public void Resolve_SettingsInstanceReplaced_ResetsCache()
        {
            var first = BuildSettings(("P001", TestableCustomFormBoFqn));
            var defineAccess = new ProgramSettingsDefineAccess(first);
            var resolver = new ProgramSettingsBoTypeResolver(defineAccess);

            var initial = resolver.Resolve("P001");
            Assert.Equal(typeof(TestableCustomFormBo), initial);

            // Simulate a file-watcher reload: hand back a different instance whose
            // P001 now declares no BusinessObject.
            defineAccess.Current = BuildSettings(("P001", null));

            var reloaded = resolver.Resolve("P001");

            Assert.Equal(typeof(FormBusinessObject), reloaded);
        }

        /// <summary>
        /// A test <see cref="IDefineAccess"/> that simulates a missing ProgramSettings.xml
        /// by throwing <see cref="FileNotFoundException"/> from <see cref="GetProgramSettings"/>.
        /// </summary>
        private sealed class ThrowingDefineAccess : IDefineAccess
        {
            public ProgramSettings GetProgramSettings() => throw new FileNotFoundException("ProgramSettings.xml not found");

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

        /// <summary>
        /// A test <see cref="IDefineAccess"/> that lets the caller replace the <see cref="ProgramSettings"/> instance,
        /// to verify the cache reset triggered by reference equality.
        /// </summary>
        private sealed class ProgramSettingsDefineAccess : IDefineAccess
        {
            public ProgramSettings Current { get; set; }

            public ProgramSettingsDefineAccess(ProgramSettings initial) { Current = initial; }

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
