using System.ComponentModel;
using Polhem.Base.Security;
using Polhem.Base.Serialization;
using Polhem.Business.System;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.ObjectCaching;
using Polhem.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Pure logic tests of <see cref="SystemBusinessObject"/> integrated with <c>IDefineAccess</c> (resolved through DI), using in-memory access without a DB.
    /// </summary>
    public class SystemBusinessObjectDefineTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public SystemBusinessObjectDefineTests(SharedDbFixture fx) { _fx = fx; }
        [Fact]
        [DisplayName("GetCommonConfiguration returns non-empty XML")]
        public void GetCommonConfiguration_ReturnsNonEmptyXml()
        {
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.Empty, SysProgIds.System);

            var result = bo.GetCommonConfiguration(new GetCommonConfigurationArgs());

            Assert.False(string.IsNullOrWhiteSpace(result.CommonConfiguration));
        }

        [Fact]
        [DisplayName("GetDefine for DatabaseSettings returns XML for a local call")]
        public void GetDefine_LocalCallDatabaseSettings_ReturnsXml()
        {
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.Empty, SysProgIds.System, isLocalCall: true);
            var args = new GetDefineArgs { DefineType = DefineType.DatabaseSettings };

            var result = bo.GetDefine(args);

            Assert.NotNull(result);
            Assert.False(string.IsNullOrWhiteSpace(result.Xml));
        }

        [Fact]
        [DisplayName("GetDefine(DatabaseSettings) returns the stored enc: passwords, never the decrypted cached instance")]
        public void GetDefine_DatabaseSettings_ServesAsStoredNotTheDecryptedCache()
        {
            // After `DecryptInPlace` in `GetDatabaseSettings()`, the cached instance holds plaintext passwords. The contract
            // of `GetDefine` is the definition as stored, so it must read the stored file rather than the cache, otherwise the
            // response would carry plaintext credentials. The fixture needs real passwords for this to be able to fail: with
            // none, the served and the cached instance look alike.
            const string serverPassword = "server-plain-pw";
            const string itemPassword = "item-plain-pw";
            var tempDir = Path.Combine(Path.GetTempPath(), $"polhem-dbsettings-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var tempPaths = new PathOptions { DefinePath = tempDir };
                var storage = new FileDefineStorage(tempPaths);
                // A private cache prefix: the fixture's container uses the empty prefix on the same process-wide
                // provider, and sharing it would replace the fixture's cached DatabaseSettings for every other test.
                var cache = new CacheContainerService(storage, tempPaths, "dbsettings-" + Guid.NewGuid().ToString("N"));
                var access = new CacheDefineAccess(storage, tempPaths, cache, AesCbcHmacKeyGenerator.GenerateCombinedKey());
                var stored = new DatabaseSettings();
                stored.Servers!.Add(new DatabaseServer { Id = "srv", Password = serverPassword });
                stored.Items!.Add(new DatabaseItem { Id = "db", Password = itemPassword });
                access.SaveDatabaseSettings(stored);   // Encrypts both passwords into their enc: form on disk.

                var cached = access.GetDatabaseSettings();   // Decrypts in place, so the cache now holds plaintext.
                Assert.Equal(serverPassword, cached.Servers!["srv"].Password);
                Assert.Equal(itemPassword, cached.Items!["db"].Password);

                var sp = _fx.Provider;
                var ctx = new BusinessObjectContext
                {
                    DefineAccess = access,
                    SessionInfoService = sp.GetRequiredService<ISessionInfoService>(),
                    LanguageService = new LanguageService(access, null),
                    BoFactory = sp.GetRequiredService<IBusinessObjectFactory>(),
                    Services = new TestOverrideServiceProvider(sp, (typeof(PathOptions), tempPaths)),
                };
                var bo = new SystemBusinessObject(ctx, Guid.Empty, SysProgIds.System, isLocalCall: true);

                var xml = bo.GetDefine(new GetDefineArgs { DefineType = DefineType.DatabaseSettings }).Xml;
                var served = XmlCodec.Deserialize<DatabaseSettings>(xml!)!;

                // Anti-vacuous: the loops below must have entries to check.
                Assert.NotEmpty(served.Servers!);
                Assert.NotEmpty(served.Items!);
                foreach (var server in served.Servers!)
                {
                    Assert.True(server.Password.StartsWith("enc:", StringComparison.Ordinal),
                        $"Server '{server.Id}' password is not in enc: form.");
                }
                foreach (var item in served.Items!)
                {
                    Assert.True(item.Password.StartsWith("enc:", StringComparison.Ordinal),
                        $"Item '{item.Id}' password is not in enc: form.");
                }
                Assert.DoesNotContain(serverPassword, xml!, StringComparison.Ordinal);
                Assert.DoesNotContain(itemPassword, xml!, StringComparison.Ordinal);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }

        [Fact]
        [DisplayName("GetDefine for SystemSettings returns XML for a local call")]
        public void GetDefine_LocalCallSystemSettings_ReturnsXml()
        {
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.Empty, SysProgIds.System, isLocalCall: true);
            var args = new GetDefineArgs { DefineType = DefineType.SystemSettings };

            var result = bo.GetDefine(args);

            Assert.False(string.IsNullOrWhiteSpace(result.Xml));
        }

        [Fact]
        [DisplayName("SaveDefine for DbCategorySettings succeeds through the SaveDefineCore path for a local call")]
        public void SaveDefine_LocalCallDbCategorySettings_Succeeds()
        {
            var getBo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.Empty, SysProgIds.System, isLocalCall: true);
            var getResult = getBo.GetDefine(new GetDefineArgs { DefineType = DefineType.DbCategorySettings });
            Assert.False(string.IsNullOrWhiteSpace(getResult.Xml));

            // `SaveDefine` writes files, so it uses a separate `IDefineAccess` pointing at a temp folder to avoid polluting tests/Define/.
            var tempDir = Path.Combine(Path.GetTempPath(), $"polhem-define-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var tempPaths = new PathOptions { DefinePath = tempDir };
                var tempAccess = new CacheDefineAccess(new FileDefineStorage(tempPaths), tempPaths);
                var saveBo = new SystemBusinessObject(
                    TestBusinessObjectContext.CreateWithDefineAccess(_fx, tempAccess), Guid.Empty, SysProgIds.System, isLocalCall: true);

                var saveResult = saveBo.SaveDefine(new SaveDefineArgs
                {
                    DefineType = DefineType.DbCategorySettings,
                    Xml = getResult.Xml
                });

                Assert.NotNull(saveResult);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }
    }
}
