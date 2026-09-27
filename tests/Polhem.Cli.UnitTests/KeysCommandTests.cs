using System.ComponentModel;
using Polhem.Base.Security;
using Polhem.Definition.Security;

namespace Polhem.Cli.UnitTests
{
    public class KeysCommandTests
    {
        [Fact]
        [DisplayName("keys protect with a master key file prints a value that the same master key decrypts to a 64-byte key")]
        public void Protect_MasterKeyFile_PrintsDecryptableKey()
        {
            string masterKeyText = AesCbcHmacKeyGenerator.GenerateBase64CombinedKey();
            string file = Path.Combine(Path.GetTempPath(), $"polhem-cli-mk-{Guid.NewGuid():N}.key");
            File.WriteAllText(file, masterKeyText);
            try
            {
                using var output = new StringWriter();

                int exitCode = KeysCommand.Run(["protect", "--master-key-file", file], output);

                Assert.Equal(ExitCodes.Success, exitCode);
                string cipherText = output.ToString().Trim();
                byte[] plainKey = EncryptionKeyProtector.DecryptEncryptedKey(Convert.FromBase64String(masterKeyText), cipherText);
                Assert.Equal(64, plainKey.Length);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        [DisplayName("keys protect prints a different key on every run")]
        public void Protect_TwoRuns_PrintDifferentKeys()
        {
            string masterKeyText = AesCbcHmacKeyGenerator.GenerateBase64CombinedKey();
            string file = Path.Combine(Path.GetTempPath(), $"polhem-cli-mk-{Guid.NewGuid():N}.key");
            File.WriteAllText(file, masterKeyText);
            try
            {
                byte[] masterKey = Convert.FromBase64String(masterKeyText);
                using var first = new StringWriter();
                using var second = new StringWriter();

                KeysCommand.Run(["protect", "--master-key-file", file], first);
                KeysCommand.Run(["protect", "--master-key-file", file], second);

                Assert.NotEqual(
                    EncryptionKeyProtector.DecryptEncryptedKey(masterKey, first.ToString().Trim()),
                    EncryptionKeyProtector.DecryptEncryptedKey(masterKey, second.ToString().Trim()));
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        [DisplayName("keys protect fails instead of generating a master key when the master key file does not exist")]
        public void Protect_MissingMasterKeyFile_Throws()
        {
            string file = Path.Combine(Path.GetTempPath(), $"polhem-cli-missing-{Guid.NewGuid():N}.key");

            Assert.Throws<FileNotFoundException>(
                () => KeysCommand.Run(["protect", "--master-key-file", file], TextWriter.Null));
            Assert.False(File.Exists(file));
        }

        [Fact]
        [DisplayName("keys protect fails when the named master key environment variable is not set")]
        public void Protect_UnsetMasterKeyVariable_Throws()
        {
            string variable = "POLHEM_CLI_TEST_" + Guid.NewGuid().ToString("N");

            var ex = Assert.Throws<InvalidOperationException>(
                () => KeysCommand.Run(["protect", "--master-key-env", variable], TextWriter.Null));

            Assert.Contains(variable, ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("keys protect rejects both master key options at once")]
        public void Protect_BothMasterKeyOptions_ThrowsUsage()
        {
            Assert.Throws<UsageException>(() => KeysCommand.Run(
                ["protect", "--master-key-file", "a.key", "--master-key-env", "X"], TextWriter.Null));
        }

        [Theory]
        [InlineData("protect", "--bogus")]
        [InlineData("rotate")]
        [DisplayName("keys rejects an unknown subcommand or option as a usage error")]
        public void Run_UnknownArgument_ThrowsUsage(params string[] args)
        {
            Assert.Throws<UsageException>(() => KeysCommand.Run(args, TextWriter.Null));
        }

        [Fact]
        [DisplayName("keys protect --help describes the master key options")]
        public void Protect_Help_ListsOptions()
        {
            using var output = new StringWriter();

            int exitCode = KeysCommand.Run(["protect", "--help"], output);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Contains("--master-key-file", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("--master-key-env", output.ToString(), StringComparison.Ordinal);
        }
    }
}
