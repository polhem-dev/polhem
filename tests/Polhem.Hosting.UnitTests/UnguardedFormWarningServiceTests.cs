using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.Hosting.Registry;
using Polhem.ObjectCaching;
using Microsoft.Extensions.Logging;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// At startup the host names the forms, stored or registered, that declare no permission model, since every
    /// authenticated user of the company can read and write them.
    /// </summary>
    public sealed class UnguardedFormWarningServiceTests : IDisposable
    {
        private readonly string _defineDir;
        private readonly PathOptions _paths;

        public UnguardedFormWarningServiceTests()
        {
            _defineDir = Path.Combine(Path.GetTempPath(), $"polhem-unguarded-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_defineDir);
            _paths = new PathOptions { DefinePath = _defineDir };
        }

        public void Dispose()
        {
            try { Directory.Delete(_defineDir, recursive: true); } catch (IOException) { /* best effort */ }
        }

        [Fact]
        [DisplayName("StartAsync logs one warning naming exactly the registered forms without a PermissionModelId")]
        public async Task StartAsync_FormsWithoutPermissionModel_LogsOneWarningNamingThem()
        {
            var access = CreateAccess();
            access.SaveFormSchema(Schema("OpenForm", permissionModelId: string.Empty));
            access.SaveFormSchema(Schema("GuardedForm", permissionModelId: "GuardedModel"));
            access.SaveProgramSettings(Registry(SysProgIds.System, "OpenForm", "GuardedForm", "NoSchemaProgram"));
            var logger = new ListLogger();

            await new UnguardedFormWarningService(access, new FileDefineStorage(_paths), logger).StartAsync(CancellationToken.None);

            var warning = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, warning.Level);
            Assert.Contains("OpenForm", warning.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("GuardedForm", warning.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("NoSchemaProgram", warning.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(SysProgIds.System, warning.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("StartAsync also names a stored form without a PermissionModelId that ProgramSettings does not list, because it is served all the same")]
        public async Task StartAsync_UnregisteredFormWithoutPermissionModel_IsNamed()
        {
            var access = CreateAccess();
            access.SaveFormSchema(Schema("UnlistedForm", permissionModelId: string.Empty));
            access.SaveFormSchema(Schema("UnlistedGuardedForm", permissionModelId: "GuardedModel"));
            access.SaveProgramSettings(Registry(SysProgIds.System));
            var logger = new ListLogger();

            await new UnguardedFormWarningService(access, new FileDefineStorage(_paths), logger).StartAsync(CancellationToken.None);

            var warning = Assert.Single(logger.Entries);
            Assert.Contains("UnlistedForm", warning.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("UnlistedGuardedForm", warning.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A form both stored and registered under another casing is named once, in the stored spelling")]
        public void FindUnguardedForms_StoredAndRegisteredInOtherCasing_NamesItOnce()
        {
            var access = CreateAccess();
            access.SaveFormSchema(Schema("OpenForm", permissionModelId: string.Empty));
            access.SaveProgramSettings(Registry("OPENFORM"));

            var found = new UnguardedFormWarningService(access, new FileDefineStorage(_paths), new ListLogger()).FindUnguardedForms();

            Assert.Equal(["OpenForm"], found);
        }

        [Fact]
        [DisplayName("StartAsync logs nothing when every registered form declares a permission model")]
        public async Task StartAsync_AllFormsGuarded_LogsNothing()
        {
            var access = CreateAccess();
            access.SaveFormSchema(Schema("GuardedForm", permissionModelId: "GuardedModel"));
            access.SaveProgramSettings(Registry("GuardedForm"));
            var logger = new ListLogger();

            await new UnguardedFormWarningService(access, new FileDefineStorage(_paths), logger).StartAsync(CancellationToken.None);

            Assert.Empty(logger.Entries);
        }

        [Fact]
        [DisplayName("StartAsync without a ProgramSettings file or any form logs nothing and does not throw")]
        public async Task StartAsync_NoRegistryFile_LogsNothing()
        {
            var logger = new ListLogger();

            var ex = await Record.ExceptionAsync(
                () => new UnguardedFormWarningService(CreateAccess(), new FileDefineStorage(_paths), logger).StartAsync(CancellationToken.None));

            Assert.Null(ex);
            Assert.Empty(logger.Entries);
        }

        private CacheDefineAccess CreateAccess()
        {
            var storage = new FileDefineStorage(_paths);
            var cache = new CacheContainerService(storage, _paths, "unguarded_" + Guid.NewGuid().ToString("N"));
            return new CacheDefineAccess(storage, _paths, cache, Array.Empty<byte>());
        }

        private static FormSchema Schema(string progId, string permissionModelId)
        {
            var schema = new FormSchema(progId, progId) { CategoryId = "company", PermissionModelId = permissionModelId };
            var table = schema.Tables!.Add(progId, progId);
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            return schema;
        }

        private static ProgramSettings Registry(params string[] progIds)
        {
            var settings = new ProgramSettings();
            foreach (var progId in progIds)
                settings.Items!.Add(new ProgramItem(progId, progId));
            return settings;
        }

        private sealed class ListLogger : ILogger<UnguardedFormWarningService>
        {
            public List<(LogLevel Level, string Message)> Entries { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
                => Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
