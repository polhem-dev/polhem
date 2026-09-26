using System.ComponentModel;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Security
{
    public class MasterKeyProviderRetryTests
    {
        [Fact]
        [DisplayName("GetMasterKey with autoCreate=true and a missing parent directory falls through catch(IOException) and throws after the retries run out")]
        public void GetMasterKey_AutoCreate_ParentDirMissing_TriggersIoExceptionFallback()
        {
            // The parent directory (polhem-missing-<guid>) does not exist, so:
            //   `FileStream(FileMode.CreateNew)` throws DirectoryNotFoundException (a subclass of IOException),
            //   the catch(IOException) in `LoadFromFile` catches it and falls through to `ReadAllTextShared`,
            //   `ReadAllTextShared` retries (ReadRetryCount=5, with Thread.Sleep 50ms each time),
            //   and on the 5th attempt the filter (attempt < 4) is false, so the exception propagates.
            // Expected duration is about 200ms (4 x 50ms sleep).
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
