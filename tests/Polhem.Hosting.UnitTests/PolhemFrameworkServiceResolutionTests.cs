using System.ComponentModel;
using System.Reflection;
using Polhem.Api.Core.JsonRpc;
using Polhem.Db;
using Polhem.Hosting.Audit;
using Polhem.Db.Manager;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Logging;
using Polhem.Definition.Organization;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;
using Polhem.ObjectCaching;
using Polhem.Repository.Abstractions;
using Polhem.Repository.Abstractions.AuditLog;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.System;
using Polhem.Repository.Factories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// Checks that the singleton factory lambdas registered by AddPolhemFramework resolve, covering the singleton
    /// factory delegates in PolhemFrameworkServiceCollectionExtensions that were not covered before.
    /// </summary>
    public class PolhemFrameworkServiceResolutionTests
    {
        [Fact]
        [DisplayName("AddPolhemFramework registers an ILoginAttemptTracker by default")]
        public void AddPolhemFramework_RegistersLoginAttemptTrackerByDefault()
        {
            // Login is the credential check that anonymous callers can reach. This service used to have no default
            // implementation, so an out-of-the-box deployment had no account lockout at all.
            using var sp = BuildProvider(out string tempDir);
            try
            {
                var tracker = sp.GetService<ILoginAttemptTracker>();

                Assert.NotNull(tracker);
                Assert.IsType<Polhem.Business.Security.LoginAttemptTracker>(tracker);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }

        [Fact]
        [DisplayName("An ILoginAttemptTracker registered by the host overrides the framework default")]
        public void AddPolhemFramework_HostRegisteredTracker_Wins()
        {
            // Registration uses `TryAdd`, so a host implementation registered before `AddPolhemFramework` wins.
            string tempDir = Path.Combine(Path.GetTempPath(), $"polhem-fw-tracker-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var services = new ServiceCollection();
                services.AddSingleton<ILoginAttemptTracker, FakeLoginAttemptTracker>();
                services.AddPolhemFramework(
                    new BackendConfiguration(),
                    new PathOptions { DefinePath = tempDir },
                    autoCreateMasterKey: true);

                using var sp = services.BuildServiceProvider();

                Assert.IsType<FakeLoginAttemptTracker>(sp.GetRequiredService<ILoginAttemptTracker>());
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }

        private static ServiceProvider BuildProvider(out string tempDir)
        {
            tempDir = Path.Combine(Path.GetTempPath(), $"polhem-fw-tracker-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            var services = new ServiceCollection();
            services.AddPolhemFramework(
                new BackendConfiguration(),
                new PathOptions { DefinePath = tempDir },
                autoCreateMasterKey: true);
            return services.BuildServiceProvider();
        }

        private sealed class FakeLoginAttemptTracker : ILoginAttemptTracker
        {
            public bool IsLockedOut(string userId) => false;

            public void RecordFailure(string userId) { }

            public void Reset(string userId) { }
        }

        [Fact]
        [DisplayName("IAuditLogWriteRepository resolves when audit logging is enabled")]
        public void AddPolhemFramework_AuditLogEnabled_ResolvesWriteRepository()
        {
            // This registration exists only when `AuditLogOptions.Enabled` is set, which is off by default, so other
            // tests never reach it. Since repositories were unified on (`IRepositoryContext`, `Guid`, `string`), the
            // container cannot construct the concrete type by itself (it can supply none of the three parameters);
            // only the factory can.
            string tempDir = Path.Combine(Path.GetTempPath(), $"polhem-fw-audit-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var configuration = new BackendConfiguration();
                configuration.AuditLogOptions.Enabled = true;
                configuration.AuditLogOptions.UseBackgroundWriter = false;

                var services = new ServiceCollection();
                // The sink in the audit chain needs an `ILogger`. A real host always has one; a bare
                // `ServiceCollection` does not.
                services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
                services.AddPolhemFramework(
                    configuration,
                    new PathOptions { DefinePath = tempDir },
                    autoCreateMasterKey: true);

                using var sp = services.BuildServiceProvider();

                Assert.NotNull(sp.GetRequiredService<IAuditLogWriteRepository>());
                Assert.NotNull(sp.GetRequiredService<IAuditLogWriter>());
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [DisplayName("A host's own IAuditLogSink replaces the database sink whether it is registered before or after AddPolhemFramework")]
        public void AddPolhemFramework_HostRegistersAuditLogSink_HostSinkIsUsed(bool registerBefore)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"polhem-fw-sink-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var configuration = new BackendConfiguration();
                configuration.AuditLogOptions.Enabled = true;
                configuration.AuditLogOptions.UseBackgroundWriter = false;
                var hostSink = new RecordingAuditLogSink();

                var services = new ServiceCollection();
                services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
                if (registerBefore) { services.AddSingleton<IAuditLogSink>(hostSink); }
                services.AddPolhemFramework(
                    configuration,
                    new PathOptions { DefinePath = tempDir },
                    autoCreateMasterKey: true);
                if (!registerBefore) { services.AddSingleton<IAuditLogSink>(hostSink); }

                using var sp = services.BuildServiceProvider();

                Assert.Same(hostSink, sp.GetRequiredService<IAuditLogSink>());
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }

        private sealed class RecordingAuditLogSink : IAuditLogSink
        {
            public List<AuditEntry> Entries { get; } = [];

            public void WriteBatch(IReadOnlyList<AuditEntry> entries) => Entries.AddRange(entries);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [DisplayName("The anomaly logging switch affects only IAnomalyLogWriter and leaves the audit side unchanged")]
        public void AddPolhemFramework_AnomalyEnabled_GatesOnlyTheAnomalyWriter(bool anomalyEnabled)
        {
            // The two interfaces resolve separately: with auditing on, `IAuditLogWriter` is always the real writer,
            // while the anomaly side is decided by `AnomalyEnabled` alone. This pair is exactly what splitting into two
            // interfaces bought; before the split, "auditing on but no anomaly logging" could not be seen in the types.
            string tempDir = Path.Combine(Path.GetTempPath(), $"polhem-fw-anomaly-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var configuration = new BackendConfiguration();
                configuration.AuditLogOptions.Enabled = true;
                configuration.AuditLogOptions.UseBackgroundWriter = false;
                configuration.AuditLogOptions.AnomalyEnabled = anomalyEnabled;

                var services = new ServiceCollection();
                services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
                services.AddPolhemFramework(
                    configuration,
                    new PathOptions { DefinePath = tempDir },
                    autoCreateMasterKey: true);

                using var sp = services.BuildServiceProvider();

                var auditWriter = sp.GetRequiredService<IAuditLogWriter>();
                var anomalyWriter = sp.GetRequiredService<IAnomalyLogWriter>();

                Assert.IsNotType<NullLogWriter>(auditWriter);
                if (anomalyEnabled)
                {
                    // One instance serves both interfaces, so queueing and fallback behave the same for both
                    // kinds of log.
                    Assert.Same(auditWriter, anomalyWriter);
                }
                else
                {
                    Assert.Same(NullLogWriter.Instance, anomalyWriter);
                }
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }

        [Fact]
        [DisplayName("AddPolhemFramework with the default configuration resolves the full DI service chain (IDbConnectionManager to JsonRpcExecutor)")]
        public void AddPolhemFramework_DefaultConfig_ResolvesFullServiceChain()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"polhem-fw-fullchain-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var services = new ServiceCollection();
                services.AddPolhemFramework(
                    new BackendConfiguration(),
                    new PathOptions { DefinePath = tempDir },
                    autoCreateMasterKey: true);

                using var sp = services.BuildServiceProvider();

                Assert.NotNull(sp.GetRequiredService<IDbConnectionManager>());
                Assert.NotNull(sp.GetRequiredService<IDbAccessFactory>());
                Assert.NotNull(sp.GetRequiredService<IAccessTokenValidator>());
                Assert.NotNull(sp.GetRequiredService<ISessionInfoService>());
                Assert.NotNull(sp.GetRequiredService<ICompanyInfoService>());
                // Resolving both of these proves the cache container takes its data source as a
                // deferred factory: resolving it eagerly would close the cycle ICacheContainer →
                // ICacheDataSourceProvider → repositories → IDefineAccess → ICacheContainer.
                Assert.NotNull(sp.GetRequiredService<ICacheContainer>());
                Assert.NotNull(sp.GetRequiredService<ICacheDataSourceProvider>());
                Assert.NotNull(sp.GetRequiredService<IRolePermissionService>());
                Assert.NotNull(sp.GetRequiredService<IDepartmentTreeService>());
                Assert.NotNull(sp.GetRequiredService<IBusinessObjectFactory>());
                Assert.NotNull(sp.GetRequiredService<IRepositoryDatabaseRouter>());
                // The single entry point to repositories; both axes resolve through it.
                Assert.NotNull(sp.GetRequiredService<IRepositoryFactory>());
                // Individual repositories are not DI-registered by design — consumers go
                // through the factory, so resolving one from it is what this asserts.
                Assert.NotNull(sp.GetRequiredService<IRepositoryFactory>().Create<ICompanyRepository>());
                Assert.NotNull(sp.GetRequiredService<IRepositoryFactory>().Create<IUserCompanyRepository>());
                Assert.NotNull(sp.GetRequiredService<JsonRpcExecutor>());
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }

        [Fact]
        [DisplayName("The container-built RepositoryFactory receives, through its resolver, the two optional dependencies the customization overlay needs")]
        public void AddPolhemFramework_RepositoryFactory_ReceivesCustomizationDependencies()
        {
            // These two dependencies are optional parameters (default null), and leaving them out silently disables
            // tenant customization: every progId resolves to its base binding, with no other symptom. The behavior
            // does not show it, so the only way is to check the fields directly, which is why this test exists.
            //
            // They used to live on the factory and moved to the resolver when type resolution was extracted into
            // `IRepositoryTypeResolver`. The risk did not go away: the resolver's constructor takes them as optional
            // parameters too, and one missing argument in the Hosting registration brings back the same silent
            // regression. So this checks two things: the factory gets the resolver the container registered, and
            // that resolver has both dependencies wired.
            string tempDir = Path.Combine(Path.GetTempPath(), $"polhem-fw-repocust-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var services = new ServiceCollection();
                services.AddPolhemFramework(
                    new BackendConfiguration(),
                    new PathOptions { DefinePath = tempDir },
                    autoCreateMasterKey: true);

                using var sp = services.BuildServiceProvider();
                var factory = sp.GetRequiredService<IRepositoryFactory>();
                var resolver = PrivateField(factory, "_typeResolver");

                Assert.Same(sp.GetRequiredService<IRepositoryTypeResolver>(), resolver);
                Assert.NotNull(PrivateField(resolver!, "_customizeReader"));
                Assert.NotNull(PrivateField(resolver!, "_sessionInfoService"));
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }

        private static object? PrivateField(object instance, string name)
            => instance.GetType()
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(instance);
    }
}
