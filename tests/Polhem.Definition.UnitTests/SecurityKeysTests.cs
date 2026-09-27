using System.ComponentModel;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;
using Polhem.Base;
using Polhem.Base.Security;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// Tests for SecurityKeys initialization and decryption.
    /// </summary>
    public class SecurityKeysTests
    {
        /// <summary>
        /// Verifies that SecurityKeys decrypts the API and cookie keys correctly.
        /// </summary>
        [Fact]
        [DisplayName("SecurityKeys decrypts the API and cookie keys correctly after initialization")]
        public void InitializeSecurityKeys_ValidMasterKey_DecryptsKeysCorrectly()
        {
            byte[] masterKey = AesCbcHmacKeyGenerator.GenerateCombinedKey();
            byte[] apiKey = AesCbcHmacKeyGenerator.GenerateCombinedKey();
            byte[] cookieKey = AesCbcHmacKeyGenerator.GenerateCombinedKey();

            AesCbcHmacKeyGenerator.FromCombinedKey(masterKey, out byte[] aesKey, out byte[] hmacKey);

            string apiEncrypted = Convert.ToBase64String(AesCbcHmacCryptor.Encrypt(apiKey, aesKey, hmacKey));
            string cookieEncrypted = Convert.ToBase64String(AesCbcHmacCryptor.Encrypt(cookieKey, aesKey, hmacKey));

            string filePath = SaveTempMasterKey(masterKey);

            var settings = new SecurityKeySettings
            {
                MasterKeySource = new MasterKeySource
                {
                    Type = MasterKeySourceType.File,
                    Value = filePath
                },
                ApiEncryptionKey = apiEncrypted,
                CookieEncryptionKey = cookieEncrypted
            };

            LoadSecurityKey(settings, out byte[] apiKey2, out byte[] cookieKey2);

            Assert.Equal(apiKey, apiKey2);
            Assert.Equal(cookieKey, cookieKey2);
        }

        /// <summary>
        /// Loads the key settings.
        /// </summary>
        /// <param name="settings">The key settings.</param>
        /// <param name="apiKey">Receives the API encryption key.</param>
        /// <param name="cookieKey">Receives the cookie encryption key.</param>
        private static void LoadSecurityKey(SecurityKeySettings settings, out byte[] apiKey, out byte[] cookieKey)
        {
            byte[] masterKey = MasterKeyProvider.GetMasterKey(settings.MasterKeySource, definePath: string.Empty);
            AesCbcHmacKeyGenerator.FromCombinedKey(masterKey, out var aesKey, out var hmacKey);

            apiKey = Array.Empty<byte>();
            cookieKey = Array.Empty<byte>();
            if (StringUtilities.IsNotEmpty(settings.ApiEncryptionKey))
            {
                byte[] bytes = Convert.FromBase64String(settings.ApiEncryptionKey);
                apiKey = AesCbcHmacCryptor.Decrypt(bytes, aesKey, hmacKey);
            }

            if (StringUtilities.IsNotEmpty(settings.CookieEncryptionKey))
            {
                byte[] bytes = Convert.FromBase64String(settings.CookieEncryptionKey);
                cookieKey = AesCbcHmacCryptor.Decrypt(bytes, aesKey, hmacKey);
            }
        }

        [Fact]
        [DisplayName("SecurityKeySettings.ToString returns the class name")]
        public void SecurityKeySettings_ToString_ReturnsClassName()
        {
            var settings = new SecurityKeySettings();

            Assert.Equal(nameof(SecurityKeySettings), settings.ToString());
        }

        /// <summary>
        /// Writes the master key to a temp file and returns the file path.
        /// </summary>
        /// <param name="key">The key bytes.</param>
        /// <returns>The file path.</returns>
        private static string SaveTempMasterKey(byte[] key)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp-master.key");
            File.WriteAllText(path, Convert.ToBase64String(key));
            return path;
        }
    }
}
