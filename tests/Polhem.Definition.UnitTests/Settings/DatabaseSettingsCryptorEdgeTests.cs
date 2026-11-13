using System.ComponentModel;
using Polhem.Core.Security;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Settings
{
    /// <summary>
    /// Edge cases of <see cref="DatabaseSettingsCryptor"/>: a null key, empty passwords, and the password probe used to
    /// warn about a missing configuration key.
    /// </summary>
    public class DatabaseSettingsCryptorEdgeTests
    {
        [Fact]
        [DisplayName("EncryptInPlace and DecryptInPlace with a null key leave every password as it is")]
        public void EncryptAndDecryptInPlace_NullKey_AreNoOps()
        {
            var settings = BuildSettings("plain", "enc:xxx");

            DatabaseSettingsCryptor.EncryptInPlace(settings, null);
            DatabaseSettingsCryptor.DecryptInPlace(settings, null);

            Assert.Equal("plain", settings.Servers![0].Password);
            Assert.Equal("enc:xxx", settings.Items![0].Password);
        }

        [Fact]
        [DisplayName("EncryptInPlace leaves an empty password empty instead of encrypting it")]
        public void EncryptInPlace_EmptyPasswords_StayEmpty()
        {
            var settings = BuildSettings("", "");

            DatabaseSettingsCryptor.EncryptInPlace(settings, AesCbcHmacKeyGenerator.GenerateCombinedKey());

            Assert.Equal(string.Empty, settings.Servers![0].Password);
            Assert.Equal(string.Empty, settings.Items![0].Password);
        }

        [Fact]
        [DisplayName("HasPasswords is false when no server or item carries a password")]
        public void HasPasswords_NoPasswords_ReturnsFalse()
        {
            Assert.False(DatabaseSettingsCryptor.HasPasswords(BuildSettings("", "")));
            Assert.False(DatabaseSettingsCryptor.HasPasswords(new DatabaseSettings()));
        }

        [Fact]
        [DisplayName("HasPasswords is true when a server carries a password")]
        public void HasPasswords_ServerPassword_ReturnsTrue()
        {
            Assert.True(DatabaseSettingsCryptor.HasPasswords(BuildSettings("secret", "")));
        }

        [Fact]
        [DisplayName("HasPasswords is true when only an item carries a password, encrypted or not")]
        public void HasPasswords_ItemPassword_ReturnsTrue()
        {
            Assert.True(DatabaseSettingsCryptor.HasPasswords(BuildSettings("", "enc:xxx")));
        }

        [Fact]
        [DisplayName("HasPasswords rejects null settings")]
        public void HasPasswords_NullSettings_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => DatabaseSettingsCryptor.HasPasswords(null!));
        }

        private static DatabaseSettings BuildSettings(string serverPassword, string itemPassword)
        {
            var settings = new DatabaseSettings();
            settings.Servers!.Add(new DatabaseServer { Id = "S1", Password = serverPassword });
            settings.Items!.Add(new DatabaseItem { Id = "D1", Password = itemPassword });
            return settings;
        }
    }
}
