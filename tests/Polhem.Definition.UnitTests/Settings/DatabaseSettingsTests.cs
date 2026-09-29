using System.ComponentModel;
using Polhem.Core.Security;
using Polhem.Definition.Settings;
using Polhem.Definition.Database;

namespace Polhem.Definition.UnitTests.Settings
{
    /// <summary>
    /// Tests for the DatabaseSettings DTO behavior and DatabaseSettingsCryptor encryption and decryption.
    /// Encryption was moved out of the DTO into a separate static utility class and no longer depends on a process-wide encryption key.
    /// </summary>
    public class DatabaseSettingsTests
    {
        [Fact]
        [DisplayName("The default constructor initializes empty collections and the default state")]
        public void DefaultConstructor_InitializesDefaults()
        {
            var settings = new DatabaseSettings();

            Assert.NotNull(settings.Servers);
            Assert.NotNull(settings.Items);
            Assert.Empty(settings.Servers!);
            Assert.Empty(settings.Items!);
            Assert.Equal(string.Empty, settings.ObjectFilePath);
        }

        [Fact]
        [DisplayName("Servers is not serialized while it is empty, whether or not it was read")]
        public void Servers_EmptyCollection_IsNotSerialized()
        {
            var settings = new DatabaseSettings();

            Assert.False(settings.ServersSpecified);
            Assert.Empty(settings.Servers!);
            Assert.False(settings.ServersSpecified);
        }

        [Fact]
        [DisplayName("Items is not serialized while it is empty, whether or not it was read")]
        public void Items_EmptyCollection_IsNotSerialized()
        {
            var settings = new DatabaseSettings();

            Assert.False(settings.ItemsSpecified);
            Assert.Empty(settings.Items!);
            Assert.False(settings.ItemsSpecified);
        }

        [Fact]
        [DisplayName("SetObjectFilePath updates the file path")]
        public void SetObjectFilePath_UpdatesPath()
        {
            var settings = new DatabaseSettings();

            settings.SetObjectFilePath("/tmp/databases.xml");

            Assert.Equal("/tmp/databases.xml", settings.ObjectFilePath);
        }

        [Fact]
        [DisplayName("Clone deep-copies Servers and Items")]
        public void Clone_DeepCopiesServersAndItems()
        {
            var settings = new DatabaseSettings();
            settings.Servers!.Add(new DatabaseServer
            {
                Id = "S1",
                DisplayName = "主伺服器",
                DatabaseType = DatabaseType.SQLServer,
                ConnectionString = "Server=.;",
                UserId = "sa",
                Password = "p@ss"
            });
            settings.Items!.Add(new DatabaseItem
            {
                Id = "D1",
                DisplayName = "共用",
                DatabaseType = DatabaseType.SQLServer,
                ServerId = "S1",
                ConnectionString = "Server=.;",
                DbName = "common",
                UserId = "sa",
                Password = "p@ss"
            });

            var clone = settings.Clone();

            Assert.NotSame(settings, clone);
            Assert.NotSame(settings.Servers!, clone.Servers!);
            Assert.Single(clone.Servers!);
            Assert.Single(clone.Items!);
            Assert.Equal("S1", clone.Servers![0].Id);
            Assert.Equal("D1", clone.Items![0].Id);
            Assert.NotSame(settings.Servers[0], clone.Servers![0]);
        }

        [Fact]
        [DisplayName("Cryptor.EncryptInPlace does not modify Password when the key is empty")]
        public void Cryptor_EncryptInPlace_NoKey_IsNoOp()
        {
            var settings = new DatabaseSettings();
            settings.Servers!.Add(new DatabaseServer { Id = "S1", Password = "plain" });
            settings.Items!.Add(new DatabaseItem { Id = "D1", Password = "plain" });

            DatabaseSettingsCryptor.EncryptInPlace(settings, Array.Empty<byte>());

            Assert.Equal("plain", settings.Servers![0].Password);
            Assert.Equal("plain", settings.Items![0].Password);
        }

        [Fact]
        [DisplayName("Cryptor.DecryptInPlace does not modify Password when the key is empty")]
        public void Cryptor_DecryptInPlace_NoKey_IsNoOp()
        {
            var settings = new DatabaseSettings();
            settings.Servers!.Add(new DatabaseServer { Id = "S1", Password = "enc:xxx" });
            settings.Items!.Add(new DatabaseItem { Id = "D1", Password = "enc:xxx" });

            DatabaseSettingsCryptor.DecryptInPlace(settings, Array.Empty<byte>());

            Assert.Equal("enc:xxx", settings.Servers![0].Password);
            Assert.Equal("enc:xxx", settings.Items![0].Password);
        }

