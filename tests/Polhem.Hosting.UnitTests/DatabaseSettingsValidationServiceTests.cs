using System.ComponentModel;
using Polhem.Definition;
using Polhem.Definition.Settings;
using Polhem.Hosting.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// The host refuses to start when the database settings lack the <c>common</c> item, instead of failing on the
    /// first request that reaches a framework table.
    /// </summary>
    public class DatabaseSettingsValidationServiceTests
    {
        [Fact]
        [DisplayName("StartAsync propagates the exception ValidateRequired throws, which stops the host")]
        public async Task StartAsync_ValidationFails_Throws()
        {
            var provider = new StubSettingsProvider(fail: true);
            var service = new DatabaseSettingsValidationService(provider);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(CancellationToken.None));
            Assert.Equal(1, provider.ValidateCalls);
        }

        [Fact]
        [DisplayName("StartAsync calls ValidateRequired once and completes when it passes")]
        public async Task StartAsync_ValidationPasses_Completes()
        {
            var provider = new StubSettingsProvider(fail: false);
            var service = new DatabaseSettingsValidationService(provider);

            var exception = await Record.ExceptionAsync(() => service.StartAsync(CancellationToken.None));

            Assert.Null(exception);
            Assert.Equal(1, provider.ValidateCalls);
        }

        [Fact]
        [DisplayName("AddPolhemFramework registers the database settings check as the first hosted service")]
        public void AddPolhemFramework_RegistersValidationAsFirstHostedService()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"polhem-dbsettings-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var services = new ServiceCollection();
                services.AddPolhemFramework(new BackendConfiguration(), new PathOptions { DefinePath = tempDir },
                    autoCreateMasterKey: true);

                var first = services.First(d => d.ServiceType == typeof(IHostedService));

                Assert.Equal(typeof(DatabaseSettingsValidationService), first.ImplementationType);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }

        private sealed class StubSettingsProvider(bool fail) : IDatabaseSettingsProvider
        {
            public int ValidateCalls { get; private set; }

            public DatabaseSettings Get() => new();

            public DatabaseItem GetItem(string databaseId) => throw new KeyNotFoundException(databaseId);

            public void ValidateRequired()
            {
                ValidateCalls++;
                if (fail) { throw new InvalidOperationException("DatabaseSettings must contain a DatabaseItem with Id='common'."); }
            }
        }
    }
}
