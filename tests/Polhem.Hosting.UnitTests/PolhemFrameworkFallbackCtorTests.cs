using System.ComponentModel;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Polhem.Hosting.UnitTests
{
    // Stub with only an (IDefineStorage, PathOptions) ctor: it takes no configuration key, so none is passed.
    public sealed class TwoArgDefineAccessStub : IDefineAccess
    {
        public TwoArgDefineAccessStub(IDefineStorage storage, PathOptions paths) { }
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

    // Stub with only an (IDefineStorage) ctor: every parameter is a registered service and none is the key.
    public sealed class StorageOnlyDefineAccessStub : IDefineAccess
    {
        public StorageOnlyDefineAccessStub(IDefineStorage storage) { }
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

    // Stub whose ctor needs a service nobody registered.
    public sealed class UnresolvableDependencyDefineAccessStub : IDefineAccess
    {
        public UnresolvableDependencyDefineAccessStub(IUnregisteredTestDependency dependency) { }
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

    // Stub with only a parameterless ctor, which DI-aware construction handles like any other.
    public sealed class ParameterlessDefineStorageStub : IDefineStorage
    {
        public DbCategorySettings? GetDbCategorySettings() => null;
        public void SaveDbCategorySettings(DbCategorySettings settings) { }
        public CurrencySettings? GetCurrencySettings() => null;
        public void SaveCurrencySettings(CurrencySettings settings) { }
        public UnitSettings? GetUnitSettings() => null;
        public void SaveUnitSettings(UnitSettings settings) { }
        public ProgramSettings? GetProgramSettings() => null;
        public void SaveProgramSettings(ProgramSettings settings) { }
        public MenuSettings? GetMenuSettings() => null;
        public void SaveMenuSettings(MenuSettings settings) { }
        public PluginSettings? GetPluginSettings() => null;
        public void SavePluginSettings(PluginSettings settings) { }
        public TableSchema? GetTableSchema(string categoryId, string tableName) => null;
        public void SaveTableSchema(string categoryId, TableSchema tableSchema) { }
        public FormSchema? GetFormSchema(string progId) => null;
        public void SaveFormSchema(FormSchema formSchema) { }
        public FormLayout? GetFormLayout(string layoutId) => null;
        public void SaveFormLayout(FormLayout formLayout) { }
        public LanguageResource? GetLanguage(string lang, string ns) => null;
        public void SaveLanguage(LanguageResource resource) { }
    }

    // Never registered in the container, so a ctor asking for it cannot be satisfied by DI.
    public interface IUnregisteredTestDependency
    {
        string Name { get; }
    }

    // Stub whose only ctor needs an unregistered service — exercises CreateConfigurableService
    // when DI-aware construction fails and there is no parameterless ctor to fall back to.
    public sealed class UnresolvableDependencyTokenValidatorStub : IAccessTokenValidator
    {
        public UnresolvableDependencyTokenValidatorStub(IUnregisteredTestDependency dependency) { }
        public bool Validate(Guid accessToken) => false;
    }

    // Stub with only a parameterless ctor, which DI-aware construction handles like any other.
    public sealed class ParameterlessTokenValidatorStub : IAccessTokenValidator
    {
        public bool Validate(Guid accessToken) => false;
    }

    public class PolhemFrameworkFallbackCtorTests
    {
        [Fact]
        [DisplayName("CreateConfigurableService names the missing service type in the exception when a constructor dependency is not registered")]
        public void CreateConfigurableService_UnregisteredCtorDependency_ExceptionNamesMissingService()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"polhem-fw-unresolved-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                using var sp = BuildProviderWithAccessTokenValidator(
                    "Polhem.Hosting.UnitTests.UnresolvableDependencyTokenValidatorStub, Polhem.Hosting.UnitTests", tempDir);

                var ex = Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<IAccessTokenValidator>());

                Assert.Contains(nameof(IUnregisteredTestDependency), ex.Message, StringComparison.Ordinal);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { }
            }
        }

        [Fact]
        [DisplayName("CreateConfigurableService supports an implementation that has only a parameterless constructor")]
        public void CreateConfigurableService_ParameterlessCtor_CreatesCorrectType()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"polhem-fw-pless-svc-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                using var sp = BuildProviderWithAccessTokenValidator(
                    "Polhem.Hosting.UnitTests.ParameterlessTokenValidatorStub, Polhem.Hosting.UnitTests", tempDir);

                Assert.IsType<ParameterlessTokenValidatorStub>(sp.GetRequiredService<IAccessTokenValidator>());
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { }
            }
        }

        [Theory]
        [InlineData("Polhem.Hosting.UnitTests.NoSuchValidator, Polhem.NoSuchAssembly")]
        [InlineData("Polhem.Hosting.UnitTests.NoSuchValidator, Polhem.Hosting.UnitTests")]
        [InlineData("Polhem.Hosting.UnitTests.ParameterlessDefineStorageStub, Polhem.Hosting.UnitTests")]
        [DisplayName("A BackendComponents type name that cannot be used fails with an error naming the setting and the type")]
        public void CreateConfigurableService_UnusableTypeName_ErrorNamesSettingAndType(string typeName)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"polhem-fw-badtype-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                using var sp = BuildProviderWithAccessTokenValidator(typeName, tempDir);

                var ex = Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<IAccessTokenValidator>());

                Assert.Contains("BackendComponents.AccessTokenValidator", ex.Message, StringComparison.Ordinal);
                Assert.Contains(typeName, ex.Message, StringComparison.Ordinal);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { }
            }
        }

        private static ServiceProvider BuildProviderWithAccessTokenValidator(string typeName, string tempDir)
        {
            var configuration = new BackendConfiguration();
            configuration.Components.AccessTokenValidator = typeName;

            var services = new ServiceCollection();
            services.AddPolhemFramework(
                configuration,
                new PathOptions { DefinePath = tempDir },
                autoCreateMasterKey: true);
            return services.BuildServiceProvider();
        }

        [Fact]
        [DisplayName("CreateDefineAccess builds an IDefineAccess implementation that has only an (IDefineStorage, PathOptions) constructor")]
        public void CreateDefineAccess_TwoArgCtor_CreatesCorrectType()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"polhem-fw-2arg-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var configuration = new BackendConfiguration();
                configuration.Components.DefineAccess =
                    "Polhem.Hosting.UnitTests.TwoArgDefineAccessStub, Polhem.Hosting.UnitTests";

                var services = new ServiceCollection();
                services.AddPolhemFramework(
                    configuration,
                    new PathOptions { DefinePath = tempDir },
                    autoCreateMasterKey: true);

                using var sp = services.BuildServiceProvider();
                var access = sp.GetRequiredService<IDefineAccess>();

                Assert.IsType<TwoArgDefineAccessStub>(access);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { }
            }
        }

        [Fact]
        [DisplayName("CreateDefineStorage builds an IDefineStorage implementation that has only a parameterless constructor")]
        public void CreateDefineStorage_ParameterlessOnly_CreatesCorrectType()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"polhem-fw-pless-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                using var sp = BuildProvider(tempDir, c => c.Components.DefineStorage =
                    "Polhem.Hosting.UnitTests.ParameterlessDefineStorageStub, Polhem.Hosting.UnitTests");

                Assert.IsType<ParameterlessDefineStorageStub>(sp.GetRequiredService<IDefineStorage>());
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { }
            }
        }

        [Fact]
        [DisplayName("CreateDefineAccess builds an IDefineAccess implementation whose constructor takes only registered services")]
        public void CreateDefineAccess_StorageOnlyCtor_CreatesCorrectType()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"polhem-fw-1arg-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                using var sp = BuildProvider(tempDir, c => c.Components.DefineAccess =
                    "Polhem.Hosting.UnitTests.StorageOnlyDefineAccessStub, Polhem.Hosting.UnitTests");

                Assert.IsType<StorageOnlyDefineAccessStub>(sp.GetRequiredService<IDefineAccess>());
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { }
            }
        }

        [Fact]
        [DisplayName("CreateDefineAccess names the missing service in the exception when a constructor dependency is not registered")]
        public void CreateDefineAccess_UnregisteredCtorDependency_ExceptionNamesMissingService()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"polhem-fw-unres-access-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                using var sp = BuildProvider(tempDir, c => c.Components.DefineAccess =
                    "Polhem.Hosting.UnitTests.UnresolvableDependencyDefineAccessStub, Polhem.Hosting.UnitTests");

                var ex = Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<IDefineAccess>());

                Assert.Contains(nameof(IUnregisteredTestDependency), ex.Message, StringComparison.Ordinal);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { }
            }
        }

        [Fact]
        [DisplayName("The default CacheDefineAccess is built through its longest constructor, so it receives the logger")]
        public void CreateDefineAccess_Default_ReceivesLogger()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"polhem-fw-default-access-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                using var sp = BuildProvider(tempDir, _ => { }, services =>
                    services.AddSingleton<Microsoft.Extensions.Logging.ILogger<Polhem.ObjectCaching.CacheDefineAccess>>(
                        Microsoft.Extensions.Logging.Abstractions.NullLogger<Polhem.ObjectCaching.CacheDefineAccess>.Instance));

                var access = Assert.IsType<Polhem.ObjectCaching.CacheDefineAccess>(sp.GetRequiredService<IDefineAccess>());
                var loggerField = typeof(Polhem.ObjectCaching.CacheDefineAccess)
                    .GetField("_logger", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

                Assert.NotNull(loggerField.GetValue(access));
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { }
            }
        }

        private static ServiceProvider BuildProvider(string tempDir, Action<BackendConfiguration> configure,
            Action<IServiceCollection>? services = null)
        {
            var configuration = new BackendConfiguration();
            configure(configuration);

            var collection = new ServiceCollection();
            services?.Invoke(collection);
            collection.AddPolhemFramework(
                configuration,
                new PathOptions { DefinePath = tempDir },
                autoCreateMasterKey: true);
            return collection.BuildServiceProvider();
        }
    }
}
