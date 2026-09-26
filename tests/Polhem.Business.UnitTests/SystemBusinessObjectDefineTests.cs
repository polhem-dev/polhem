using System.ComponentModel;
using Polhem.Base.Serialization;
using Polhem.Business.System;
using Polhem.Definition;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.ObjectCaching;
using Polhem.Tests.Shared;

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
            var bo = new SystemBusinessObject(TestPolhemContext.Create(_fx), Guid.Empty, SysProgIds.System);

            var result = bo.GetCommonConfiguration(new GetCommonConfigurationArgs());

            Assert.False(string.IsNullOrWhiteSpace(result.CommonConfiguration));
        }

        [Fact]
        [DisplayName("GetDefine for DatabaseSettings returns XML for a local call")]
        public void GetDefine_LocalCallDatabaseSettings_ReturnsXml()
        {
            var bo = new SystemBusinessObject(TestPolhemContext.Create(_fx), Guid.Empty, SysProgIds.System, isLocalCall: true);
            var args = new GetDefineArgs { DefineType = DefineType.DatabaseSettings };

            var result = bo.GetDefine(args);

            Assert.NotNull(result);
            Assert.False(string.IsNullOrWhiteSpace(result.Xml));
        }

        [Fact]
        [DisplayName("GetDefine(DatabaseSettings) returns the stored file, never the decrypted cached instance")]
        public void GetDefine_DatabaseSettings_ServesAsStoredNotTheDecryptedCache()
        {
            // After `DecryptInPlace` in `GetDatabaseSettings()`, the cached instance holds plaintext passwords.
            // The contract of `GetDefine` is the definition as stored, so it must read the stored file rather than the cache,
            // otherwise the response would carry plaintext credentials.
            var access = _fx.GetRequiredService<IDefineAccess>();
            var cached = access.GetDatabaseSettings();          // Triggers decryption, so the cache now holds plaintext.
            var bo = new SystemBusinessObject(TestPolhemContext.Create(_fx), Guid.Empty, SysProgIds.System, isLocalCall: true);

            var xml = bo.GetDefine(new GetDefineArgs { DefineType = DefineType.DatabaseSettings }).Xml;
            var served = XmlCodec.Deserialize<DatabaseSettings>(xml!);

            Assert.NotNull(served);
            Assert.NotSame(cached, served);
            // Every returned password must be empty or keep its enc: ciphertext, never the decrypted plaintext.
            foreach (var password in (served.Servers ?? []).Select(s => s.Password)
                         .Concat((served.Items ?? []).Select(i => i.Password)))
            {
                Assert.True(string.IsNullOrEmpty(password) || password.StartsWith("enc:", StringComparison.Ordinal),
                    $"Password did not keep the enc: form: {password}");
            }
        }

        [Fact]
        [DisplayName("GetDefine for SystemSettings returns XML for a local call")]
        public void GetDefine_LocalCallSystemSettings_ReturnsXml()
        {
            var bo = new SystemBusinessObject(TestPolhemContext.Create(_fx), Guid.Empty, SysProgIds.System, isLocalCall: true);
            var args = new GetDefineArgs { DefineType = DefineType.SystemSettings };

            var result = bo.GetDefine(args);

            Assert.False(string.IsNullOrWhiteSpace(result.Xml));
        }

        [Fact]
        [DisplayName("SaveDefine for DbCategorySettings succeeds through the SaveDefineCore path for a local call")]
        public void SaveDefine_LocalCallDbCategorySettings_Succeeds()
        {
            var getBo = new SystemBusinessObject(TestPolhemContext.Create(_fx), Guid.Empty, SysProgIds.System, isLocalCall: true);
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
                    TestPolhemContext.CreateWithDefineAccess(_fx, tempAccess), Guid.Empty, SysProgIds.System, isLocalCall: true);

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
