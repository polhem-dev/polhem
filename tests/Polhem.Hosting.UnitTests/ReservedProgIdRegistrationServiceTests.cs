using System.ComponentModel;
using Polhem.Base.Serialization;
using Polhem.Business;
using Polhem.Business.Form;
using Polhem.Business.System;
using Polhem.Definition;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.Hosting.Registry;
using Polhem.ObjectCaching;
using Microsoft.Extensions.Logging.Abstractions;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// Startup self-registration of the reserved progIds: a fresh DefinePath, an existing ProgramSettings.xml without
    /// the System entry, a customization that overrides the System BO, and a failed write on a read-only deployment
    /// that does not affect the current run.
    /// </summary>
    public sealed class ReservedProgIdRegistrationServiceTests : IDisposable
    {
        private readonly string _defineDir;
        private readonly PathOptions _paths;

        public ReservedProgIdRegistrationServiceTests()
        {
            _defineDir = Path.Combine(Path.GetTempPath(), $"polhem-reserved-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_defineDir);
            _paths = new PathOptions { DefinePath = _defineDir };
        }

        public void Dispose()
        {
            try { Directory.Delete(_defineDir, recursive: true); } catch (IOException) { /* best effort */ }
        }

        private CacheDefineAccess CreateAccess()
        {
            var storage = new FileDefineStorage(_paths);
            var cache = new CacheContainerService(storage, _paths, "reserved_" + Guid.NewGuid().ToString("N"));
            return new CacheDefineAccess(storage, _paths, cache, Array.Empty<byte>());
        }

        private static ReservedProgIdRegistrationService CreateService(IDefineAccess access)
            => new(access, new ProgramSettingsBoTypeResolver(access),
                   NullLogger<ReservedProgIdRegistrationService>.Instance);

        private ProgramSettings ReadRegistryFromDisk()
            => XmlCodec.DeserializeFromFile<ProgramSettings>(_paths.GetProgramSettingsFilePath())!;

        [Fact]
        [DisplayName("StartAsync on a fresh DefinePath writes a ProgramSettings.xml containing every reserved progId")]
        public async Task StartAsync_EmptyDefinePath_WritesAllReservedEntries()
        {
            var access = CreateAccess();

            await CreateService(access).StartAsync(CancellationToken.None);

            Assert.True(File.Exists(_paths.GetProgramSettingsFilePath()));
            var registry = ReadRegistryFromDisk();
            foreach (var binding in ReservedProgIds.All)
            {
                Assert.True(registry.Items!.Contains(binding.ProgId));
                Assert.Equal(binding.DefaultTypeName, registry.Items![binding.ProgId].BusinessObject);
            }
        }

        [Fact]
        [DisplayName("StartAsync adds the missing reserved entries to an existing ProgramSettings.xml without a System entry and keeps the others")]
        public async Task StartAsync_ExistingRegistryMissingSystem_AddsItAndKeepsOthers()
        {
            // Every host shipping today is in exactly this state: a ProgramSettings.xml with
            // application progIds and no System entry. A file-exists check would skip it.
            var existing = new ProgramSettings();
            existing.Items!.Add("Order", "訂單").BusinessObject = "MyErp.OrderBO, MyErp";
            new FileDefineStorage(_paths).SaveProgramSettings(existing);

            await CreateService(CreateAccess()).StartAsync(CancellationToken.None);

            var registry = ReadRegistryFromDisk();
            Assert.True(registry.Items!.Contains(SysProgIds.System));
            Assert.True(registry.Items!.Contains(SysProgIds.AuditLog));
            Assert.Equal("MyErp.OrderBO, MyErp", registry.Items!["Order"].BusinessObject);
        }

        [Fact]
        [DisplayName("Adding the reserved progIds keeps the Repository binding of existing entries")]
        public async Task StartAsync_ExistingRegistryWithRepository_PreservesIt()
        {
            // Adding entries rewrites the whole file, so any property that is not copied over is lost, and only on a
            // host that happens to lack a reserved progId, which is rare and hard to trace. Every property added to
            // `ProgramItem` needs a test like this one guarding it.
            var existing = new ProgramSettings();
            var order = existing.Items!.Add("Order", "訂單");
            order.BusinessObject = "MyErp.OrderBO, MyErp";
            order.Repository = "MyErp.OrderRepository, MyErp";
            new FileDefineStorage(_paths).SaveProgramSettings(existing);

            await CreateService(CreateAccess()).StartAsync(CancellationToken.None);

            var registry = ReadRegistryFromDisk();
            Assert.Equal("MyErp.OrderRepository, MyErp", registry.Items!["Order"].Repository);
            // The reserved progIds leave Repository empty: their BOs are not schema-driven CRUD and read
            // through the framework repositories.
            Assert.Equal(string.Empty, registry.Items![SysProgIds.System].Repository);
            Assert.Equal(string.Empty, registry.Items![SysProgIds.AuditLog].Repository);
        }

        [Fact]
        [DisplayName("A reserved progId that is already declared is not overwritten, so a customized System BO is kept")]
        public async Task StartAsync_ReservedProgIdAlreadyDeclared_IsNotOverwritten()
        {
            var custom = $"{typeof(CustomSystemBo).FullName}, {typeof(CustomSystemBo).Assembly.GetName().Name}";
            var existing = new ProgramSettings();
            existing.Items!.Add(SysProgIds.System, "System").BusinessObject = custom;
            new FileDefineStorage(_paths).SaveProgramSettings(existing);

            var access = CreateAccess();
            await CreateService(access).StartAsync(CancellationToken.None);

            Assert.Equal(custom, ReadRegistryFromDisk().Items![SysProgIds.System].BusinessObject);
            Assert.Equal(typeof(CustomSystemBo),
                new ProgramSettingsBoTypeResolver(access).Resolve(SysProgIds.System));
        }

        [Fact]
        [DisplayName("Adding entries invalidates the cache, so the same IDefineAccess sees the new entries immediately")]
        public async Task StartAsync_InvalidatesCache_SoResolutionSeesNewEntries()
        {
            var access = CreateAccess();
            // Warm the cache first: without invalidation this instance would keep serving the
            // pre-registration snapshot.
            new FileDefineStorage(_paths).SaveProgramSettings(new ProgramSettings());
            _ = access.GetProgramSettings();

            await CreateService(access).StartAsync(CancellationToken.None);

            Assert.True(access.GetProgramSettings().Items!.Contains(SysProgIds.System));
        }

        [Fact]
        [DisplayName("A failed write (read-only deployment) does not block startup and resolution falls back to the framework default")]
        public async Task StartAsync_PersistFails_StillStartsAndResolves()
        {
            var access = new ReadOnlyDefineAccess(CreateAccess());

            var exception = await Record.ExceptionAsync(
                () => CreateService(access).StartAsync(CancellationToken.None));

            Assert.Null(exception);
            Assert.Equal(typeof(SystemBusinessObject),
                new ProgramSettingsBoTypeResolver(access).Resolve(SysProgIds.System));
        }

        [Fact]
        [DisplayName("Startup is refused when a reserved progId resolves to a type without the expected base class")]
        public async Task StartAsync_ReservedProgIdResolvesToWrongBase_Throws()
        {
            var access = CreateAccess();
            var service = new ReservedProgIdRegistrationService(
                access, new WrongTypeResolver(), NullLogger<ReservedProgIdRegistrationService>.Instance);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.StartAsync(CancellationToken.None));

            Assert.Contains(SysProgIds.System, ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Running StartAsync twice is idempotent and adds no duplicate entries")]
        public async Task StartAsync_RunTwice_IsIdempotent()
        {
            await CreateService(CreateAccess()).StartAsync(CancellationToken.None);
            int afterFirst = ReadRegistryFromDisk().Items!.Count;

            await CreateService(CreateAccess()).StartAsync(CancellationToken.None);

            Assert.Equal(afterFirst, ReadRegistryFromDisk().Items!.Count);
        }

        // ---- Test doubles ----

        public sealed class CustomSystemBo : SystemBusinessObject
        {
            public CustomSystemBo(IPolhemContext ctx, Guid accessToken, string progId, bool isLocalCall = true)
                : base(ctx, accessToken, progId, isLocalCall) { }
        }

        /// <summary>Stands in for a resolver that ignores the reserved progIds.</summary>
        private sealed class WrongTypeResolver : IBoTypeResolver
        {
            public Type Resolve(string progId) => typeof(FormBusinessObject);
        }

        /// <summary>Stands in for a read-only deployment: reads work, the write does not.</summary>
        private sealed class ReadOnlyDefineAccess : IDefineAccess
        {
            private readonly IDefineAccess _inner;
            public ReadOnlyDefineAccess(IDefineAccess inner) { _inner = inner; }

            public void SaveProgramSettings(ProgramSettings settings)
                => throw new UnauthorizedAccessException("read-only deployment");

            public ProgramSettings GetProgramSettings() => _inner.GetProgramSettings();
            public object GetDefine(DefineType defineType, string[]? keys = null) => _inner.GetDefine(defineType, keys);
            public void SaveDefine(DefineType defineType, object defineObject, string[]? keys = null) => _inner.SaveDefine(defineType, defineObject, keys);
            public Definition.Settings.SystemSettings GetSystemSettings() => _inner.GetSystemSettings();
            public void SaveSystemSettings(Definition.Settings.SystemSettings settings) => _inner.SaveSystemSettings(settings);
            public DatabaseSettings GetDatabaseSettings() => _inner.GetDatabaseSettings();
            public void SaveDatabaseSettings(DatabaseSettings settings) => _inner.SaveDatabaseSettings(settings);
            public DbCategorySettings GetDbCategorySettings() => _inner.GetDbCategorySettings();
            public void SaveDbCategorySettings(DbCategorySettings settings) => _inner.SaveDbCategorySettings(settings);
            public Definition.Database.TableSchema GetTableSchema(string categoryId, string tableName) => _inner.GetTableSchema(categoryId, tableName);
            public void SaveTableSchema(string categoryId, Definition.Database.TableSchema tableSchema) => _inner.SaveTableSchema(categoryId, tableSchema);
            public Definition.Forms.FormSchema GetFormSchema(string progId) => _inner.GetFormSchema(progId);
            public void SaveFormSchema(Definition.Forms.FormSchema formSchema) => _inner.SaveFormSchema(formSchema);
            public Definition.Layouts.FormLayout GetFormLayout(string layoutId) => _inner.GetFormLayout(layoutId);
            public void SaveFormLayout(Definition.Layouts.FormLayout formLayout) => _inner.SaveFormLayout(formLayout);
            public Definition.Language.LanguageResource GetLanguage(string lang, string ns) => _inner.GetLanguage(lang, ns);
            public void SaveLanguage(Definition.Language.LanguageResource resource) => _inner.SaveLanguage(resource);
        }
    }
}
