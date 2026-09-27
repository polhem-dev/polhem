using System.ComponentModel;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Security
{
    public class MasterKeyProviderRetryTests
    {
        [Fact]
        [DisplayName("GetMasterKey with autoCreate=true and a missing parent directory throws an IOException instead of creating the directory")]
        public void GetMasterKey_AutoCreate_ParentDirMissing_ThrowsIoException()
        {
            // The parent directory (polhem-missing-<guid>) does not exist. Creating the key file throws
            // DirectoryNotFoundException (a subclass of IOException). Only an IOException raised because
            // another process created the file first is treated as a lost race and read back; this one
            // propagates, and the provider does not create the directory on the caller's behalf.
            string missingParent = Path.Combine(
                Path.GetTempPath(),
                $"polhem-missing-{Guid.NewGuid():N}",
                "file.key");

            var source = new MasterKeySource { Type = MasterKeySourceType.File, Value = missingParent };

            var exception = Record.Exception(() =>
                MasterKeyProvider.GetMasterKey(source, definePath: string.Empty, autoCreate: true));

            Assert.NotNull(exception);
            Assert.IsType<IOException>(exception, exactMatch: false);
        }
    }
}
