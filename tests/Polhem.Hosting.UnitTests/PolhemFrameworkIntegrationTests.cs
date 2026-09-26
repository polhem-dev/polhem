using System.ComponentModel;
using Polhem.Base.Security;
using Polhem.Business.Providers;
using Polhem.Definition;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Polhem.Hosting.UnitTests
{
    public class PolhemFrameworkIntegrationTests
    {
        [Fact]
        [DisplayName("AddPolhemFramework with a valid configuration and autoCreateMasterKey=true registers the services and returns the IServiceCollection")]
        public void AddPolhemFramework_ValidConfigurationAutoCreateKey_RegistersServices()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"polhem-fw-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var services = new ServiceCollection();
                var configuration = new BackendConfiguration();
                var pathOptions = new PathOptions { DefinePath = tempDir };

                // With `autoCreateMasterKey` set, Master.key is created in tempDir. This also covers
                // `DecryptSecurityKeys`, `CacheInfo.Initialize` and the `AddSingleton` registration paths.
                var result = services.AddPolhemFramework(configuration, pathOptions, autoCreateMasterKey: true);

                Assert.Same(services, result);
                Assert.True(services.Count > 0);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }

        [Fact]
        [DisplayName("AddPolhemFramework configured with StaticApiEncryptionKeyProvider resolves the static key provider")]
        public void AddPolhemFramework_StaticApiEncryptionKeyProvider_ResolvesStaticProvider()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"polhem-fw-static-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                string masterKeyBase64 = AesCbcHmacKeyGenerator.GenerateBase64CombinedKey();
                byte[] masterKey = Convert.FromBase64String(masterKeyBase64);
                File.WriteAllText(Path.Combine(tempDir, "Master.key"), masterKeyBase64);

                string encryptedApiKey = EncryptionKeyProtector.GenerateEncryptedKey(masterKey);

                var configuration = new BackendConfiguration();
                // This test prepares its own Master.key file, so the File source must be set explicitly to override
                // the `MasterKeySource` default (Environment). Otherwise the framework reads the `POLHEM_MASTER_KEY`
                // environment variable, which does not match the master key generated here,
                // and HMAC verification fails.
                configuration.SecurityKeySettings.MasterKeySource = new MasterKeySource
                {
                    Type = MasterKeySourceType.File,
                    Value = "Master.key"
                };
                configuration.SecurityKeySettings.ApiEncryptionKey = encryptedApiKey;
                configuration.Components.ApiEncryptionKeyProvider =
                    "Polhem.Business.Providers.StaticApiEncryptionKeyProvider, Polhem.Business";

                var services = new ServiceCollection();
                var pathOptions = new PathOptions { DefinePath = tempDir };
                services.AddPolhemFramework(configuration, pathOptions);

                // Resolving `IApiEncryptionKeyProvider` exercises the static branch of the private
                // `CreateApiEncryptionKeyProvider`.
                using var sp = services.BuildServiceProvider();
                var provider = sp.GetRequiredService<IApiEncryptionKeyProvider>();

                Assert.IsType<StaticApiEncryptionKeyProvider>(provider);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }
    }
}
