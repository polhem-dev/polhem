using System.ComponentModel;
using Polhem.Business.Providers;
using Polhem.Definition;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// Checks which <see cref="IApiEncryptionKeyProvider"/> branch <c>AddPolhemFramework</c> resolves.
    /// The default is <see cref="DerivedApiEncryptionKeyProvider"/> (session rebuild needs it); when
    /// <c>ApiEncryptionKey</c> is not set, the root key is derived from the master key instead.
    /// </summary>
    public class PolhemFrameworkApiEncryptionKeyProviderTests
    {
        private static void WithFramework(string prefix, Action<IServiceProvider> assert, Action<BackendConfiguration>? configure = null)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"polhem-fw-{prefix}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var services = new ServiceCollection();
                var configuration = new BackendConfiguration();
                configure?.Invoke(configuration);
                var pathOptions = new PathOptions { DefinePath = tempDir };

                services.AddPolhemFramework(configuration, pathOptions, autoCreateMasterKey: true);

                using var sp = services.BuildServiceProvider();
                assert(sp);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }

        [Fact]
        [DisplayName("AddPolhemFramework with the default configuration resolves IApiEncryptionKeyProvider to DerivedApiEncryptionKeyProvider")]
        public void AddPolhemFramework_DefaultConfig_ResolvesDerivedApiEncryptionKeyProvider()
        {
            WithFramework("derived", sp =>
                Assert.IsType<DerivedApiEncryptionKeyProvider>(sp.GetRequiredService<IApiEncryptionKeyProvider>()));
        }

        [Fact]
        [DisplayName("A usable key can still be derived when ApiEncryptionKey is not set (rooted in the master key)")]
        public void AddPolhemFramework_NoApiEncryptionKey_DerivesUsableKey()
        {
            WithFramework("derived-fallback", sp =>
            {
                var provider = sp.GetRequiredService<IApiEncryptionKeyProvider>();
                var token = Guid.NewGuid();

                var generated = provider.GenerateKeyForLogin(token);

                Assert.Equal(64, generated.Length);
                Assert.Equal(generated, provider.GetKey(token));
            });
        }

        [Fact]
        [DisplayName("Configuring Dynamic explicitly resolves DynamicApiEncryptionKeyProvider")]
        public void AddPolhemFramework_ConfiguredDynamic_ResolvesDynamicApiEncryptionKeyProvider()
        {
            WithFramework("dynamic",
                sp => Assert.IsType<DynamicApiEncryptionKeyProvider>(sp.GetRequiredService<IApiEncryptionKeyProvider>()),
                configuration => configuration.Components.ApiEncryptionKeyProvider =
                    "Polhem.Business.Providers.DynamicApiEncryptionKeyProvider, Polhem.Business");
        }

        [Fact]
        [DisplayName("AddPolhemFramework with the default configuration resolves the whole service chain without throwing")]
        public void AddPolhemFramework_DefaultConfig_ResolvesServiceChainWithoutException()
        {
            WithFramework("chain", sp =>
            {
                var exception = Record.Exception(() =>
                {
                    _ = sp.GetRequiredService<IApiEncryptionKeyProvider>();
                    _ = sp.GetRequiredService<Polhem.Definition.Storage.IDefineAccess>();
                });

                Assert.Null(exception);
            });
        }
    }
}
