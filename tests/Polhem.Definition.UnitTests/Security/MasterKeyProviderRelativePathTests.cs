using System.ComponentModel;
using Polhem.Core.Security;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Security
{
    public class MasterKeyProviderRelativePathTests
    {
        [Fact]
        [DisplayName("GetMasterKey resolves a relative path against definePath")]
        public void GetMasterKey_RelativeFilePath_ResolvesAgainstDefinePath()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"polhem-mk-rel-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            string fileName = "relative.key";
            string fullPath = Path.Combine(tempDir, fileName);
            byte[] expected = AesCbcHmacKeyGenerator.GenerateCombinedKey();
            File.WriteAllText(fullPath, Convert.ToBase64String(expected));

            try
            {
                byte[] actual = MasterKeyProvider.GetMasterKey(
                    new MasterKeySource { Type = MasterKeySourceType.File, Value = fileName },
                    definePath: tempDir);

                Assert.Equal(expected, actual);
            }
            finally
            {
                if (File.Exists(fullPath)) File.Delete(fullPath);
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, false);
            }
        }

        [Fact]
        [DisplayName("GetMasterKey with a missing relative path and autoCreate=true creates the file under definePath")]
        public void GetMasterKey_RelativeFilePath_Missing_AutoCreate_CreatesInDefinePath()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"polhem-mk-relcreate-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            string fileName = "auto-created.key";
            string fullPath = Path.Combine(tempDir, fileName);

            try
            {
                byte[] result = MasterKeyProvider.GetMasterKey(
                    new MasterKeySource { Type = MasterKeySourceType.File, Value = fileName },
                    definePath: tempDir,
                    autoCreate: true);

                Assert.NotEmpty(result);
                Assert.True(File.Exists(fullPath));
            }
            finally
            {
                if (File.Exists(fullPath)) File.Delete(fullPath);
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, false);
            }
        }
    }
}