        [Fact]
        [DisplayName("Cryptor.EncryptInPlace encrypts a plaintext Password and adds the enc: prefix")]
        public void Cryptor_EncryptInPlace_EncryptsPlainPassword()
        {
            var key = AesCbcHmacKeyGenerator.GenerateCombinedKey();
            var settings = new DatabaseSettings();
            settings.Servers!.Add(new DatabaseServer { Id = "S1", Password = "plain-server" });
            settings.Items!.Add(new DatabaseItem { Id = "D1", Password = "plain-item" });

            DatabaseSettingsCryptor.EncryptInPlace(settings, key);

            Assert.StartsWith("enc:", settings.Servers![0].Password);
            Assert.StartsWith("enc:", settings.Items![0].Password);
            Assert.NotEqual("plain-server", settings.Servers[0].Password);
            Assert.NotEqual("plain-item", settings.Items[0].Password);
        }

        [Fact]
        [DisplayName("Cryptor.EncryptInPlace does not encrypt a Password that already has the enc: prefix again")]
        public void Cryptor_EncryptInPlace_AlreadyEncrypted_NotReEncrypted()
        {
            var key = AesCbcHmacKeyGenerator.GenerateCombinedKey();
            var settings = new DatabaseSettings();
            settings.Servers!.Add(new DatabaseServer { Id = "S1", Password = "enc:fake-base64" });
            settings.Items!.Add(new DatabaseItem { Id = "D1", Password = "enc:fake-base64" });

            DatabaseSettingsCryptor.EncryptInPlace(settings, key);

            Assert.Equal("enc:fake-base64", settings.Servers![0].Password);
            Assert.Equal("enc:fake-base64", settings.Items![0].Password);
        }

        [Fact]
        [DisplayName("Cryptor Encrypt and Decrypt round-trip a plaintext Password")]
        public void Cryptor_EncryptThenDecrypt_RoundTripsPassword()
        {
            var key = AesCbcHmacKeyGenerator.GenerateCombinedKey();
            var settings = new DatabaseSettings();
            settings.Servers!.Add(new DatabaseServer { Id = "S1", Password = "pa$$w0rd-server" });
            settings.Items!.Add(new DatabaseItem { Id = "D1", Password = "pa$$w0rd-item" });

            DatabaseSettingsCryptor.EncryptInPlace(settings, key);
            Assert.StartsWith("enc:", settings.Servers![0].Password);

            DatabaseSettingsCryptor.DecryptInPlace(settings, key);
            Assert.Equal("pa$$w0rd-server", settings.Servers![0].Password);
            Assert.Equal("pa$$w0rd-item", settings.Items![0].Password);
        }

        [Fact]
        [DisplayName("Cryptor.DecryptInPlace does not modify a plaintext Password")]
        public void Cryptor_DecryptInPlace_PlainPassword_LeftUnchanged()
        {
            var key = AesCbcHmacKeyGenerator.GenerateCombinedKey();
            var settings = new DatabaseSettings();
            settings.Servers!.Add(new DatabaseServer { Id = "S1", Password = "plain" });
            settings.Items!.Add(new DatabaseItem { Id = "D1", Password = string.Empty });

            DatabaseSettingsCryptor.DecryptInPlace(settings, key);

            Assert.Equal("plain", settings.Servers![0].Password);
            Assert.Equal(string.Empty, settings.Items![0].Password);
        }

        [Fact]
        [DisplayName("Cryptor.EncryptInPlace on a Clone does not affect the source object's Password")]
        public void Clone_ThenEncrypt_DoesNotMutateSource()
        {
            var key = AesCbcHmacKeyGenerator.GenerateCombinedKey();
            var source = new DatabaseSettings();
            source.Servers!.Add(new DatabaseServer { Id = "S1", Password = "plain-server" });
            source.Items!.Add(new DatabaseItem { Id = "D1", Password = "plain-item" });

            var copy = source.Clone();
            DatabaseSettingsCryptor.EncryptInPlace(copy, key);

            Assert.StartsWith("enc:", copy.Servers![0].Password);
            Assert.StartsWith("enc:", copy.Items![0].Password);

            Assert.Equal("plain-server", source.Servers![0].Password);
            Assert.Equal("plain-item", source.Items![0].Password);
            Assert.NotSame(source.Servers[0], copy.Servers[0]);
            Assert.NotSame(source.Items[0], copy.Items[0]);
        }

        [Fact]
        [DisplayName("Cryptor.DecryptInPlace returns an empty string for an enc: Password with invalid base64")]
        public void Cryptor_DecryptInPlace_InvalidBase64_ReturnsEmpty()
        {
            var key = AesCbcHmacKeyGenerator.GenerateCombinedKey();
            var settings = new DatabaseSettings();
            settings.Servers!.Add(new DatabaseServer { Id = "S1", Password = "enc:not-valid-base64!!!" });
            settings.Items!.Add(new DatabaseItem { Id = "D1", Password = "enc:not-valid-base64!!!" });

            DatabaseSettingsCryptor.DecryptInPlace(settings, key);

            Assert.Equal(string.Empty, settings.Servers![0].Password);
            Assert.Equal(string.Empty, settings.Items![0].Password);
        }
    }
}
