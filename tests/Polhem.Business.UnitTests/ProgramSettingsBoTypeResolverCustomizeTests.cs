using System.ComponentModel;
using Polhem.Business.Form;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Microsoft.Extensions.Logging;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Tenant customization overlay tests for <see cref="ProgramSettingsBoTypeResolver"/>: cust has the progId → customized BO,
    /// cust lacks it → base BO; the type cache is isolated by (customizeId, progId); an empty customizeId or no reader → short-circuit to base only.
    /// </summary>
    public class ProgramSettingsBoTypeResolverCustomizeTests
    {
        public class TenantFormBo : FormBusinessObject
        {
            public TenantFormBo(IPolhemContext ctx, Guid accessToken, string progId, bool isLocalCall = true)
                : base(ctx, accessToken, progId, isLocalCall) { }
        }

        private static string TenantFormBoFqn =>
            $"{typeof(TenantFormBo).FullName}, {typeof(TenantFormBo).Assembly.GetName().Name}";

        private static string BaseFormBoFqn =>
            $"{typeof(ProgramSettingsBoTypeResolverTests.TestableCustomFormBo).FullName}, " +
            $"{typeof(ProgramSettingsBoTypeResolverTests.TestableCustomFormBo).Assembly.GetName().Name}";

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
        [DisplayName("Resolve returns the customized BO when cust has the progId (overriding base)")]
        public void Resolve_CustHasProgId_ReturnsCustomizeBo()
        {
            var defineAccess = new ProgramSettingsDefineAccess(BuildSettings(("P001", BaseFormBoFqn)));
            var reader = new SpyCustomizeReader();
            reader.SetProgramSettings("acme", BuildSettings(("P001", TenantFormBoFqn)));
            var resolver = new ProgramSettingsBoTypeResolver(defineAccess, reader);

            var result = resolver.Resolve("acme", "P001");

            Assert.Equal(typeof(TenantFormBo), result);
        }

        [Fact]
        [DisplayName("Resolve falls back to the base BO when cust lacks the progId")]
        public void Resolve_CustMissesProgId_FallsBackToBase()
        {
            var defineAccess = new ProgramSettingsDefineAccess(BuildSettings(("P001", BaseFormBoFqn)));
            var reader = new SpyCustomizeReader();
            // The cust settings override only P999, not P001.
            reader.SetProgramSettings("acme", BuildSettings(("P999", TenantFormBoFqn)));
            var resolver = new ProgramSettingsBoTypeResolver(defineAccess, reader);

            var result = resolver.Resolve("acme", "P001");

            Assert.Equal(typeof(ProgramSettingsBoTypeResolverTests.TestableCustomFormBo), result);
        }

        [Fact]
        [DisplayName("The type cache is isolated by (customizeId, progId), so the same progId resolves independently for different tenants")]
        public void Resolve_DifferentCustomizeIds_IsolatedCache()
        {
            var defineAccess = new ProgramSettingsDefineAccess(BuildSettings(("P001", BaseFormBoFqn)));
            var reader = new SpyCustomizeReader();
            reader.SetProgramSettings("acme", BuildSettings(("P001", TenantFormBoFqn)));
            // globex has no customization.
            var resolver = new ProgramSettingsBoTypeResolver(defineAccess, reader);

            var acme = resolver.Resolve("acme", "P001");
            var globex = resolver.Resolve("globex", "P001");
            var baseOnly = resolver.Resolve("P001");

            Assert.Equal(typeof(TenantFormBo), acme);
            Assert.Equal(typeof(ProgramSettingsBoTypeResolverTests.TestableCustomFormBo), globex);
            Assert.Equal(typeof(ProgramSettingsBoTypeResolverTests.TestableCustomFormBo), baseOnly);
        }

        [Fact]
        [DisplayName("An empty customizeId short-circuits to base only and never calls the reader")]
        public void Resolve_EmptyCustomizeId_ShortCircuits_ReaderNotCalled()
        {
            var defineAccess = new ProgramSettingsDefineAccess(BuildSettings(("P001", BaseFormBoFqn)));
            var reader = new SpyCustomizeReader();
            reader.SetProgramSettings("acme", BuildSettings(("P001", TenantFormBoFqn)));
            var resolver = new ProgramSettingsBoTypeResolver(defineAccess, reader);

            var result = resolver.Resolve("P001");

            Assert.Equal(typeof(ProgramSettingsBoTypeResolverTests.TestableCustomFormBo), result);
            Assert.Equal(0, reader.GetCustomizeProgramSettingsCallCount);
        }

        [Fact]
        [DisplayName("Without an injected reader, Resolve uses base only even with a customizeId (backward compatible)")]
        public void Resolve_NoReader_BehavesAsBase()
        {
            var defineAccess = new ProgramSettingsDefineAccess(BuildSettings(("P001", BaseFormBoFqn)));
            var resolver = new ProgramSettingsBoTypeResolver(defineAccess); // No reader.

            var result = resolver.Resolve("acme", "P001");

            Assert.Equal(typeof(ProgramSettingsBoTypeResolverTests.TestableCustomFormBo), result);
        }

        [Fact]
        [DisplayName("Resolve still returns the customized BO when the base ProgramSettings is missing but cust has the progId")]
        public void Resolve_BaseMissingButCustHasProgId_ReturnsCustomizeBo()
        {
            var defineAccess = new ThrowingProgramSettingsDefineAccess();
            var reader = new SpyCustomizeReader();
            reader.SetProgramSettings("acme", BuildSettings(("P001", TenantFormBoFqn)));
            var resolver = new ProgramSettingsBoTypeResolver(defineAccess, reader);

            var result = resolver.Resolve("acme", "P001");

            Assert.Equal(typeof(TenantFormBo), result);
        }

        // ---- Observability of resolution failures ----

        [Fact]
        [DisplayName("Resolve throws when the customized BO type cannot be loaded, and the message names the customization as the origin")]
        public void Resolve_CustomizeTypeUnloadable_ThrowsWithCustomizeOrigin()
        {
            var reader = new SpyCustomizeReader();
            reader.SetProgramSettings("acme", BuildSettings(("Order", "Acme.Typo.OrderBo, Acme.Typo")));
            var defineAccess = new ProgramSettingsDefineAccess(BuildSettings(("Order", BaseFormBoFqn)));
            var resolver = new ProgramSettingsBoTypeResolver(defineAccess, reader);

            var ex = Assert.Throws<InvalidOperationException>(() => resolver.Resolve("acme", "Order"));

            Assert.Contains("Order", ex.Message, StringComparison.Ordinal);
            Assert.Contains("Acme.Typo.OrderBo, Acme.Typo", ex.Message, StringComparison.Ordinal);
            Assert.Contains("acme", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Resolve throws when the type does not inherit BusinessObject, and the message names the packaged layer as the origin")]
        public void Resolve_TypeNotBusinessObject_ThrowsWithBaseOrigin()
        {
            var defineAccess = new ProgramSettingsDefineAccess(BuildSettings(("Order", NotABusinessObjectFqn)));
            var resolver = new ProgramSettingsBoTypeResolver(defineAccess, null);

            var ex = Assert.Throws<InvalidOperationException>(() => resolver.Resolve("Order"));

            Assert.Contains("base registry", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Failures are not cached, so every repeated call throws instead of passing silently from the second call on")]
        public void Resolve_RepeatedFailure_ThrowsEveryTime()
        {
            var defineAccess = new ProgramSettingsDefineAccess(BuildSettings(("Order", "Nope.OrderBo, Nope")));
            var resolver = new ProgramSettingsBoTypeResolver(defineAccess, null);

            Assert.Throws<InvalidOperationException>(() => resolver.Resolve("Order"));
            Assert.Throws<InvalidOperationException>(() => resolver.Resolve("Order"));
            Assert.Throws<InvalidOperationException>(() => resolver.Resolve("Order"));
        }

        [Fact]
        [DisplayName("The constructor overload that takes a logger still works (the logger is no longer used and receives nothing)")]
        public void Ctor_LoggerOverload_StillResolvesAndLogsNothing()
        {
            var defineAccess = new ProgramSettingsDefineAccess(BuildSettings(("Order", BaseFormBoFqn)));
            var logger = new RecordingLogger();
            var resolver = new ProgramSettingsBoTypeResolver(defineAccess, null, logger);

            var result = resolver.Resolve("Order");

            Assert.Equal(typeof(ProgramSettingsBoTypeResolverTests.TestableCustomFormBo), result);
            Assert.Empty(logger.Entries);
        }

        // ---- Test doubles ----

        /// <summary>A type that does not inherit <see cref="BusinessObject"/>; used to verify the incompatible type failure path.</summary>
        public sealed class NotABusinessObject { }

        private static string NotABusinessObjectFqn =>
            $"{typeof(NotABusinessObject).FullName}, {typeof(NotABusinessObject).Assembly.GetName().Name}";

        private sealed class RecordingLogger : ILogger<ProgramSettingsBoTypeResolver>
        {
            public List<(LogLevel Level, string Message)> Entries { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
                => Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class SpyCustomizeReader : ICustomizeDefineReader
        {
            private readonly Dictionary<string, ProgramSettings> _settings = new(StringComparer.Ordinal);

            public int GetCustomizeProgramSettingsCallCount { get; private set; }

            public void SetProgramSettings(string customizeId, ProgramSettings settings) => _settings[customizeId] = settings;

            public ProgramSettings? GetCustomizeProgramSettings(string customizeId)
            {
                GetCustomizeProgramSettingsCallCount++;
                return _settings.TryGetValue(customizeId, out var s) ? s : null;
            }

            public LanguageResource? GetCustomizeLanguage(string customizeId, string lang, string ns) => null;
            public FormLayout? GetCustomizeFormLayout(string customizeId, string layoutId) => null;
            public MenuSettings? GetCustomizeMenuSettings(string customizeId) => null;
            public PluginSettings? GetCustomizePluginSettings(string customizeId) => null;
        }

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

        private sealed class ThrowingProgramSettingsDefineAccess : IDefineAccess
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
    }
}
