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
    /// <see cref="PluginSettingsResolver"/>: the packaged and customized layers are added together, every failure throws,
    /// and the chain cache is rebuilt after the definition reloads.
    /// </summary>
    public class PluginSettingsResolverTests
    {
        private static string SamplePluginFqn =>
            $"{typeof(SamplePlugin).FullName}, {typeof(SamplePlugin).Assembly.GetName().Name}";

        private static string OtherPluginFqn =>
            $"{typeof(OtherPlugin).FullName}, {typeof(OtherPlugin).Assembly.GetName().Name}";

        private static string NotAPluginFqn =>
            $"{typeof(NotAPlugin).FullName}, {typeof(NotAPlugin).Assembly.GetName().Name}";

        private static PluginSettings Build(string progId, params (string Type, PluginStage Stage)[] plugins)
        {
            var settings = new PluginSettings();
            var program = settings.Items!.Add(progId);
            foreach (var (type, stage) in plugins)
                program.Plugins!.Add(type, stage);
            return settings;
        }

        /// <summary><see cref="SamplePlugin"/> overrides BeforeSave, so the declaration matches the override.</summary>
        private static (string, PluginStage) Sample => (SamplePluginFqn, PluginStage.BeforeSave);

        /// <summary><see cref="OtherPlugin"/> overrides AfterSave, so the declaration matches the override.</summary>
        private static (string, PluginStage) Other => (OtherPluginFqn, PluginStage.AfterSave);

        [Fact]
        [DisplayName("The packaged layer's chain resolves to the matching type")]
        public void Resolve_BaseOnly_ReturnsBaseChain()
        {
            var access = new StubDefineAccess(Build("Order", Sample));
            var resolver = new PluginSettingsResolver(access);

            Assert.Equal([typeof(SamplePlugin)], resolver.Resolve("", "Order").Types);
        }

        [Fact]
        [DisplayName("Both layers are added together, packaged first and customized after")]
        public void Resolve_BothLayers_ConcatenatesBaseThenCustomize()
        {
            var access = new StubDefineAccess(Build("Order", Sample));
            var reader = new StubCustomizeReader { Settings = Build("Order", Other) };
            var resolver = new PluginSettingsResolver(access, reader);

            Assert.Equal([typeof(SamplePlugin), typeof(OtherPlugin)],
                resolver.Resolve("acme", "Order").Types);
        }

        [Fact]
        [DisplayName("A progId with no plugin bound returns an empty chain")]
        public void Resolve_NoBinding_ReturnsEmptyChain()
        {
            var resolver = new PluginSettingsResolver(new StubDefineAccess(new PluginSettings()));

            Assert.True(resolver.Resolve("", "Order").IsEmpty);
        }

        [Fact]
        [DisplayName("With no definition file in either layer the chain is empty, not null (the state of most deployments)")]
        public void Resolve_NeitherLayerHasSettings_ReturnsEmptyChainNotNull()
        {
            // The base file is missing (storage throws `FileNotFoundException`) and the customization is missing (the reader returns null).
            var access = new StubDefineAccess(null);
            var reader = new StubCustomizeReader { Settings = null };
            var resolver = new PluginSettingsResolver(access, reader);

            var chain = resolver.Resolve("acme", "Order");

            Assert.NotNull(chain);
            Assert.True(chain.IsEmpty);
            Assert.Empty(chain.Types);
            Assert.False(chain.HasStage(PluginStage.BeforeSave));
        }

        [Fact]
        [DisplayName("With no definition file in either layer the runner can still be created and every stage is a no-op")]
        public void Resolve_NeitherLayerHasSettings_RunnerIsUsableNoOp()
        {
            var resolver = new PluginSettingsResolver(new StubDefineAccess(null), new StubCustomizeReader());

            var runner = resolver.Resolve("acme", "Order").CreateRunner(new StubBusinessObjectContext(), Guid.NewGuid(), "Order");

            // Nothing throws and nothing is constructed, so `FormBusinessObject` can call it unconditionally.
            var exception = Record.Exception(() =>
            {
                runner.RunBeforeSave(null!);
                runner.RunAfterSave(null!);
                runner.RunBeforeDelete(null!);
                runner.RunAfterDelete(null!);
            });
            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("A missing packaged definition is not an error, and the customization still resolves")]
        public void Resolve_BaseMissing_StillResolvesCustomize()
        {
            var access = new StubDefineAccess(null);
            var reader = new StubCustomizeReader { Settings = Build("Order", Sample) };
            var resolver = new PluginSettingsResolver(access, reader);

            Assert.Equal([typeof(SamplePlugin)], resolver.Resolve("acme", "Order").Types);
        }

        [Fact]
        [DisplayName("A type that cannot be loaded throws (a plugin is added on purpose, and skipping it silently means the customization has no effect)")]
        public void Resolve_UnloadableType_Throws()
        {
            var access = new StubDefineAccess(Build("Order", ("Nope.Missing, Nope", PluginStage.BeforeSave)));
            var resolver = new PluginSettingsResolver(access);

            var ex = Assert.Throws<InvalidOperationException>(() => resolver.Resolve("", "Order"));
            Assert.Contains("Nope.Missing, Nope", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A type that does not inherit FormBusinessPlugin throws")]
        public void Resolve_TypeNotAPlugin_Throws()
        {
            var access = new StubDefineAccess(Build("Order", (NotAPluginFqn, PluginStage.BeforeSave)));
            var resolver = new PluginSettingsResolver(access);

            var ex = Assert.Throws<InvalidOperationException>(() => resolver.Resolve("", "Order"));
            Assert.Contains(nameof(FormBusinessPlugin), ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A declared stage that does not match the class's override throws (hand-written files have no maintenance API guarding them, so this is the only gate)")]
        public void Resolve_DeclaredStageDisagreesWithOverride_Throws()
        {
            var access = new StubDefineAccess(Build("Order", (SamplePluginFqn, PluginStage.AfterDelete)));
            var resolver = new PluginSettingsResolver(access);

            var ex = Assert.Throws<InvalidOperationException>(() => resolver.Resolve("", "Order"));
            Assert.Contains("overrides BeforeSave", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A hand-written file missing the Stage attribute throws, with a message saying the stage was not declared")]
        public void Resolve_NoStageDeclared_Throws()
        {
            var access = new StubDefineAccess(Build("Order", (SamplePluginFqn, PluginStage.None)));
            var resolver = new PluginSettingsResolver(access);

            var ex = Assert.Throws<InvalidOperationException>(() => resolver.Resolve("", "Order"));
            Assert.Contains("with no Stage", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The cache is isolated by (customizeId, progId), so tenants do not affect each other")]
        public void Resolve_DifferentCustomizeIds_Isolated()
        {
            var access = new StubDefineAccess(Build("Order", Sample));
            var reader = new StubCustomizeReader { Settings = Build("Order", Other) };
            var resolver = new PluginSettingsResolver(access, reader);

            var acme = resolver.Resolve("acme", "Order");
            reader.Settings = null;   // globex has no customization file.
            var globex = resolver.Resolve("globex", "Order");

            Assert.Equal([typeof(SamplePlugin), typeof(OtherPlugin)], acme.Types);
            Assert.Equal([typeof(SamplePlugin)], globex.Types);
        }

        [Fact]
        [DisplayName("A chain built from settings that were reloaded before it was cached is not served for the reloaded settings")]
        public void Resolve_ReloadBeforeCacheWrite_DoesNotPinOldChain()
        {
            var access = new StubDefineAccess(Build("Order", Sample));
            var resolver = new PluginSettingsResolver(access);
            bool reloaded = false;
            resolver.BeforeCacheWrite = () =>
            {
                if (reloaded) { return; }
                reloaded = true;
                access.Current = Build("Order", Sample, Other);
                resolver.Resolve("", "Invoice");
            };

            _ = resolver.Resolve("", "Order");
            resolver.BeforeCacheWrite = null;

            Assert.Equal([typeof(SamplePlugin), typeof(OtherPlugin)], resolver.Resolve("", "Order").Types);
        }

        [Fact]
        [DisplayName("The chain cache is rebuilt after the definition instance changes (a file-watcher reload)")]
        public void Resolve_SettingsInstanceChanged_RebuildsChain()
        {
            var access = new StubDefineAccess(Build("Order", Sample));
            var resolver = new PluginSettingsResolver(access);
            Assert.Equal([typeof(SamplePlugin)], resolver.Resolve("", "Order").Types);

            access.Current = Build("Order", Sample, Other);

            Assert.Equal([typeof(SamplePlugin), typeof(OtherPlugin)], resolver.Resolve("", "Order").Types);
        }

        // ---- Test doubles ----

        public sealed class SamplePlugin : FormBusinessPlugin
        {
            public SamplePlugin(IBusinessObjectContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId) { }

            public override void BeforeSave(SaveContext context) { }
        }

        public sealed class OtherPlugin : FormBusinessPlugin
        {
            public OtherPlugin(IBusinessObjectContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId) { }

            public override void AfterSave(SaveContext context) { }
        }

        /// <summary>Does not inherit <see cref="FormBusinessPlugin"/>; used to verify the type check.</summary>
        public sealed class NotAPlugin { }

        /// <summary>A runner with an empty chain never touches the context, so no member needs an implementation.</summary>
        private sealed class StubBusinessObjectContext : IBusinessObjectContext
        {
            public IDefineAccess DefineAccess => throw new NotImplementedException();
            public Polhem.Definition.Identity.ISessionInfoService SessionInfoService => throw new NotImplementedException();
            public ILanguageService LanguageService => throw new NotImplementedException();
            public IBusinessObjectFactory BoFactory => throw new NotImplementedException();
            public IServiceProvider Services => throw new NotImplementedException();
        }

        private sealed class StubCustomizeReader : ICustomizeDefineReader
        {
            public PluginSettings? Settings { get; set; }

            public PluginSettings? GetCustomizePluginSettings(string customizeId) => Settings;

            public LanguageResource? GetCustomizeLanguage(string customizeId, string lang, string ns) => null;
            public ProgramSettings? GetCustomizeProgramSettings(string customizeId) => null;
            public MenuSettings? GetCustomizeMenuSettings(string customizeId) => null;
            public FormLayout? GetCustomizeFormLayout(string customizeId, string layoutId) => null;
        }

        private sealed class StubDefineAccess : IDefineAccess
        {
            public StubDefineAccess(PluginSettings? initial) { Current = initial; }

            public PluginSettings? Current { get; set; }

            public PluginSettings GetPluginSettings()
                => Current ?? throw new FileNotFoundException("PluginSettings.xml not found");

            public object GetDefine(DefineType defineType, string[]? keys = null) => throw new NotImplementedException();
            public void SaveDefine(DefineType defineType, object defineObject, string[]? keys = null) => throw new NotImplementedException();
            public SystemSettings GetSystemSettings() => throw new NotImplementedException();
            public void SaveSystemSettings(SystemSettings settings) => throw new NotImplementedException();
            public DatabaseSettings GetDatabaseSettings() => throw new NotImplementedException();
            public void SaveDatabaseSettings(DatabaseSettings settings) => throw new NotImplementedException();
            public ProgramSettings GetProgramSettings() => throw new NotImplementedException();
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
