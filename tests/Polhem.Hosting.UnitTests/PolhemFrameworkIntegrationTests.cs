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
        [DisplayName("AddPolhemFramework 傳入有效組態且 autoCreateMasterKey=true 應完成服務注冊並回傳 IServiceCollection")]
        public void AddPolhemFramework_ValidConfigurationAutoCreateKey_RegistersServices()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"polhem-fw-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var services = new ServiceCollection();
                var configuration = new BackendConfiguration();
                var pathOptions = new PathOptions { DefinePath = tempDir };

                // autoCreateMasterKey=true → Master.key 自動建立於 tempDir
                // 同時涵蓋 DecryptSecurityKeys、CacheInfo.Initialize 及所有 AddSingleton 注冊路徑
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
        [DisplayName("AddPolhemFramework 設定 StaticApiEncryptionKeyProvider 解析服務應回傳靜態金鑰提供者")]
        public void AddPolhemFramework_StaticApiEncryptionKeyProvider_ResolvesStaticProvider()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"polhem-fw-static-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                // 產生主金鑰並寫入暫存目錄（預設 Master.key 路徑）
                string masterKeyBase64 = AesCbcHmacKeyGenerator.GenerateBase64CombinedKey();
                byte[] masterKey = Convert.FromBase64String(masterKeyBase64);
                File.WriteAllText(Path.Combine(tempDir, "Master.key"), masterKeyBase64);

                // 用主金鑰加密一把 API 金鑰
                string encryptedApiKey = EncryptionKeyProtector.GenerateEncryptedKey(masterKey);

                var configuration = new BackendConfiguration();
                // 此測試自己準備 Master.key 檔案,需要明確指定 File 來源覆寫
                // MasterKeySource 預設值(Environment)——否則框架會讀環境變數 POLHEM_MASTER_KEY,
                // 與本測試剛產生的 masterKey 對不上而 HMAC 驗證失敗。
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

                // 解析 IApiEncryptionKeyProvider 觸發 CreateApiEncryptionKeyProvider 私有方法的 static 分支
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
