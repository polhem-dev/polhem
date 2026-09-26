using System.ComponentModel;
using Polhem.Business.AuditLog;
using Polhem.Business.Form;
using Polhem.Business.System;
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
    /// Resolution guards for reserved progIds (System / AuditLog): a missing entry returns the framework default, and an unloadable type or wrong base fails fast.
    /// Ordinary progIds use the same failure policy; the only difference is a wider expected base (BusinessObject rather than that axis's framework object).
    /// </summary>
    public class ReservedProgIdResolutionTests
    {
        private static string Fqn(Type t) => $"{t.FullName}, {t.Assembly.GetName().Name}";

        private static ProgramSettings Registry(params (string progId, string businessObject)[] items)
        {
            var settings = new ProgramSettings();
            foreach (var (progId, bo) in items)
                settings.Items!.Add(progId, progId).BusinessObject = bo;
            return settings;
        }

        // ---- Missing entry: the self-registration result takes part in resolution ----

        [Theory]
        [InlineData(SysProgIds.System, typeof(SystemBusinessObject))]
        [InlineData(SysProgIds.AuditLog, typeof(LogBusinessObject))]
        [DisplayName("Resolve returns the framework default BO when the registry does not declare the reserved progId (so a read-only deployment starts even when self-registration cannot write the file)")]
        public void Resolve_ReservedProgIdAbsent_ReturnsFrameworkDefault(string progId, Type expected)
        {
            var resolver = new ProgramSettingsBoTypeResolver(new StubDefineAccess(new ProgramSettings()));

            Assert.Equal(expected, resolver.Resolve(progId));
        }

        [Fact]
        [DisplayName("A reserved progId still resolves to the framework default when ProgramSettings.xml does not exist")]
        public void Resolve_ReservedProgIdWithNoRegistryFile_ReturnsFrameworkDefault()
        {
            var resolver = new ProgramSettingsBoTypeResolver(new ThrowingDefineAccess());

            Assert.Equal(typeof(SystemBusinessObject), resolver.Resolve(SysProgIds.System));
        }

        [Fact]
        [DisplayName("A reserved progId declared with an empty BusinessObject resolves to the framework default")]
        public void Resolve_ReservedProgIdWithEmptyBusinessObject_ReturnsFrameworkDefault()
        {
            var resolver = new ProgramSettingsBoTypeResolver(
                new StubDefineAccess(Registry((SysProgIds.System, string.Empty))));

            Assert.Equal(typeof(SystemBusinessObject), resolver.Resolve(SysProgIds.System));
        }

        // ---- Declared: customization succeeds ----

        [Fact]
        [DisplayName("A reserved progId bound to a SystemBusinessObject subclass resolves to that subclass")]
        public void Resolve_ReservedProgIdBoundToSubclass_ReturnsSubclass()
        {
            var resolver = new ProgramSettingsBoTypeResolver(
                new StubDefineAccess(Registry((SysProgIds.System, Fqn(typeof(CustomSystemBo))))));

            Assert.Equal(typeof(CustomSystemBo), resolver.Resolve(SysProgIds.System));
        }

        // ---- Declared but broken: fail fast ----

        [Fact]
        [DisplayName("A reserved progId bound to a type that cannot be loaded throws and names the progId and type name instead of falling back silently")]
        public void Resolve_ReservedProgIdWithUnloadableType_Throws()
        {
            var resolver = new ProgramSettingsBoTypeResolver(
                new StubDefineAccess(Registry((SysProgIds.System, "Polhem.Business.NoSuchTypeXyz, Polhem.Business"))));

            var ex = Assert.Throws<InvalidOperationException>(() => resolver.Resolve(SysProgIds.System));

            Assert.Contains(SysProgIds.System, ex.Message, StringComparison.Ordinal);
            Assert.Contains("NoSuchTypeXyz", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A reserved progId throws as well when the assembly cannot be found")]
        public void Resolve_ReservedProgIdWithMissingAssembly_Throws()
        {
            var resolver = new ProgramSettingsBoTypeResolver(
                new StubDefineAccess(Registry((SysProgIds.System, "Some.Type, NoSuchAssemblyXyz"))));

            Assert.Throws<InvalidOperationException>(() => resolver.Resolve(SysProgIds.System));
        }

        [Fact]
        [DisplayName("System bound to a FormBusinessObject subclass throws (exactly the gap opened by widening to BusinessObject)")]
        public void Resolve_SystemBoundToFormBusinessObject_Throws()
        {
            var resolver = new ProgramSettingsBoTypeResolver(
                new StubDefineAccess(Registry((SysProgIds.System, Fqn(typeof(FormBusinessObject))))));

            var ex = Assert.Throws<InvalidOperationException>(() => resolver.Resolve(SysProgIds.System));

            Assert.Contains(nameof(SystemBusinessObject), ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("AuditLog bound to SystemBusinessObject throws (each progId has its own expected base)")]
        public void Resolve_AuditLogBoundToSystemBo_Throws()
        {
            var resolver = new ProgramSettingsBoTypeResolver(
                new StubDefineAccess(Registry((SysProgIds.AuditLog, Fqn(typeof(SystemBusinessObject))))));

            Assert.Throws<InvalidOperationException>(() => resolver.Resolve(SysProgIds.AuditLog));
        }

        // ---- Ordinary progIds fail fast like reserved ones; only the expected base is wider ----

        [Fact]
        [DisplayName("An ordinary progId with an unloadable type throws as well (the two axes no longer have opposite failure policies)")]
        public void Resolve_OrdinaryProgIdWithUnloadableType_Throws()
        {
            var resolver = new ProgramSettingsBoTypeResolver(
                new StubDefineAccess(Registry(("Order", "Polhem.Business.NoSuchTypeXyz, Polhem.Business"))));

            var ex = Assert.Throws<InvalidOperationException>(() => resolver.Resolve("Order"));

            Assert.Contains("Order", ex.Message, StringComparison.Ordinal);
            Assert.Contains("NoSuchTypeXyz", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("An ordinary progId without a declared BusinessObject still returns FormBusinessObject (not declaring one is not a failure)")]
        public void Resolve_OrdinaryProgIdWithEmptyBusinessObject_ReturnsFormBusinessObject()
        {
            var resolver = new ProgramSettingsBoTypeResolver(
                new StubDefineAccess(Registry(("Order", string.Empty))));

            Assert.Equal(typeof(FormBusinessObject), resolver.Resolve("Order"));
        }

        [Fact]
        [DisplayName("The resolution target is widened to BusinessObject, so an ordinary progId bound to a LogBusinessObject subclass is accepted")]
        public void Resolve_OrdinaryProgIdBoundToNonFormBusinessObject_IsAccepted()
        {
            var resolver = new ProgramSettingsBoTypeResolver(
                new StubDefineAccess(Registry(("Report", Fqn(typeof(CustomLogBo))))));

            Assert.Equal(typeof(CustomLogBo), resolver.Resolve("Report"));
        }

        // ---- DefaultBoTypeResolver ----

        [Fact]
        [DisplayName("DefaultBoTypeResolver also returns the framework default for reserved progIds instead of always FormBusinessObject")]
        public void DefaultResolver_HonoursReservedProgIds()
        {
            var resolver = new DefaultBoTypeResolver();

            Assert.Equal(typeof(SystemBusinessObject), resolver.Resolve(SysProgIds.System));
            Assert.Equal(typeof(LogBusinessObject), resolver.Resolve(SysProgIds.AuditLog));
            Assert.Equal(typeof(FormBusinessObject), resolver.Resolve("Order"));
        }

        // ---- ReservedProgIds ----

        [Fact]
        [DisplayName("ReservedProgIds.Find is case-insensitive and returns null for an unlisted name")]
        public void ReservedProgIds_Find_IsCaseInsensitive()
        {
            Assert.NotNull(ReservedProgIds.Find("system"));
            Assert.NotNull(ReservedProgIds.Find("AUDITLOG"));
            Assert.Null(ReservedProgIds.Find("Order"));
        }

        [Fact]
        [DisplayName("ReservedProgIdBinding.DefaultTypeName is an assembly-qualified name that loads back")]
        public void ReservedProgIds_DefaultTypeName_RoundTripsThroughAssemblyLoader()
        {
            foreach (var binding in ReservedProgIds.All)
            {
                var loaded = Polhem.Base.AssemblyLoader.GetType(binding.DefaultTypeName);
                Assert.Equal(binding.DefaultType, loaded);
            }
        }

        // ---- Test doubles ----

        public sealed class CustomSystemBo : SystemBusinessObject
        {
            public CustomSystemBo(IPolhemContext ctx, Guid accessToken, string progId, bool isLocalCall = true)
                : base(ctx, accessToken, progId, isLocalCall) { }
        }

        public sealed class CustomLogBo : LogBusinessObject
        {
            public CustomLogBo(IPolhemContext ctx, Guid accessToken, string progId, bool isLocalCall = true)
                : base(ctx, accessToken, progId, isLocalCall) { }
        }

        private class StubDefineAccess : IDefineAccess
        {
            private readonly ProgramSettings _settings;
            public StubDefineAccess(ProgramSettings settings) { _settings = settings; }
            public virtual ProgramSettings GetProgramSettings() => _settings;

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

        private sealed class ThrowingDefineAccess : StubDefineAccess
        {
            public ThrowingDefineAccess() : base(new ProgramSettings()) { }
            public override ProgramSettings GetProgramSettings() => throw new FileNotFoundException();
        }
    }
}
